using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DotnesktRemastered.Utils
{
    public class NativeLib
    {
        [DllImport("oo2core_8_win64.dll")]
        public static extern unsafe long OodleLZ_Decompress(
            byte* compBuf, uint compSize, byte* rawBuf, uint rawSize,
            int fuzzSafe,
            int checkCrc,
            int verbosity,
            byte* dictBuff,
            ulong dictSize,
            ulong unk,
            void* unkCallback,
            byte* scratchBuff,
            ulong scratchSize,
            int threadPhase);
    }
}
