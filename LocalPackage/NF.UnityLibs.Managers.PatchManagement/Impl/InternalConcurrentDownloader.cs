using NF.UnityLibs.Managers.PatchManagement.Common;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace NF.UnityLibs.Managers.PatchManagement.Impl
{
    // TODO(pyoung): dispose timing for awaiter
    internal sealed class InternalConcurrentDownloader : IDisposable, INotifyCompletion, ICriticalNotifyCompletion
    {
        public sealed class Option
        {
            public int ConcurrentWebRequestMax;
            public string RemoteURL_Parent = string.Empty;
            public string PatchDirectory = string.Empty;
            public int PatchItemMax;
            public long PatchItemByteMax;
            public IPatchManagerEventReceiver EventReceiver = new IPatchManagerEventReceiver.DummyPatchManagerEventReceiver();
        }

        private struct EventStorage : IDisposable
        {
            private Option _option;
            private long _bytesDownloadedPerSecond;
            private long _previousBytesDownloaded;
            private bool _isDisposed;
            private long _accByte;

            private List<ProgressFileInfo> _registerdProgressFileInfo;
            public EventStorage(Option option, long needToDownloadByte)
            {
                _option = option;
                _isDisposed = false;
                _registerdProgressFileInfo = new List<ProgressFileInfo>(option.ConcurrentWebRequestMax);
                long alreadyDownloaedByte = option.PatchItemByteMax - needToDownloadByte;
                _previousBytesDownloaded = alreadyDownloaedByte;
                _accByte = alreadyDownloaedByte;
                _bytesDownloadedPerSecond = 0;
            }

            public void Dispose()
            {
                _isDisposed = true;
            }

            internal void RegisterProgressFileInfo(ProgressFileInfo info)
            {
                _registerdProgressFileInfo.Add(info);
            }
            internal void UnregisterProgressFileInfo(ProgressFileInfo info)
            {
                _accByte += info.PatchFileInfo.Bytes;
                _registerdProgressFileInfo.Remove(info);
            }

            internal void OnProgressFile(ProgressFileInfo info, float currProgress)
            {
                if (_isDisposed)
                {
                    return;
                }
                if (currProgress == info.ProgressInFileDownload)
                {
                    return;
                }
                info.ProgressInFileDownload = currProgress;
                info.BytesDownloaded = (long)(info.PatchFileInfo.Bytes * currProgress);
                _option.EventReceiver.OnProgressFileInfo(info);
            }

            internal void TickPer30FPS()
            {
                if (_isDisposed)
                {
                    return;
                }
                long totalBytesDownloaded = GetTotalBytesDownloaded();
                float totalProgress = (float)((double)totalBytesDownloaded / _option.PatchItemByteMax);
                _option.EventReceiver.OnProgressTotal(totalProgress, _bytesDownloadedPerSecond);
            }

            internal void TickPerOneSecond()
            {
                if (_isDisposed)
                {
                    return;
                }
                long totalBytesDownloaded = GetTotalBytesDownloaded();
                _bytesDownloadedPerSecond = totalBytesDownloaded - _previousBytesDownloaded;
                _previousBytesDownloaded = totalBytesDownloaded;
            }

            private long GetTotalBytesDownloaded()
            {
                return _accByte + _registerdProgressFileInfo.Sum(x => x.BytesDownloaded);
            }
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
        private EventStorage ___eventStorage___;
        private bool _isStopWatch;

        public static InternalConcurrentDownloader DownloadAll(Option option, PatchFileList.PatchFileInfo[] infoArr)
        {
            InternalConcurrentDownloader ret = new InternalConcurrentDownloader(option, infoArr);
            ret._isError = false;
            ret._isDisposed = false;
            ret.___eventStorage___ = new EventStorage(option, infoArr.Sum(x => x.Bytes));
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
            ___eventStorage___.Dispose();
            _cancelTokenSource.Cancel();
            _cancelTokenSource.Dispose();
            _isDisposed = true;
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
                Task watchTask = _Watch();
                int qid;
                for (int i = 0; i < _infoArr.Length; ++i)
                {
                    PatchFileList.PatchFileInfo fileInfo = _infoArr[i];
                    while (!_concurrentIdQueue.TryDequeue(out qid))
                    {
                        if (_IsError())
                        {
                            return _GetError();
                        }
                        await Task.Yield();
                    }

                    ProgressFileInfo progressInfo = new ProgressFileInfo
                    {
                        PatchFileInfo = fileInfo,
                        ProgressInFileDownload = 0,
                        ConcurrentIndex = qid,
                        ItemCurrIndex = i,
                        ItemMaxLength = _infoArr.Length,
                        BytesDownloaded = 0,
                    };
                    taskArr[i] = _DownloadPerFile(progressInfo);
                }
                for (int i = 0; i < _infoArr.Length; ++i)
                {
                    Exception? exOrNull = await taskArr[i];
                    if (exOrNull != null)
                    {
                        return _SetError(exOrNull);
                    }
                }
                _isStopWatch = true;
                await watchTask;
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

        private async Task<Exception?> _DownloadPerFile(ProgressFileInfo progressInfo)
        {
            if (_IsError())
            {
                return _GetError();
            }
            try
            {
                ___eventStorage___.RegisterProgressFileInfo(progressInfo);
                ___eventStorage___.OnProgressFile(progressInfo, 0);
                string url = $"{_option.RemoteURL_Parent}/{progressInfo.PatchFileInfo.Name}";
                string fpath = $"{_option.PatchDirectory}/{progressInfo.PatchFileInfo.Name}";
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
                        ___eventStorage___.OnProgressFile(progressInfo, op.progress);
                    }
                    if (uwr.result != UnityWebRequest.Result.Success)
                    {
                        return _SetError(new PatchManagerException(E_EXCEPTION_KIND.ERR_WEBREQUEST_FAIL, $"uwr.error: {uwr.error} / uwr.responseCode: {uwr.responseCode} / url: {url} / progressInfo: {progressInfo}"));
                    }
                }
                ___eventStorage___.OnProgressFile(progressInfo, 1);

                return null;
            }
            catch (Exception ex)
            {
                return _SetError(ex);
            }
            finally
            {
                ___eventStorage___.UnregisterProgressFileInfo(progressInfo);
                _concurrentIdQueue.Enqueue(progressInfo.ConcurrentIndex);
            }
        }

        private async Task _Watch()
        {
            long totalElapsedTime = 0;

            while (true)
            {
                if (_isStopWatch)
                {
                    return;
                }
                if (_IsError())
                {
                    return;
                }

                await Task.Delay(30);
                ___eventStorage___.TickPer30FPS();

                totalElapsedTime += 30;
                if (totalElapsedTime >= 1000)
                {
                    totalElapsedTime -= 1000;
                    ___eventStorage___.TickPerOneSecond();
                }
            }
        }

        private Exception _GetError()
        {
            return _savedExceptionOrNull!;
        }

        private Exception _SetError(Exception ex)
        {
            _cancelTokenSource.Cancel();
            _savedExceptionOrNull = ex;
            _isError = true;
            _isStopWatch = true;
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
                _SetError(new PatchManagerException(E_EXCEPTION_KIND.ERR_APPLICATION_IS_NOT_PLAYING, "!Application.isPlaying"));
                return true;
            }
#endif // UNITY_EDITOR
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                _SetError(new PatchManagerException(E_EXCEPTION_KIND.ERR_NETWORK_IS_NOT_REACHABLE, "Application.internetReachability == NetworkReachability.NotReachable"));
                return true;
            }
            if (_cancelToken.IsCancellationRequested)
            {
                _SetError(new PatchManagerException(E_EXCEPTION_KIND.ERR_TASK_IS_CANCELED, "_cancelToken.IsCancellationRequested"));
                return true;
            }
            return false;
        }
    }
}
