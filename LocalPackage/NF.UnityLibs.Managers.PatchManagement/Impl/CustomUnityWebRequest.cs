using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace NF.UnityLibs.Managers.PatchManagement.Impl
{
    internal sealed class CustomUnityWebRequest
    {
        public static async Task<(string, Exception? ex)> Get(string url, int timeoutSecond = 60)
        {
            try
            {
                using (UnityWebRequest uwr = UnityWebRequest.Get(url))
                {
                    uwr.timeout = timeoutSecond;
                    UnityWebRequestAsyncOperation op = uwr.SendWebRequest();
                    await op;

                    if (uwr.result != UnityWebRequest.Result.Success)
                    {
                        return (string.Empty, new PatchManagerException(E_EXCEPTION_KIND.ERR_WEBREQUEST_FAIL, $"uwr.result != UnityWebRequest.Result.Success | url: {url} | uwr.result: {uwr.result} | uwr.error: {uwr.error}"));
                    }

                    string str = uwr.downloadHandler.text;
                    if (string.IsNullOrEmpty(str))
                    {
                        return (string.Empty, new PatchManagerException(E_EXCEPTION_KIND.ERR_WEBREQUEST_TXT_IS_EMPTY, $"uwr.downloadHandler.text is empty | url: {url}"));
                    }

                    return (str, null);
                }
            }
            catch (Exception ex)
            {
                return (string.Empty, ex);
            }
        }

        public static async Task<(T?, Exception? ex)> Get<T>(string url, int timeoutSecond = 60) where T : class
        {
            (string str, Exception? exOrNull) = await Get(url, timeoutSecond);
            if (exOrNull != null)
            {
                return (null, exOrNull);
            }

            try
            {
                T? tOrNull = JsonConvert.DeserializeObject<T>(str);
                return (tOrNull, null);
            }
            catch (System.Exception ex)
            {
#if UNITY_5_3_OR_NEWER
                UnityEngine.Debug.LogException(ex);
#endif // UNITY_5_3_OR_NEWER
                return (null, ex);
            }
        }
    }

}