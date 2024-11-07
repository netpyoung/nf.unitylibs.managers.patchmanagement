using System.Threading.Tasks;

namespace NF.UnityLibs.Managers.PatchManagement
{
    public interface IPatchManagerEventReceiver
    {
        Task<bool> OnIsEnoughStorageSpace(long needFreeStorageBytes);
        void OnProgressFileInfo(ProgressFileInfo info);
        void OnProgressTotal(float progressTotal, long bytesDownloadedPerSecond);

        internal sealed class DummyPatchManagerEventReceiver : IPatchManagerEventReceiver
        {
            public Task<bool> OnIsEnoughStorageSpace(long needFreeStorageBytes)
            {
                return Task.FromResult(true);
            }

            public void OnProgressFileInfo(ProgressFileInfo info)
            {
            }

            public void OnProgressTotal(float progressTotal, long bytesDownloadedPerSecond)
            {
            }
        }
    }
}
