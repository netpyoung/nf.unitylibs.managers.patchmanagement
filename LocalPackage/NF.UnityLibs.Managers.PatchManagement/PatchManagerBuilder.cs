using System;

namespace NF.UnityLibs.Managers.PatchManagement
{
    public sealed class PatchManagerBuilder
    {
        private readonly PatchManager.Option _options = new PatchManager.Option();

        private PatchManagerBuilder() { }

        public static PatchManagerBuilder FromRemote(string url, string bucketPrefix)
        {
            PatchManagerBuilder builder = new PatchManagerBuilder();
            builder._options.RemoteURL_Base = url;
            builder._options.RemoteURL_SubPath = bucketPrefix;
            return builder;
        }

        public PatchManagerBuilder ToPersistantPrefix(string path)
        {
            _options.DevicePersistentPrefix = path;
            return this;
        }

        public PatchManagerBuilder WithConcurrentWebRequestMax(int maxRequests)
        {
            _options.ConcurrentWebRequestMax = maxRequests;
            return this;
        }

        public PatchManagerBuilder EventRecieveWith(IPatchManagerEventReceiver receiver)
        {
            _options.EventReceiver = receiver;
            return this;
        }

        public (PatchManager? patchManagerOrNull, Exception? builderExOrNull) Build()
        {
            Exception? exOrNull = _options.Validate();
            if (exOrNull != null)
            {
                return (null, exOrNull!);
            }
            PatchManager downloader = new PatchManager(_options);
            return (downloader, null);
        }
    }
}
