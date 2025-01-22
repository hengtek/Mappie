using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
