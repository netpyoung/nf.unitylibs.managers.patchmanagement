using NF.UnityLibs.Managers.PatchManagement;
using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Logging;
using Unity.Logging.Sinks;
using UnityEngine;
using UnityEngine.UI;
using Logger = Unity.Logging.Logger;

public sealed class NewMonoBehaviourScript : MonoBehaviour, IPatchManagerEventReceiver
{
    public Slider _slider_Total;
    public TextMeshProUGUI _txt_Total;
    public Slider _slider_0;
    public Slider _slider_1;
    public Slider _slider_2;
    public Slider _slider_3;
    public Slider _slider_4;
    Slider[] _sliders = new Slider[5];

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async private Task Start()
    {
        _sliders[0] = _slider_0;
        _sliders[1] = _slider_1;
        _sliders[2] = _slider_2;
        _sliders[3] = _slider_3;
        _sliders[4] = _slider_4;

        Log.Logger = new Logger(
            new LoggerConfig()
                .CaptureStacktrace()
                .MinimumLevel.Verbose()
                .WriteTo.UnityEditorConsole()
        // .SyncMode.FullSync()
        );

        (PatchManager? patchManagerOrNull, Exception? builderExOrNull) =
            PatchManagerBuilder
            .FromRemote("http://127.0.0.1:9000", "bucket-a/prefix-a")
            .ToPersistantPrefix("A")
            .WithConcurrentWebRequestMax(5)
            .EventRecieveWith(this)
            .Build();
        PatchManager patchManager = patchManagerOrNull!;
        Exception? exOrNull = await patchManager.FromCurrentAppVersion();
        if (exOrNull != null)
        {
            Debug.LogException(exOrNull, this);
            return;
        }
        Debug.Log("!!!!!!!!!!!!");
    }

    #region IPatchManagerEventReceiver
    public Task<bool> OnIsEnoughStorageSpace(long needFreeStorageBytes)
    {
        return Task.FromResult(true);
    }

    public void OnProgressFileInfo(ProgressFileInfo info)
    {
        if (info.ProgressInFileDownload == 1)
        {
            Debug.LogWarning(info.PatchFileInfo.Name);
        }
        _sliders[info.ConcurrentIndex].value = info.ProgressInFileDownload;
    }

    public void OnProgressTotal(float progressTotal, long bytesDownloadedPerSecond)
    {
        _slider_Total.value = progressTotal;
        _txt_Total.text = $"{bytesDownloadedPerSecond.ToSize(MyExtension.SizeUnits.MB)}Mb/s";
    }
    #endregion IPatchManagerEventReceiver
}

public static class MyExtension
{
    public enum SizeUnits
    {
        Byte, KB, MB, GB, TB, PB, EB, ZB, YB
    }

    public static string ToSize(this Int64 value, SizeUnits unit)
    {
        return (value / (double)Math.Pow(1024, (Int64)unit)).ToString("0.00");
    }
}