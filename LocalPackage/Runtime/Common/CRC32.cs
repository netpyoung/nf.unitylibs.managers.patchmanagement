using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NF.UnityLibs.Managers.PatchManagement.Common
{
    public static class CRC32
    {
        private static readonly uint[] CRC32_TABLE = GenerateCrc32Table();
        private const int BUFFER_SIZE = 4096;
        private const uint POLY_NOMIAL = 0xEDB88320;

        public static uint ComputeFromStr(string str)
        {
            return ComputeFromSpan(Encoding.UTF8.GetBytes(str).AsSpan());
        }

        public static uint ComputeFromSpan(ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in data)
            {
                byte tableIndex = (byte)((crc ^ b) & 0xFF);
                crc = CRC32_TABLE[tableIndex] ^ (crc >> 8);
            }

            return ~crc;
        }

        public static async Task<uint> ComputeFromStreamAsync(Stream stream, CancellationToken cancellationToken)
        {
            uint crc = 0xFFFFFFFF;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BUFFER_SIZE);
            try
            {
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, 0, BUFFER_SIZE)) > 0)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return 0;
                    }

                    for (int i = 0; i < bytesRead; i++)
                    {
                        byte tableIndex = (byte)((crc ^ buffer[i]) & 0xFF);
                        crc = CRC32_TABLE[tableIndex] ^ (crc >> 8);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return ~crc;
        }

        public static async Task<uint> ComputeFromFpathAsync(string fpath, CancellationToken cancellationToken = default)
        {
            try
            {
                long bytes = new FileInfo(fpath).Length;
                if (bytes == 0)
                {
                    return 0;
                }

                // NOTE(pyoung): commented MemoryMappedFile.
                //using System.IO.MemoryMappedFiles;
                //using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(fpath, FileMode.Open, null, bytes))
                //using (MemoryMappedViewStream stream = mmf.CreateViewStream(0, bytes, MemoryMappedFileAccess.Read))
                using (FileStream stream = File.OpenRead(fpath))
                {
                    return await ComputeFromStreamAsync(stream, cancellationToken);
                }
            }
#if UNITY_5_3_OR_NEWER && NF_PATCHMANAGEMENT_LOG_ENABLED
            catch (IOException ex)
            {
                UnityEngine.Debug.LogException(ex);
                return 0;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                return 0;
            }
#endif // UNITY_5_3_OR_NEWER && NF_PATCHMANAGEMENT_LOG_ENABLED
            catch
            {
                return 0;
            }
        }

        private static uint[] GenerateCrc32Table()
        {
            uint[] table = new uint[256];

            for (uint i = 0; i < table.Length; i++)
            {
                uint crc = i;

                for (int j = 0; j < 8; j++)
                {
                    crc = (crc >> 1) ^ ((crc & 1) * POLY_NOMIAL);
                }
                table[i] = crc;
            }

            return table;
        }
    }
}