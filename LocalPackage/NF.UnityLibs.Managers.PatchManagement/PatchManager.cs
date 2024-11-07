using Newtonsoft.Json;
using NF.UnityLibs.Managers.PatchManagement.Common;
using NF.UnityLibs.Managers.PatchManagement.Impl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public sealed class PatchManager
    {
        public class Option
        {
            public string RemoteURL_Base { get; internal set; } = string.Empty;
            public string RemoteURL_SubPath { get; internal set; } = string.Empty;
            public string DevicePersistentPrefix { get; internal set; } = string.Empty;
            public int ConcurrentWebRequestMax { get; internal set; } = 1;
            public IPatchManagerEventReceiver EventReceiver { get; internal set; } = new DummyPatchManagerEventReceiver();

            public Exception? Validate()
            {
                if (ConcurrentWebRequestMax < 1)
                {
                    return null;
                }
                if (string.IsNullOrEmpty(RemoteURL_Base))
                {
                    return null;
                }

                if (string.IsNullOrEmpty(RemoteURL_SubPath))
                {
                    return null;
                }

                if (string.IsNullOrEmpty(DevicePersistentPrefix))
                {
                    return null;
                }
                return null;
            }
        }

        private Option _option;

        public string RemoteURL_Base { get => _option.RemoteURL_Base; }
        public string RemoteURL_SubPath { get => _option.RemoteURL_SubPath; }
        public string DevicePersistentPrefix { get => _option.DevicePersistentPrefix; }

        internal PatchManager(Option option)
        {
            _option = option;
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

        public Task<Exception?> FromCurrentAppVersion()
        {
            string appVersion = Application.version;
            return FromAppVersion(appVersion);
        }

        public async Task<Exception?> FromAppVersion(string appVersion)
        {
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
                    return new PatchManagerException($"failed to get version from patchVersion\n{patchVersion.ToJson()}");
                }
            }
            Debug.LogWarning($"{nameof(FromPatchBuildVersion)} // patchBuildVersion: {patchBuildVersion}");
            return await FromPatchBuildVersion(patchBuildVersion);
        }

        public async Task<Exception?> FromPatchBuildVersion(int patchBuildVersion)
        {
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
            Debug.LogWarning($"{nameof(FromPatchBuildVersion)} // nextPatchFileList: {nextPatchFileList}");

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

            string patchDir = $"{Application.persistentDataPath}/{DevicePersistentPrefix}";
            List<PatchFileListDifference.PatchStatus>? patchStatusListOrNull = await PatchFileListDifference.DifferenceSetOrNull(currPatchFileListOrNull, nextPatchFileList, patchDir);
            if (patchStatusListOrNull == null)
            {
                return new PatchManagerException("Internal Exception: patchStatusListOrNull == null");
            }

            {
                List<PatchFileListDifference.PatchStatus> patchStatusList = patchStatusListOrNull!;

                {
                    // Skip
                    Debug.LogWarning($"{nameof(FromPatchBuildVersion)} // Skip");
                    patchStatusList.RemoveAll(x => x.State == PatchFileListDifference.PatchStatus.E_STATE.SKIP);
                }
                {
                    // Delete
                    Debug.LogWarning($"{nameof(FromPatchBuildVersion)} // Delete");
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
                    Debug.LogWarning($"{nameof(FromPatchBuildVersion)} // Update");
                    PatchFileListDifference.PatchStatus[] updateStatusArr = patchStatusList.Where(x => x.State == PatchFileListDifference.PatchStatus.E_STATE.UPDATE).ToArray();
                    if (updateStatusArr.Length != 0)
                    {
                        PatchFileList.PatchFileInfo[] updateItems = updateStatusArr.Select(x => x.PatchFileInfo).ToArray();
                        long requireByteForUpdate = updateItems.Sum(x => x.Bytes);
                        long occupiedByte = updateStatusArr.Sum(x => x.OccupiedByte);
                        long needFreeStorageBytes = requireByteForUpdate - occupiedByte;
                        if (!await _option.EventReceiver.OnIsEnoughStorageSpace(needFreeStorageBytes))
                        {
                            return new PatchManagerException("needFreeStorageBytes");
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
                        Exception? exOrNull = await InternalConcurrentDownloader.DownloadAll(opt, updateItems);
                        if (exOrNull != null)
                        {
                            return exOrNull!;
                        }
                    }
                }
            }

            {
                // Validate
                Debug.LogWarning($"{nameof(FromPatchBuildVersion)} // Validate");
                List<PatchFileListDifference.PatchStatus>? validatePatchStatusListOrNull = await PatchFileListDifference.DifferenceSetOrNull(currPatchFileListOrNull, nextPatchFileList, patchDir);
                if (validatePatchStatusListOrNull == null)
                {
                    return new PatchManagerException("Internal Exception: validatePatchStatusListOrNull == null");
                }
                List<PatchFileListDifference.PatchStatus> validatePatchStatusList = validatePatchStatusListOrNull!;
                validatePatchStatusList.RemoveAll(x => x.State == PatchFileListDifference.PatchStatus.E_STATE.SKIP);
                if (validatePatchStatusList.Count != 0)
                {
                    return new PatchManagerException($"validatePatchStatusList.Count != 0 | validatePatchStatusList.Count:{validatePatchStatusList.Count}");
                }
            }

            {
                // Finalize
                string json = nextPatchFileList.ToJson();
                try
                {
                    File.WriteAllText(currPatchFileListFpath, json);
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