#nullable enable
#load "Utils/S3Uploader.csx"

using System.Runtime.CompilerServices;

public static string GetScriptFolder([CallerFilePath] string? path = null) => Path.GetDirectoryName(path)!;

string baseDir = GetScriptFolder();
string uploadDirectoryFpath = Path.GetFullPath(Path.Combine(baseDir, "../test-src"));
string credentialFpath = Path.GetFullPath(Path.Combine(baseDir, "../s3.ini"));

try
{

    S3UploaderBuilder builder = S3UploaderBuilder
        .FromCredentialFpath(credentialFpath)
        .UseURL("http://127.0.0.1:9000");
    using (S3Uploader uploader = builder.Build())
    {
        string bucketName = "bucket-a";
        string prefix = "prefix-a/0";

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        {
            await uploader.UploadDirectoryAsync(bucketName, prefix, uploadDirectoryFpath);
        }
        stopwatch.Stop();
        Console.WriteLine($"time : {stopwatch.ElapsedMilliseconds} ms");
    }

}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
}
Console.ReadKey();
