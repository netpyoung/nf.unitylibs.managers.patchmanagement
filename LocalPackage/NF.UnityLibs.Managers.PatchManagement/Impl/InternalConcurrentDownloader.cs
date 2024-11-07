using NF.UnityLibs.Managers.PatchManagement.Common;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine.Networking;
using UnityEngine;

namespace NF.UnityLibs.Managers.PatchManagement.Impl
{
    internal sealed class InternalConcurrentDownloader : IDisposable, INotifyCompletion, ICriticalNotifyCompletion
    {
        public sealed class Option
        {
            public int ConcurrentWebRequestMax;
            public string RemoteURL_Parent = string.Empty;
            public string PatchDirectory = string.Empty;
            public int PatchItemMax;
            public long PatchItemByteMax;
            public UnityEngine.Object? UnityObject;
        }

        private CancellationTokenSource _cancelTokenSource;
        private CancellationToken _cancelToken;
        private Task<Exception?> _downloadAllTask;
        private PatchFileList.PatchFileInfo[] _infoArr;
        private Queue<int> _concurrentIdQueue;
        private Option _option;
        private bool _isDisposed;
        private bool _isError;
        private Exception? _savedExceptionOrNull;

        public static InternalConcurrentDownloader DownloadAll(Option option, PatchFileList.PatchFileInfo[] infoArr)
        {
            InternalConcurrentDownloader ret = new InternalConcurrentDownloader(option, infoArr);
            ret._isError = false;
            ret._isDisposed = false;
            return ret;
        }


        private InternalConcurrentDownloader(Option option, PatchFileList.PatchFileInfo[] infoArr)
        {
            _infoArr = infoArr;
            _cancelTokenSource = new CancellationTokenSource();
            _cancelToken = _cancelTokenSource.Token;
            TaskScheduler unityScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            _downloadAllTask = Task.Factory.StartNew(() => _DownloadAll(), _cancelToken, TaskCreationOptions.None, unityScheduler).Unwrap();

            _option = option;
            _concurrentIdQueue = new Queue<int>(option.ConcurrentWebRequestMax);
            for (int qid = 0; qid < option.ConcurrentWebRequestMax; ++qid)
            {
                _concurrentIdQueue.Enqueue(qid);
            }
#if UNITY_IOS
            UnityEngine.iOS.Device.SetNoBackupFlag(Application.persistentDataPath);
#endif // UNITY_IOS

#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif // UNITY_EDITOR
        }

        ~InternalConcurrentDownloader()
        {
            Dispose();
        }

#if UNITY_EDITOR
        private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                Dispose();
                UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            }
        }
#endif // UNITY_EDITOR

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _cancelTokenSource.Dispose();
            _isDisposed = true;
            Debug.LogWarning("Disposed!!!!");
        }

        #region For Await
        public TaskAwaiter<Exception?> GetAwaiter()
        {
            return _downloadAllTask.GetAwaiter();
        }

        public bool IsCompleted => _downloadAllTask.IsCompleted;

        public Exception? GetResult()
        {
            return _downloadAllTask.GetAwaiter().GetResult();
        }

        public void OnCompleted(Action continuation)
        {
            _downloadAllTask.GetAwaiter().OnCompleted(continuation);
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            _downloadAllTask.GetAwaiter().OnCompleted(continuation);
        }
        #endregion For Await

        private async Task<Exception?> _DownloadAll()
        {

            Task<Exception?>[] taskArr = ArrayPool<Task<Exception?>>.Shared.Rent(_infoArr.Length);
            try
            {
                int qid;
                for (int i = 0; i < _infoArr.Length; ++i)
                {
                    PatchFileList.PatchFileInfo info = _infoArr[i];
                    while (!_concurrentIdQueue.TryDequeue(out qid))
                    {
                        if (_IsError())
                        {
                            return _GetError();
                        }
                        await Task.Yield();
                    }
                    taskArr[i] = _DownloadPerFile(qid, info);
                }
                for (int i = 0; i < _infoArr.Length; ++i)
                {
                    Exception? exOrNull = await taskArr[i];
                    if (exOrNull != null)
                    {
                        return _SetError(exOrNull);
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return _SetError(ex);
            }
            finally
            {
                ArrayPool<Task<Exception?>>.Shared.Return(taskArr);
            }
        }

        private async Task<Exception?> _DownloadPerFile(int qid, PatchFileList.PatchFileInfo info)
        {
            if (_IsError())
            {
                return _GetError();
            }
            try
            {
                string url = $"{_option.RemoteURL_Parent}/{info.Name}";
                string fpath = $"{_option.PatchDirectory}/{info.Name}";
                using (UnityWebRequest uwr = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET))
                {
                    DownloadHandlerFile downloadHandler = new DownloadHandlerFile(fpath)
                    {
                        removeFileOnAbort = true
                    };
                    uwr.downloadHandler = downloadHandler;
                    UnityWebRequestAsyncOperation op = uwr.SendWebRequest();
                    while (!op.isDone)
                    {
                        if (_IsError())
                        {
                            return _GetError();
                        }
                        await Task.Yield();
                    }
                    if (uwr.result != UnityWebRequest.Result.Success)
                    {
                        return _SetError(new PatchManagerException($"uwr.error: {uwr.error} / uwr.responseCode: {uwr.responseCode} / url: {url} / info: {info}"));
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return _SetError(ex);
            }
            finally
            {
                _concurrentIdQueue.Enqueue(qid);
            }
        }

        private Exception _GetError()
        {
            return _savedExceptionOrNull!;
        }

        private Exception _SetError(Exception ex)
        {
            _savedExceptionOrNull = ex;
            _isError = true;
            return _savedExceptionOrNull;
        }

        private bool _IsError()
        {
            if (_isError)
            {
                return true;
            }
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                _isError = true;
                return true;
            }
#endif // UNITY_EDITOR
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                _isError = true;
                return true;
            }
            if (_cancelToken.IsCancellationRequested)
            {
                _isError = true;
                return true;
            }
            return false;
        }
    }
}
