using Newtonsoft.Json;
using System.Collections.Generic;

namespace NF.UnityLibs.Managers.Patcher.Common
{
#if UNITY_5_3_OR_NEWER
    [UnityEngine.Scripting.Preserve]
#endif // UNITY_5_3_OR_NEWER
    [JsonDictionary]
    public sealed class PatchVersion : Dictionary<string, int>
    {
        public string ToJson()
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            return json;
        }

        public static PatchVersion Dummy()
        {
            PatchVersion ret = new PatchVersion();
            ret["latest"] = 0;
            return ret;
        }

        public static bool TryFromJson(string json, out PatchVersion result)
        {
            try
            {
                PatchVersion? x = JsonConvert.DeserializeObject<PatchVersion>(json);
                result = x!;
                return true;
            }
#if UNITY_5_3_OR_NEWER
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
#else
            catch
            {
#endif // UNITY_5_3_OR_NEWER
                result = Dummy();
                return false;
            }
        }
    }
}