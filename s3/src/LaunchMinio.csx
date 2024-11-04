#!/usr/bin/env dotnet-script

#nullable enable

// https://github.com/settings/billing/summary => Git LFS Data
// 100MB => LFS
// https://docs.github.com/ko/billing/using-the-billing-platform/about-billing-on-github#included-amounts-by-plan
// https://docs.github.com/ko/enterprise-cloud@latest/repositories/working-with-files/managing-large-files/about-git-large-file-storage#git-%EB%8C%80%EC%9A%A9%EB%9F%89-%ED%8C%8C%EC%9D%BC-%EC%8A%A4%ED%86%A0%EB%A6%AC%EC%A7%80
// 파일당 한도 - GitHub Free	2GB 
// https://docs.github.com/ko/billing/managing-billing-for-your-products/managing-billing-for-git-large-file-storage/about-billing-for-git-large-file-storage#git-%EB%8C%80%EC%9A%A9%EB%9F%89-%ED%8C%8C%EC%9D%BC-%EC%8A%A4%ED%86%A0%EB%A6%AC%EC%A7%80
// Git 대용량 파일 스토리지를 사용하는 모든 계정은 무료 스토리지의 1GiB 및 매월 무료 대역폭 1GiB를 받습니다.

using System.Runtime.CompilerServices;

public static string GetScriptPath([CallerFilePath] string? path = null) => path!;
public static string GetScriptFolder([CallerFilePath] string? path = null) => Path.GetDirectoryName(path)!;

string currDir = GetScriptFolder();
string storageDir = Path.Combine(currDir, "../minio_storage");
Console.WriteLine($"storageDir: {storageDir}");

Environment.SetEnvironmentVariable("MINIO_ROOT_USER", "minioadmin");     // default: minioadmin | at least 3
Environment.SetEnvironmentVariable("MINIO_ROOT_PASSWORD", "minioadmin"); // default: minioadmin | at least 8

ProcessStartInfo psi = new ProcessStartInfo
{
    FileName = "minio",
    Arguments = $"server {storageDir} --address :9000 --console-address :9001",
    UseShellExecute = false,
};

Console.WriteLine($"{psi.FileName} {psi.Arguments}");
using (Process process = Process.Start(psi)!)
{
    Process.Start("explorer", "http://127.0.0.1:9001/login");
    process.WaitForExit();
}

Console.WriteLine("END");


// http://127.0.0.1:9001/login