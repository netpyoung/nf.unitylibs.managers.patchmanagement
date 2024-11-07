using System.Diagnostics;
using System.IO;
using NF.UnityLibs.Managers.PatchManagement.Common;
using Unity.Logging;
using Unity.Logging.Sinks;
using UnityEditor;
using UnityEngine;
using Logger = Unity.Logging.Logger;

public class NewEmptyCSharpScript
{
    [MenuItem("@A/Hello")]
    public static void Hello()
    {
        Log.Logger = new Logger(
            new LoggerConfig()
                .WriteTo.UnityEditorConsole()
                .SyncMode.FullSync()
        );

        string patchSrcDir = Path.Combine(Application.dataPath, "../../s3/test-src/");
        int patchNumber = 0;
        Stopwatch sw = new Stopwatch();
        sw.Start();
        string x = PatchDataGenerator.CreatePatchFileListJson(patchNumber, patchSrcDir);
        sw.Stop();
        Log.Info($"stop: {sw.ElapsedMilliseconds} ms");
    }
}