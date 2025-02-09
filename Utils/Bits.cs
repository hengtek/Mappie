using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DotnesktRemastered.Utils
{
    internal class Bits
    {
        public static bool BitScanReverse64(ulong value, out int index)
        {
            if (value == 0)
            {
                index = 0; // No bits are set
                return false;
            }

            index = 63; // Start from the most significant bit
            while ((value & 1UL << index) == 0)
            {
                index--;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RotateLeft(uint value, int offset)
        {
#if FCL_BITOPS
            return System.Numerics.BitOperations.RotateLeft(value, offset);
#else
            return (value << offset) | (value >> (32 - offset));
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong RotateLeft(ulong value, int offset) // Taken help from: https://stackoverflow.com/a/48580489/5592276
        {
#if FCL_BITOPS
            return System.Numerics.BitOperations.RotateLeft(value, offset);
#else
            return (value << offset) | (value >> (64 - offset));
#endif
        }
    }
}
