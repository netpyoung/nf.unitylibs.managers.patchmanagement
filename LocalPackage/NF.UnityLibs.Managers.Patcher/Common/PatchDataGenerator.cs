using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NF.UnityLibs.Managers.PatchManagement.Common
{
    public static class PatchDataGenerator
    {
        private static readonly HashSet<string> _DEFAULT_FILTER_SET = new HashSet<string>
        {
            $"{nameof(PatchFileList)}.json".ToLower(),
            "buildlogtep.json".ToLower(),
            "Android_manifest.manifest".ToLower(),
            "iOS_manifest.manifest".ToLower(),
        };

        public static string CreateDummyPatchVersionJson(string destDir)
        {
            PatchVersion x = new PatchVersion();
            x["latest"] = 0;
            string json = x.ToJson();
            File.WriteAllText($"{destDir}/{nameof(PatchVersion)}.json", json);
            return json;
        }

        public static string? CreatePatchFileListJson(int patchNumber, string patchSrcDir, HashSet<string>? filterOrNull = null)
        {
            if (string.IsNullOrEmpty(patchSrcDir))
            {
#if UNITY_5_3_OR_NEWER
                UnityEngine.Debug.LogError($"string.IsNullOrEmpty - patchSrcDir: {patchSrcDir}");
#endif // UNITY_5_3_OR_NEWER
                return null;
            }

            if (!Directory.Exists(patchSrcDir))
            {
#if UNITY_5_3_OR_NEWER
                UnityEngine.Debug.LogError($"!Directory.Exists - patchSrcDir: {patchSrcDir}");
#endif // UNITY_5_3_OR_NEWER
                return null;
            }

            HashSet<string> filter;
            if (filterOrNull == null)
            {
                filter = _DEFAULT_FILTER_SET;
            }
            else
            {
                filter = filterOrNull!;
            }

            ConcurrentQueue<PatchFileList.PatchFileInfo> cq = new ConcurrentQueue<PatchFileList.PatchFileInfo>();
            string[] paths = Directory.GetFiles(patchSrcDir, "*", SearchOption.TopDirectoryOnly);
            List<Task> tasks = new List<Task>(paths.Length);
            foreach (string path in paths.Select(x => x.Replace('\\', '/')))
            {
                string filename = Path.GetFileName(path).ToLower();
                if (filter.Contains(filename))
                {
                    continue;
                }

                long bytes = new FileInfo(path).Length;
                tasks.Add(Task.Run(() =>
                {
                    uint checksum = CRC32.ComputeFromFpath(path);
                    PatchFileList.PatchFileInfo patchFileInfo = new PatchFileList.PatchFileInfo(filename, bytes, checksum);
                    cq.Enqueue(patchFileInfo);
                }));
            }

            Task.WhenAll(tasks).Wait();
            Dictionary<string, PatchFileList.PatchFileInfo> dic = cq.ToDictionary(x => x.Name, x => x);
            long totalBytes = dic.Sum(x => x.Value.Bytes);
            PatchFileList patchFileList = new PatchFileList(patchNumber, dic, totalBytes);
            string json = patchFileList.ToJson();

            string outputJsonFpath = $"{patchSrcDir}/{nameof(PatchFileList)}.json";
            string? directoryPath = Path.GetDirectoryName(outputJsonFpath)!;
            Directory.CreateDirectory(directoryPath);

            File.WriteAllText(outputJsonFpath, json);
            return json;
        }
    }
}