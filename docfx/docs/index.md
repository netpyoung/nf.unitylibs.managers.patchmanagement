# Introduction

- [repo](https://github.com/netpyoung/nf.unitylibs.managers.patchmanagement/)

## upm

- <https://docs.unity3d.com/Manual/upm-ui-giturl.html>

```
https://github.com/netpyoung/nf.unitylibs.managers.patchmanagement.git?path=LocalPackage
```

## example

``` cs
(PatchManager? patchManagerOrNull, Exception? builderExOrNull) = PatchManagerBuilder
    .FromRemote("http://127.0.0.1:9000", "bucket-a/prefix-a")
    .ToPersistantPrefix("A")
    .WithConcurrentWebRequestMax(5)
    .EventRecieveWith(this)
    .Build();
if (builderExOrNull is Exception ex)
{
    Debug.LogException(ex);
    return;
}

using (PatchManager patchManager = patchManagerOrNull!)
{
    Exception? exOrNull = await patchManager.FromCurrentAppVersion();
    if (exOrNull != null)
    {
        Debug.LogException(exOrNull, this);
        return;
    }
    Debug.Log("!!!!!!!!!!!!");
}
```