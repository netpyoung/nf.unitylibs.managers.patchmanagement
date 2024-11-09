using Newtonsoft.Json;
using NF.UnityLibs.Managers.PatchManagement.Common;
using NF.UnityLibs.Managers.PatchManagement.Impl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace NF.UnityLibs.Managers.PatchManagement
{
    // remoteURL_Full    : https://helloworld.com/blabla/hello/a.txt
    // remoteURL_Parent  : https://helloworld.com/blabla/hello
    // remoteURL_Base    : https://helloworld.com
    // remoteURL_SubPath : blabla/hello
    // remoteURL_Filename: a.txt

    [Serializable]
    public sealed class PatchManager : IDisposable
    {
        internal class Option
        {
            public string RemoteURL_Base { get; internal set; } = string.Empty;
            public string RemoteURL_SubPath { get; internal set; } = string.Empty;
            public string DevicePersistentPrefix { get; internal set; } = string.Empty;
            public int ConcurrentWebRequestMax { get; internal set; } = 1;
            public IPatchManagerEventReceiver EventReceiver { get; internal set; } = new IPatchManagerEventReceiver.DummyPatchManagerEventReceiver();

            public Exception? Validate()
            {
                if (ConcurrentWebRequestMax < 1)
                {
                    return new PatchManagerException(E_EXCEPTION_KIND.ERR_FAIL_OPTION_VALIDATE, $"ConcurrentWebRequestMax < 1 | ConcurrentWebRequestMax: {ConcurrentWebRequestMax}");
                }

                if (string.IsNullOrEmpty(RemoteURL_Base))
                {
                    return new PatchManagerException(E_EXCEPTION_KIND.ERR_FAIL_OPTION_VALIDATE, $"string.IsNullOrEmpty(RemoteURL_Base) | RemoteURL_Base: {RemoteURL_Base}");
                }

                if (string.IsNullOrEmpty(RemoteURL_SubPath))
                {
                    return new PatchManagerException(E_EXCEPTION_KIND.ERR_FAIL_OPTION_VALIDATE, $"string.IsNullOrEmpty(RemoteURL_SubPath) | RemoteURL_SubPath: {RemoteURL_SubPath}");
                }

                if (string.IsNullOrEmpty(DevicePersistentPrefix))
                {
                    return new PatchManagerException(E_EXCEPTION_KIND.ERR_FAIL_OPTION_VALIDATE, $"string.IsNullOrEmpty(DevicePersistentPrefix) | DevicePersistentPrefix: {DevicePersistentPrefix}");
                }
                return null;
            }
        }

        private Option _option;
        private CancellationTokenSource _cancelTokenSource = default!;
        private bool _isDisposed;

        public string RemoteURL_Base { get => _option.RemoteURL_Base; }
        public string RemoteURL_SubPath { get => _option.RemoteURL_SubPath; }
        public string DevicePersistentPrefix { get => _option.DevicePersistentPrefix; }

        internal PatchManager(Option option)
        {
            _option = option;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif // UNITY_EDITOR
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

        ~PatchManager()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            if (_cancelTokenSource != null)
            {
                _cancelTokenSource.Cancel();
                _cancelTokenSource.Dispose();
            }
            _isDisposed = true;
        }

        private PatchFileList? GetCurrPatchFileListOrNull(string fpath)
        {
            if (!File.Exists(fpath))
            {
                return null;
            }

            string json = File.ReadAllText(fpath);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                PatchFileList? ret = JsonConvert.DeserializeObject<PatchFileList>(json);
                return ret;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return null;
            }
        }

        public async Task<Exception?> FromCurrentAppVersion()
        {
            string appVersion = Application.version;
            return await FromAppVersion(appVersion);
        }

        public async Task<Exception?> FromAppVersion(string appVersion)
        {
            _option.EventReceiver.OnStateChanged(E_PATCH_STATE.RECIEVE_PATCHBUILDVERSION_START);
            int patchBuildVersion = 0;
            {
                string url = $"{RemoteURL_Base}/{RemoteURL_SubPath}/{nameof(PatchVersion)}.json";
                (PatchVersion? patchVersionOrNull, Exception? exOrNull) = await CustomUnityWebRequest.Get<PatchVersion>(url);
                if (exOrNull != null)
                {
                    return exOrNull!;
                }

                PatchVersion patchVersion = patchVersionOrNull!;
                if (patchVersion.TryGetValue(appVersion, out patchBuildVersion))
                {
                }
                else if (patchVersion.TryGetValue("latest", out patchBuildVersion))
                {
                }
                else
                {
                    return new PatchManagerException(E_EXCEPTION_KIND.ERR_FAIL_TO_GET_PATCHBUILDVERSION, $"failed to get version from patchVersion\n{patchVersion.ToJson()}");
                }
            }
            _option.EventReceiver.OnStateChanged(E_PATCH_STATE.RECIEVE_PATCHBUILDVERSION_END, $"patchBuildVersion: {patchBuildVersion}");
            return await FromPatchBuildVersion(patchBuildVersion);
        }

        public async Task<Exception?> FromPatchBuildVersion(int patchBuildVersion)
        {
            if (_cancelTokenSource != null)
            {
                _cancelTokenSource.Cancel();
                _cancelTokenSource.Dispose();
            }
            _cancelTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = _cancelTokenSource.Token;

            string currPatchFileListFpath = $"{Application.persistentDataPath}/{DevicePersistentPrefix}/{nameof(PatchFileList)}.json";
            PatchFileList? currPatchFileListOrNull = GetCurrPatchFileListOrNull(currPatchFileListFpath);
            if (currPatchFileListOrNull != null)
            {
                PatchFileList currPatchFileList = currPatchFileListOrNull!;
                if (currPatchFileList.Version == patchBuildVersion)
                {
                    return null;
                }
            }
            _option.EventReceiver.OnStateChanged(E_PATCH_STATE.PATCHFILELIST_CURR, $"currPatchFileListOrNull: {currPatchFileListOrNull}");

            PatchFileList nextPatchFileList;
            {
                string url = $"{RemoteURL_Base}/{RemoteURL_SubPath}/{patchBuildVersion}/{nameof(PatchFileList)}.json";
                (PatchFileList? remotePatchFileListOrNull, Exception? exOrNull) = await CustomUnityWebRequest.Get<PatchFileList>(url);
                if (exOrNull != null)
                {
                    return exOrNull!;
                }

                nextPatchFileList = remotePatchFileListOrNull!;
            }
            _option.EventReceiver.OnStateChanged(E_PATCH_STATE.PATCHFILELIST_NEXT, $"nextPatchFileList: {nextPatchFileList}");

            string patchDir = $"{Application.persistentDataPath}/{DevicePersistentPrefix}";
            List<PatchFileListDifference.PatchStatus> patchStatusList;
            {
                // Collecting diff
                _option.EventReceiver.OnStateChanged(E_PATCH_STATE.PATCHFILELIST_DIFF_COLLECT_START);
                List<PatchFileListDifference.PatchStatus>? patchStatusListOrNull = await PatchFileListDifference.DifferenceSetOrNull(currPatchFileListOrNull, nextPatchFileList, patchDir, cancellationToken);
                if (patchStatusListOrNull == null)
                {
                    return new PatchManagerException(E_EXCEPTION_KIND.ERR_SYSTEM_EXCEPTION, "Internal Exception: patchStatusListOrNull == null");
                }
                patchStatusList = patchStatusListOrNull!;
                _option.EventReceiver.OnStateChanged(E_PATCH_STATE.PATCHFILELIST_DIFF_COLLECT_END);
            }

            {
                _option.EventReceiver.OnStateChanged(E_PATCH_STATE.PATCHFILELIST_VALIDATE);
                {
                    // Skip
                    patchStatusList.RemoveAll(x => x.State == PatchFileListDifference.PatchStatus.E_STATE.SKIP);
                }
                {
                    // Delete
                    PatchFileListDifference.PatchStatus[] items = patchStatusList.Where(x => x.State == PatchFileListDifference.PatchStatus.E_STATE.DELETE).ToArray();
                    foreach (PatchFileListDifference.PatchStatus item in items)
                    {
                        string fpath = $"{patchDir}/{item.PatchFileInfo.Name}";
                        if (!File.Exists(fpath))
                        {
                            continue;
                        }

                        try
                        {
                            File.Delete(fpath);
                        }
                        catch (Exception ex)
                        {
                            return ex;
                        }
                    }
                }
                {
                    // Update
                    PatchFileListDifference.PatchStatus[] updateStatusArr = patchStatusList.Where(x => x.State == PatchFileListDifference.PatchStatus.E_STATE.UPDATE).ToArray();
                    if (updateStatusArr.Length != 0)
                    {
                        {
                            // Download
                            PatchFileList.PatchFileInfo[] updateItems = updateStatusArr.Select(x => x.PatchFileInfo).ToArray();
                            long requireByteForUpdate = updateItems.Sum(x => x.Bytes);
                            long occupiedByte = updateStatusArr.Sum(x => x.OccupiedByte);
                            long needFreeStorageBytes = requireByteForUpdate - occupiedByte;
                            if (!await _option.EventReceiver.OnIsEnoughStorageSpace(needFreeStorageBytes))
                            {
                                return new PatchManagerException(E_EXCEPTION_KIND.ERR_LACK_OF_STORAGE, "needFreeStorageBytes");
                            }

                            Directory.CreateDirectory(patchDir);
#if UNITY_IOS
                            UnityEngine.iOS.Device.SetNoBackupFlag(patchDir);
#endif // UNITY_IOS
                            InternalConcurrentDownloader.Option opt = new InternalConcurrentDownloader.Option
                            {
                                PatchDirectory = patchDir,
                                ConcurrentWebRequestMax = _option.ConcurrentWebRequestMax,
                                PatchItemByteMax = nextPatchFileList.TotalBytes,
                                PatchItemMax = nextPatchFileList.Dic.Count,
                                RemoteURL_Parent = $"{RemoteURL_Base}/{RemoteURL_SubPath}/{patchBuildVersion}",
                                EventReceiver = _option.EventReceiver,
                            };

                            using (InternalConcurrentDownloader concurrentDownloader = InternalConcurrentDownloader.DownloadAll(opt, updateItems))
                            {
                                Exception? exOrNull = await concurrentDownloader;
                                if (exOrNull != null)
                                {
                                    return exOrNull!;
                                }
                            }
                        }

                        {
                            // Validate
                            _option.EventReceiver.OnStateChanged(E_PATCH_STATE.PATCHFILELIST_VALIDATE);
                            List<PatchFileListDifference.PatchStatus>? validatePatchStatusListOrNull = await PatchFileListDifference.DifferenceSetOrNull(currPatchFileListOrNull, nextPatchFileList, patchDir, cancellationToken);
                            if (validatePatchStatusListOrNull == null)
                            {
                                return new PatchManagerException(E_EXCEPTION_KIND.ERR_SYSTEM_EXCEPTION, "Internal Exception: validatePatchStatusListOrNull == null");
                            }
                            List<PatchFileListDifference.PatchStatus> validatePatchStatusList = validatePatchStatusListOrNull!;
                            validatePatchStatusList.RemoveAll(x => x.State == PatchFileListDifference.PatchStatus.E_STATE.SKIP);
                            if (validatePatchStatusList.Count != 0)
                            {
                                return new PatchManagerException(E_EXCEPTION_KIND.ERR_FAIL_TO_VALIDATE_PATCHFILES, $"validatePatchStatusList.Count != 0 | validatePatchStatusList.Count:{validatePatchStatusList.Count}");
                            }
                        }
                    }
                }
            }

            {
                _option.EventReceiver.OnStateChanged(E_PATCH_STATE.PATCHFILELIST_FINALIZE);
                string json = nextPatchFileList.ToJson();
                try
                {
                    await File.WriteAllTextAsync(currPatchFileListFpath, json, cancellationToken);
                    return null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            }
        }
    }
}