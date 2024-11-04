using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace NF.UnityLibs.Managers.Patcher.Common
{
#if UNITY_5_3_OR_NEWER
    [UnityEngine.Scripting.Preserve]
#endif // UNITY_5_3_OR_NEWER
    [JsonObject]
    public sealed class PatchFileList
    {
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Scripting.Preserve]
#endif // UNITY_5_3_OR_NEWER
        [JsonObject]
        public sealed class PatchFileInfo
        {
            [JsonProperty]
            public string Name { get; }

            [JsonProperty]
            public long Bytes { get; }

            [JsonProperty]
            public uint Checksum { get; }

#if UNITY_5_3_OR_NEWER
            [UnityEngine.Scripting.Preserve]
#endif // UNITY_5_3_OR_NEWER
            [JsonConstructor]
            public PatchFileInfo(string name, long bytes, uint checksum)
            {
                Name = name;
                Checksum = checksum;
                Bytes = bytes;
            }

            public override string ToString()
            {
                return $"<PatchFileInfo| {Name} / bytes: {Bytes} / md5: {Checksum}>";
            }
        }

        [JsonProperty]
        public int Version { get; }

        [JsonProperty]
        public Dictionary<string, PatchFileInfo> Dic { get; }

        [JsonProperty]
        public long TotalBytes { get; private set; }

#if UNITY_5_3_OR_NEWER
        [UnityEngine.Scripting.Preserve]
#endif // UNITY_5_3_OR_NEWER
        [JsonConstructor]
        public PatchFileList(int version, Dictionary<string, PatchFileInfo> dic, long totalBytes)
        {
            Version = version;
            Dic = dic;
            TotalBytes = totalBytes;
        }

        public PatchFileList(int version, int count)
        {
            Version = version;
            Dic = new Dictionary<string, PatchFileInfo>(count);
        }

        public long UpdateTotalBytes()
        {
            long totalBytes = Dic.Values.Sum(x => x.Bytes);
            TotalBytes = totalBytes;
            return totalBytes;
        }

        public string ToJson()
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            return json;
        }

        public PatchFileList Clone()
        {
            PatchFileList ret = new PatchFileList(Version, Dic.Count);
            foreach (KeyValuePair<string, PatchFileInfo> kv in Dic)
            {
                ret.Dic.Add(kv.Key, kv.Value);
            }

            ret.UpdateTotalBytes();
            return ret;
        }
    }
}