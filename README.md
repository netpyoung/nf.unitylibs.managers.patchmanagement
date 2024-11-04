# nf.unitylibs.managers.patcher

- [repo](https://github.com/netpyoung/nf.unitylibs.managers.patcher/)

## upm

- <https://docs.unity3d.com/Manual/upm-ui-giturl.html>

```
https://github.com/netpyoung/nf.unitylibs.managers.patcher.git?path=LocalPackage
```

## 구조

``` tree
/bucket-a/prefix-a
|- PatchVersion.json
|- 1/
|  |- PatchFileList.json
|  |- blabla
|  |- ...
|- 2/
|  |- PatchFileList.json
|  |- blabla
|  |- ...
```

``` json
// PatchVersion.json
{
  "latest": 2,
  
  "1.0.0" : 1,
  "1.1.0" : 2,
  ...
}
```

``` json
// PatchFileList.json
{
  "Version": 0,
  "Dic": {
    "blabla": {
      "Name": "blabla",
      "Bytes": 35991506,
      "Checksum": 2456101181
    },
	...
  }
}
```