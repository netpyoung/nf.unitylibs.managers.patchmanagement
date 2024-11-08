using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NF.UnityLibs.Managers.PatchManagement.Common
{
    public sealed class PatchFileListDifference
    {
        public sealed class PatchStatus
        {
            public enum E_STATE
            {
                SKIP = 0,
                UPDATE = 1,
                DELETE = 2,
            }

            public PatchFileList.PatchFileInfo PatchFileInfo { get; private set; }
            public E_STATE State { get; private set; }
            public long OccupiedByte { get; private set; }

            public PatchStatus(PatchFileList.PatchFileInfo patchFileInfo, E_STATE state, long occupiedByte = 0)
            {
                PatchFileInfo = patchFileInfo;
                State = state;
                OccupiedByte = occupiedByte;
            }
        }

        private static async Task<PatchStatus> _GetPatchStatus(KeyValuePair<string, PatchFileList.PatchFileInfo> currKeyValue, PatchFileList nextPatchFileList, string patchFileDir, CancellationToken cancellationToken)
        {
            (string currKey, PatchFileList.PatchFileInfo currValue) = currKeyValue;
            if (!nextPatchFileList.Dic.TryGetValue(currKey, out PatchFileList.PatchFileInfo? nextValueOrNull))
            {
                return new PatchStatus(currValue, PatchStatus.E_STATE.DELETE);
            }

            PatchFileList.PatchFileInfo nextValue = nextValueOrNull!;
            {
                // UPDATE
                string downloadFpath = Path.Combine(patchFileDir, nextValue.Name);
                if (!File.Exists(downloadFpath))
                {
                    return new PatchStatus(nextValue, PatchStatus.E_STATE.UPDATE);
                }

                long occupiedByte = new FileInfo(downloadFpath).Length;
                if (occupiedByte != nextValue.Bytes)
                {
                    return new PatchStatus(nextValue, PatchStatus.E_STATE.UPDATE, occupiedByte);
                }

                uint checksum = await CRC32.ComputeFromFpathAsync(downloadFpath, cancellationToken);
                if (checksum != nextValue.Checksum)
                {
                    return new PatchStatus(nextValue, PatchStatus.E_STATE.UPDATE, occupiedByte);
                }
            }

            return new PatchStatus(nextValue, PatchStatus.E_STATE.SKIP);
        }

        private static int _SortByBytesAscending(PatchStatus x, PatchStatus y)
        {
            return x.PatchFileInfo.Bytes.CompareTo(y.PatchFileInfo.Bytes);
        }

        public async static Task<List<PatchStatus>?> DifferenceSetOrNull(PatchFileList? currPatchFileListOrNull, PatchFileList nextPatchFileList, string patchFileDir, CancellationToken cancellationToken = default)
        {
            PatchFileList currPatchFileList;
            if (currPatchFileListOrNull != null)
            {
                currPatchFileList = currPatchFileListOrNull!;
            }
            else
            {
                currPatchFileList = nextPatchFileList;
            }

            IEnumerable<Task<PatchStatus>> tasks = currPatchFileList.Dic.Select(kv => _GetPatchStatus(kv, nextPatchFileList, patchFileDir, cancellationToken));
            PatchStatus[] statusArr = await Task.WhenAll(tasks);

            List<PatchStatus> ret = statusArr.ToList();
            string[] newAssetNames = nextPatchFileList.Dic.Keys.Except(currPatchFileList.Dic.Keys).ToArray();
            foreach (string newAssetName in newAssetNames)
            {
                PatchFileList.PatchFileInfo info = nextPatchFileList.Dic[newAssetName];
                ret.Add(new PatchStatus(info, PatchStatus.E_STATE.UPDATE));
            }
            ret.Sort(_SortByBytesAscending);
            return ret;
        }
    }
}