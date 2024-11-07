using NF.UnityLibs.Managers.Patcher.Common;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine.Networking;
using UnityEngine;

namespace NF.UnityLibs.Managers.Patcher.Impl
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
        private Task<bool> _downloadAllTask;
        private PatchFileList.PatchFileInfo[] _infoArr;
        private Queue<int> _concurrentIdQueue;
        private Option _option;
        private bool _isDisposed;
        private bool _isError;

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


        public TaskAwaiter<bool> GetAwaiter()
        {
            return _downloadAllTask.GetAwaiter();
        }

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

        private async Task<bool> _DownloadAll()
        {

            Task<bool>[] taskArr = ArrayPool<Task<bool>>.Shared.Rent(_infoArr.Length);
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
                            return false;
                        }
                        await Task.Yield();
                    }
                    taskArr[i] = _DownloadPerFile(qid, info);
                }
                bool[] xs = await Task.WhenAll(taskArr.Take(_infoArr.Length));
                return xs.All(x => x);
            }
            finally
            {
                ArrayPool<Task<bool>>.Shared.Return(taskArr);
            }
        }

        private async Task<bool> _DownloadPerFile(int qid, PatchFileList.PatchFileInfo info)
        {
            if (_IsError())
            {
                return false;
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
                            return false;
                        }
                        await Task.Yield();
                        Debug.Log($"tick {qid} - {op.progress} - {info}");
                    }
                    if (uwr.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"uwr.error: {uwr.error} / uwr.responseCode: {uwr.responseCode} / url: {url} / info: {info}");
                        _isError = true;
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _isError = true;
                return false;
            }
            finally
            {
                _concurrentIdQueue.Enqueue(qid);
            }
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

        public void OnCompleted(Action continuation)
        {
            _downloadAllTask.GetAwaiter().OnCompleted(continuation);
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            _downloadAllTask.GetAwaiter().OnCompleted(continuation);
        }
    }
}
