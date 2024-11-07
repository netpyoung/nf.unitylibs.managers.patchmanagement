using NF.UnityLibs.Managers.PatchManagement.Common;
using System.Threading.Tasks;

namespace NF.UnityLibs.Managers.PatchManagement
{
    public interface IPatchManagerEventReceiver
    {
        public sealed class ProgressFileInfo
        {
            public PatchFileList.PatchFileInfo PatchFileInfo { get; set; } = new PatchFileList.PatchFileInfo(string.Empty, 0, 0);
            public float ProgressInFileDownload { get; set; }
            public int ConcurrentIndex { get; set; }
            public int ItemCurrIndex { get; set; }
            public int ItemMaxLength { get; set; }
            public long BytesDownloaded { get; set; }

            public override string ToString()
            {
                return $"{ConcurrentIndex} - {PatchFileInfo} - {ProgressInFileDownload}";
            }
        }

        Task<bool> OnIsEnoughStorageSpace(long needFreeStorageBytes);
        void OnProgressFileInfo(ProgressFileInfo info);
        void OnProgressTotal(float progressTotal, long bytesDownloadedPerSecond);
    }

    internal sealed class DummyPatchManagerEventReceiver : IPatchManagerEventReceiver
    {
        public Task<bool> OnIsEnoughStorageSpace(long needFreeStorageBytes)
        {
            return Task.FromResult(true);
        }

        public void OnProgressFileInfo(IPatchManagerEventReceiver.ProgressFileInfo info)
        {
        }

        public void OnProgressTotal(float progressTotal, long bytesDownloadedPerSecond)
        {
        }
    }
}
