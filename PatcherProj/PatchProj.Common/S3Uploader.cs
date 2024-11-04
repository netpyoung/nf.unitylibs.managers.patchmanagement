using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace PatchProj.Common
{
	public class S3Uploader : IDisposable
	{
		private readonly TransferUtility _transfer_utility;
		private int _currPercent = 0;

		public AmazonS3Client S3Client { get; private set; }

		internal S3Uploader(S3UploaderBuilder builder, AmazonS3Config config)
		{
			S3Client = new AmazonS3Client(builder._aws_access_key_id, builder._aws_secret_access_key, config);
			_transfer_utility = new TransferUtility(S3Client);
		}

		public async Task UploadDirectoryAsync(string bucketName, string prefix, string uploadDirectoryFPath)
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(uploadDirectoryFPath);
			await UploadDirectoryAsync(bucketName, prefix, directoryInfo);
		}

		public async Task UploadDirectoryAsync(string bucket, string prefix, DirectoryInfo di)
		{
			_currPercent = 0;

			TransferUtilityUploadDirectoryRequest request = new TransferUtilityUploadDirectoryRequest
			{
				BucketName = bucket,
				KeyPrefix = prefix,
				Directory = di.FullName,
				UploadFilesConcurrently = true,
				CannedACL = S3CannedACL.PublicRead,
				SearchOption = SearchOption.AllDirectories,
			};
			request.UploadDirectoryFileRequestEvent += Request_UploadDirectoryFileRequestEvent;
			request.UploadDirectoryProgressEvent += OnProgress;

			try
			{
				await _transfer_utility.UploadDirectoryAsync(request);
			}
			catch (AmazonS3Exception ex)
			{
				Console.Error.WriteLine($"S3 error occurred during upload: {ex.Message}");
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex);
			}
		}

		private void OnProgress(object? sender, UploadDirectoryProgressArgs e)
		{
			if (e.TransferredBytes > 0)
			{
				int percentDone = (int)((float)e.TransferredBytes / e.TotalBytes * 100);
				if (percentDone != _currPercent)
				{
					Console.WriteLine($"Progress: {percentDone}% ({e.TransferredBytes}/{e.TotalBytes} bytes)");
					_currPercent = percentDone;
				}
			}
		}

		private void Request_UploadDirectoryFileRequestEvent(object? sender, UploadDirectoryFileRequestArgs e)
		{
			e.UploadRequest.Headers.ContentMD5 = GetMD5(e.UploadRequest.FilePath);
			// Console.WriteLine($"q: {e.UploadRequest.FilePath} => {e.UploadRequest.Key}");
		}

		public static string GetMD5(string fpath)
		{
			using (FileStream stream = new FileStream(fpath, FileMode.Open, FileAccess.Read))
			{
#pragma warning disable CA5351
				using (MD5 md5 = MD5.Create())
#pragma warning restore CA5351
				{
					byte[] md5bytes = md5.ComputeHash(stream);
					return Convert.ToBase64String(md5bytes);
				}
			}
		}

		public void Upload(string bucket, string prefix, params string[] fpaths)
		{
			if (fpaths.Length == 0)
			{
				return;
			}

			foreach (string fpath in fpaths)
			{
				string filename = Path.GetFileName(fpath);
				string key = Path.Combine(prefix, filename).Replace("\\", "/");
				TransferUtilityUploadRequest request = new TransferUtilityUploadRequest()
				{
					FilePath = fpath,
					BucketName = bucket,
					Key = key,
					CannedACL = S3CannedACL.PublicRead
				};
				_transfer_utility.Upload(request);
				Console.WriteLine($"q: {fpath} => {key}");
			}
		}

		public async Task<string> GetTextAsync(string bucketName, string Key)
		{
			GetObjectRequest getObjectRequest = new GetObjectRequest
			{
				BucketName = bucketName,
				Key = Key
			};

			using (GetObjectResponse getObjectResponse = await S3Client.GetObjectAsync(getObjectRequest))
			using (StreamReader streamReader = new StreamReader(getObjectResponse.ResponseStream))
			{
				return await streamReader.ReadToEndAsync();
			}
		}

		public void Dispose()
		{
			_transfer_utility.Dispose();
		}
	}
}
