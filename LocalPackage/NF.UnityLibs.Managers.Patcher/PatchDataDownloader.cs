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

        public async Task X()
        {
            string appVersion = Application.version;
            int version = 0;
            {
                string url = $"{RemoteURL_Base}/{RemoteURL_SubPath}/{nameof(PatchVersion)}.json";
                (PatchVersion? patchVersionOrNull, Exception? exOrNull) = await CustomUnityWebRequest.Get<PatchVersion>(url);
                if (exOrNull != null)
                {
                    Debug.LogException(exOrNull);
                    return;
                }

                PatchVersion patchVersion = patchVersionOrNull!;
                if (patchVersion.TryGetValue(appVersion, out version))
                {
                }
                else if (patchVersion.TryGetValue("latest", out version))
                {
                }
                else
                {
                    return;
                }
            }

            PatchFileList nextPatchFileList;
            {
                string url = $"{RemoteURL_Base}/{RemoteURL_SubPath}/{version}/{nameof(PatchFileList)}.json";
                (PatchFileList? remotePatchFileListOrNull, Exception? exOrNull) = await CustomUnityWebRequest.Get<PatchFileList>(url);
                if (exOrNull != null)
                {
                    Debug.LogException(exOrNull);
                    return;
                }

                nextPatchFileList = remotePatchFileListOrNull!;
            }

            string currPatchFileListFpath = $"{Application.persistentDataPath}/{DevicePrefix}/{nameof(PatchFileList)}.json";
            PatchFileList? currPatchFileListOrNull = GetCurrPatchFileListOrNull(currPatchFileListFpath);
            if (currPatchFileListOrNull != null)
            {
                PatchFileList currPatchFileList = currPatchFileListOrNull!;
                if (currPatchFileList.Version == version)
                {
                    Debug.Log("HelloWorld");
                    return;
                }
            }

            string patchDir = $"{Application.persistentDataPath}/{DevicePrefix}";
            List<PatchFileListDifference.PatchStatus>? lstOrNull = await PatchFileListDifference.DifferenceSetOrNull(currPatchFileListOrNull, nextPatchFileList, patchDir);
            if (lstOrNull == null)
            {
                Debug.Log("WTF");
                return;
            }

            {
                List<PatchFileListDifference.PatchStatus> lst = lstOrNull!;

                {
                    // Skip
                    lst.RemoveAll(x => x.State == PatchFileListDifference.PatchStatus.E_STATE.SKIP);
                }
                {
                    // Delete
                    PatchFileListDifference.PatchStatus[] items = lst.Where(x => x.State == PatchFileListDifference.PatchStatus.E_STATE.DELETE).ToArray();
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
                            Debug.LogException(ex);
                            return;
                        }
                    }
                }
                {
                    // Update
                    Directory.CreateDirectory(patchDir);
                    PatchFileList.PatchFileInfo[] items = lst.Where(x => x.State == PatchFileListDifference.PatchStatus.E_STATE.UPDATE).Select(x => x.PatchFileInfo).ToArray();
                    {
                        InternalConcurrentDownloader.Option opt = new InternalConcurrentDownloader.Option
                        {
                            PatchDirectory = patchDir,
                            ConcurrentWebRequestMax = 5,
                            PatchItemByteMax = nextPatchFileList.TotalBytes,
                            PatchItemMax = nextPatchFileList.Dic.Count,
                            RemoteURL_Parent = $"{RemoteURL_Base}/{RemoteURL_SubPath}/{version}"
                        };
                        bool isSuccess = await InternalConcurrentDownloader.DownloadAll(opt, items);
                        Debug.Log($"isSuccess: {isSuccess}");
                    }

                    List<PatchFileListDifference.PatchStatus>? againListOrNull = await PatchFileListDifference.DifferenceSetOrNull(currPatchFileListOrNull, nextPatchFileList, patchDir);
                    if (againListOrNull == null)
                    {
                        Debug.Log("WTF");
                        return;
                    }
                    List<PatchFileListDifference.PatchStatus> againList = againListOrNull!;
                    againList.RemoveAll(x => x.State == PatchFileListDifference.PatchStatus.E_STATE.SKIP);
                    if (againList.Count != 0)
                    {
                        Debug.LogError($"againList.Count : {againList.Count}");
                        return;
                    }
                    string json = nextPatchFileList.ToJson();
                    File.WriteAllText(currPatchFileListFpath, json);
                }
            }
        }
    }
}