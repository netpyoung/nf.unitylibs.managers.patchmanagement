using System;
using System.Buffers;
using System.IO;
using System.Text;

namespace NF.UnityLibs.Managers.Patcher.Common
{
    public static class CRC32
    {
        private static readonly uint[] CRC32_TABLE = GenerateCrc32Table();
        private const int BUFFER_SIZE = 4096;
        private const uint POLY_NOMIAL = 0xEDB88320;

        public static uint Compute(string str)
        {
            return Compute(Encoding.UTF8.GetBytes(str).AsSpan());
        }

        public static uint Compute(ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in data)
            {
                byte tableIndex = (byte)((crc ^ b) & 0xFF);
                crc = CRC32_TABLE[tableIndex] ^ (crc >> 8);
            }

            return ~crc;
        }

        public static uint Compute(Stream stream)
        {
            uint crc = 0xFFFFFFFF;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BUFFER_SIZE);
            try
            {
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, BUFFER_SIZE)) > 0)
                {
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