using System.Threading.Tasks;

namespace NF.UnityLibs.Managers.PatchManagement
{
    public interface IPatchManagerEventReceiver
    {
        Task<bool> OnIsEnoughStorageSpace(long needFreeStorageBytes);
        void OnProgressFileInfo(ProgressFileInfo info);
        void OnProgressTotal(float progressTotal, long bytesDownloadedPerSecond);
        void OnStateChanged(E_PATCH_STATE state, string debugMessage = "");

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

            public void OnStateChanged(E_PATCH_STATE state, string debugMessage)
            {
            }
        }
    }
}
