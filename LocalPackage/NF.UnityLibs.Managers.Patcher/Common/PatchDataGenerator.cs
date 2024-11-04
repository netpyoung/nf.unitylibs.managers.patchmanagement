using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading.Tasks;

namespace NF.UnityLibs.Managers.Patcher.Common
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
			PatchVersion x = PatchVersion.Dummy();
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

			ConcurrentBag<PatchFileList.PatchFileInfo> cb = new ConcurrentBag<PatchFileList.PatchFileInfo>();
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
					using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, bytes))
					using (MemoryMappedViewStream stream = mmf.CreateViewStream(0, bytes, MemoryMappedFileAccess.Read))
					{
						uint checksum = CRC32.Compute(stream);
						PatchFileList.PatchFileInfo patchFileInfo = new PatchFileList.PatchFileInfo(filename, bytes, checksum);
						cb.Add(patchFileInfo);
					}
				}));
			}

			Task.WhenAll(tasks).Wait();
			Dictionary<string, PatchFileList.PatchFileInfo> dic = cb.ToDictionary(x => x.Name, x => x);
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