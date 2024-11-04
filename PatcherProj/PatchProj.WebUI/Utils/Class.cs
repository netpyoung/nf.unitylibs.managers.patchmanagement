using NF.UnityLibs.Managers.Patcher.Common;
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

		public Class()
		{
			string credential_fpath = Path.Combine(Directory.GetCurrentDirectory(), "../../s3/s3.ini");
			S3UploaderBuilder builder = S3UploaderBuilder
				.FromCredentialFpath(credential_fpath)
				.UseURL("http://127.0.0.1:9000");
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
			_uploader.Upload("bucket-a", string.Empty, fpath);
		}

		public async Task<(bool, PatchVersion patchVersion)> TryGetPatchVersion()
		{
			string bucketName = "bucket-a";
			string key = "PatchVersion.json";
			string json = await _uploader.GetTextAsync(bucketName, key);
			if (!PatchVersion.TryFromJson(json, out PatchVersion patchVersion))
			{
				return (false, PatchVersion.Dummy());
			}
			return (true, patchVersion);
		}
	}

}
