using Amazon.S3;
using IniParser;
using IniParser.Model;

namespace PatchProj.Common
{
	public class S3UploaderBuilder
	{
		internal string _aws_access_key_id = string.Empty;
		internal string _aws_secret_access_key = string.Empty;
		internal AmazonS3Config _config = new AmazonS3Config();

		public static S3UploaderBuilder FromCredentialFpath(string credential_fpath)
		{
			S3UploaderBuilder ret = new S3UploaderBuilder();
			FileIniDataParser parser = new FileIniDataParser();
			IniData data = parser.ReadFile(credential_fpath);
			ret._aws_access_key_id = data["default"]["aws_access_key_id"];
			ret._aws_secret_access_key = data["default"]["aws_secret_access_key"];
			return ret;
		}

		public static S3UploaderBuilder FromAccessKey(string aws_access_key_id, string aws_secret_access_key)
		{
			S3UploaderBuilder ret = new S3UploaderBuilder
			{
				_aws_access_key_id = aws_access_key_id,
				_aws_secret_access_key = aws_secret_access_key
			};
			return ret;
		}

		public S3UploaderBuilder UseURL(string serviceURL)
		{
			_config = new AmazonS3Config()
			{
				ServiceURL = serviceURL,
				UseHttp = true,
				ForcePathStyle = true,
			};
			return this;
		}
		public S3UploaderBuilder UseRegion(Amazon.RegionEndpoint endpoint)
		{
			_config = new AmazonS3Config()
			{
				// https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/Amazon/TRegionEndpoint.html
				RegionEndpoint = endpoint,
			};
			return this;
		}

		public S3Uploader Build()
		{
			S3Uploader ret = new S3Uploader(this, _config);
			return ret;
		}
	}
}
