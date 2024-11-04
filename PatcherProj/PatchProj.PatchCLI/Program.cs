using NF.UnityLibs.Managers.Patcher.Common;
using System;
using System.Diagnostics;

namespace PatchProj.PatchCLI
{
	public class Program
	{
		public static void Main()
		{
			Console.WriteLine("Hello, World!");

			string patchSrcDir = "D:\\@NF\\nf.unitylibs.managers.patcher\\s3\\test-src";
			int patchNumber = 0;
			Stopwatch sw = new Stopwatch();
			sw.Start();
			string? x = PatchDataGenerator.CreatePatchFileListJson(patchNumber, patchSrcDir);
			sw.Stop();
			Console.WriteLine($"stop: {sw.ElapsedMilliseconds} ms");
			Console.WriteLine($"x: {x}");
			PatchDataGenerator.CreateDummyPatchVersionJson(patchSrcDir);
		}
	}
}
