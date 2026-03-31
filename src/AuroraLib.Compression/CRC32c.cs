using System;
using System.Runtime.InteropServices;

#if NET6_0_OR_GREATER
using System.Runtime.Intrinsics.X86;
#endif

/// <summary>
/// CRC32 Castagnoli
/// </summary>
internal static class Crc32C
{
    // CRC32C-Table
    private static readonly uint[] Crc32CTable;

    static Crc32C()
    {
#if NET6_0_OR_GREATER
        if (Sse42.IsSupported)
        {
            Crc32CTable = Array.Empty<uint>();
            return;
        }
#endif
        Crc32CTable = GenerateTable();

    }

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF; // initial CRC-32C Wert

#if NET6_0_OR_GREATER

        // Nativ Crc32C
        if (Sse42.IsSupported)
        {
            if (data.Length >= 4)
            {
                var ints = MemoryMarshal.Cast<byte, uint>(data);
                foreach (var i in ints)
                    crc = Sse42.Crc32(crc, i);
                data = data[(ints.Length * 4)..];
            }
            foreach (byte b in data)
                crc = Sse42.Crc32(crc, b);

            return ~crc;
        }
#endif
        // SoftwareCrc32C
        uint[] table = Crc32CTable;
        foreach (byte b in data)
        {
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return ~crc;
    }

    private static uint[] GenerateTable()
    {
        const uint poly = 0x1EDC6F41;
        uint[] table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) != 0)
                    crc = (crc >> 1) ^ poly;
                else
                    crc >>= 1;
            }
            table[i] = crc;
        }
        return table;
    }
}
