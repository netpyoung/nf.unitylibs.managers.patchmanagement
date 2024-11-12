using NF.UnityLibs.Managers.PatchManagement.Common;

namespace NF.UnityLibs.Managers.PatchManagement
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
            return $"<ProgressFileInfo: {ConcurrentIndex} / {PatchFileInfo} / {ProgressInFileDownload}>";
        }
    }
}
