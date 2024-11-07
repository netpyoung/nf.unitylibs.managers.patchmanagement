using Newtonsoft.Json;
using System.Collections.Generic;

namespace NF.UnityLibs.Managers.PatchManagement.Common
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
    }
}