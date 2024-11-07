using Newtonsoft.Json;
using NF.UnityLibs.Managers.Patcher.Common;
using NF.UnityLibs.Managers.Patcher.Impl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace NF.UnityLibs.Managers.Patcher
{
    // remoteURL_Full    : https://helloworld.com/blabla/hello/a.txt
    // remoteURL_Parent  : https://helloworld.com/blabla/hello
    // remoteURL_Base    : https://helloworld.com
    // remoteURL_SubPath : blabla/hello
    // remoteURL_Filename: a.txt

    [Serializable]
    public sealed class PatchDataDownloader
    {
        [SerializeField]
        private string _remoteURL_Base = string.Empty;

        [SerializeField]
        private string _remoteURL_SubPath = string.Empty;

        [SerializeField]
        private string _devicePrefix = string.Empty;

        [SerializeField]
        private int _version;

        public string RemoteURL_Base
        {
            get { return _remoteURL_Base; }
            private set { _remoteURL_Base = value; }
        }

        public string RemoteURL_SubPath
        {
            get { return _remoteURL_SubPath; }
            private set { _remoteURL_SubPath = value; }
        }

        public string DevicePrefix
        {
            get { return _devicePrefix; }
            private set { _devicePrefix = value; }
        }

        public int Version
        {
            get { return _version; }
            set { _version = value; }
        }

        public void Init(string remoteURL_Base, string remoteURL_SubPath)
        {
            _remoteURL_Base = remoteURL_Base.TrimEnd('/');
            _remoteURL_SubPath = remoteURL_SubPath;
            _devicePrefix = "A";
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
                    return new PatcherException($"failed to get version from patchVersion\n{patchVersion.ToJson()}");
                }
            }
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

            string currPatchFileListFpath = $"{Application.persistentDataPath}/{DevicePrefix}/{nameof(PatchFileList)}.json";
            PatchFileList? currPatchFileListOrNull = GetCurrPatchFileListOrNull(currPatchFileListFpath);
            if (currPatchFileListOrNull != null)
            {
                PatchFileList currPatchFileList = currPatchFileListOrNull!;
                if (currPatchFileList.Version == patchBuildVersion)
                {
                    return null;
                }
            }

            string patchDir = $"{Application.persistentDataPath}/{DevicePrefix}";
            List<PatchFileListDifference.PatchStatus>? patchStatusListOrNull = await PatchFileListDifference.DifferenceSetOrNull(currPatchFileListOrNull, nextPatchFileList, patchDir);
            if (patchStatusListOrNull == null)
            {
                return new PatcherException("Internal Exception: patchStatusListOrNull == null");
            }

            {
                List<PatchFileListDifference.PatchStatus> patchStatusList = patchStatusListOrNull!;

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
                    PatchFileList.PatchFileInfo[] updateItems = patchStatusList.Where(x => x.State == PatchFileListDifference.PatchStatus.E_STATE.UPDATE).Select(x => x.PatchFileInfo).ToArray();
                    if (updateItems.Length != 0)
                    {
                        Directory.CreateDirectory(patchDir);
#if UNITY_IOS
                        UnityEngine.iOS.Device.SetNoBackupFlag(patchDir);
#endif // UNITY_IOS
                        InternalConcurrentDownloader.Option opt = new InternalConcurrentDownloader.Option
                        {
                            PatchDirectory = patchDir,
                            ConcurrentWebRequestMax = 5,
                            PatchItemByteMax = nextPatchFileList.TotalBytes,
                            PatchItemMax = nextPatchFileList.Dic.Count,
                            RemoteURL_Parent = $"{RemoteURL_Base}/{RemoteURL_SubPath}/{patchBuildVersion}"
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
                List<PatchFileListDifference.PatchStatus>? validatePatchStatusListOrNull = await PatchFileListDifference.DifferenceSetOrNull(currPatchFileListOrNull, nextPatchFileList, patchDir);
                if (validatePatchStatusListOrNull == null)
                {
                    return new PatcherException("Internal Exception: validatePatchStatusListOrNull == null");
                }
                List<PatchFileListDifference.PatchStatus> validatePatchStatusList = validatePatchStatusListOrNull!;
                validatePatchStatusList.RemoveAll(x => x.State == PatchFileListDifference.PatchStatus.E_STATE.SKIP);
                if (validatePatchStatusList.Count != 0)
                {
                    return new PatcherException($"validatePatchStatusList.Count != 0 | validatePatchStatusList.Count:{validatePatchStatusList.Count}");
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