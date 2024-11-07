using NF.UnityLibs.Managers.PatchManagement.Common;
using PatchProj.Common;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PatchProj.WebUI.Utils
{
	public class Class : IDisposable
	{
		private S3Uploader _uploader;
		public bool IsLoading { get; private set; } = true;
		Config _config;

		public Class(Config config)
		{
			_config = config;
			string credentialPath = config.CredentialPath;
			string url = config.URL;
			string credentialFpath = Path.Combine(Directory.GetCurrentDirectory(), credentialPath);
			S3UploaderBuilder builder = S3UploaderBuilder
				.FromCredentialFpath(credentialFpath)
				.UseURL(url);
			S3Uploader uploader = builder.Build();
			_uploader = uploader;
		}


		public void Dispose()
		{
			_uploader.Dispose();
		}

		public void Upload(PatchVersion patchVersion)
		{
			string fpath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "PatchVersion.json");
			Directory.CreateDirectory(Path.GetDirectoryName(fpath)!);

			string jsonStr = patchVersion.ToJson();
			File.WriteAllText(fpath, jsonStr);
			_uploader.Upload("bucket-a", "prefix-a", fpath);
		}

		public async Task<(bool, PatchVersion patchVersion)> TryGetPatchVersion()
		{
			string bucketName = "bucket-a";
			string key = "prefix-a/PatchVersion.json";
			string json = await _uploader.GetTextAsync(bucketName, key);
			if (!PatchVersion.TryFromJson(json, out PatchVersion patchVersion))
			{
				return (false, PatchVersion.Dummy());
			}
			return (true, patchVersion);
		}

		public async Task<(bool, PatchFileList)> GetPatchFileList(int v)
		{
			string bucketName = "bucket-a";
			string key = $"prefix-a/{v}/PatchFileList.json";

			string json = await _uploader.GetTextAsync(bucketName, key);
			if (!PatchFileList.TryFromJson(json, out PatchFileList patchFileList))
			{
				return (false, PatchFileList.Dummy());
			}
			return (true, patchFileList);
		}
	}

}
