using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Mappie.Utils
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


        [DllImport("CascLib.dll")]
        public static extern unsafe bool CascOpenStorage([MarshalAs(UnmanagedType.LPTStr)] string szParams, uint dwLocaleMask, out nint phStorage);

        [DllImport("CascLib.dll")]
        public static extern unsafe bool CascCloseStorage(nint hStorage);

        [DllImport("CascLib.dll")]
        public static extern unsafe bool CascCloseFile(nint hFile);

        [DllImport("CascLib.dll")]
        public static extern unsafe bool CascOpenFile(nint hStorage, [MarshalAs(UnmanagedType.LPTStr)] string szFileName, uint dwLocaleFlags, uint dwOpenFlags, out nint phFile);

        [DllImport("CascLib.dll")]
        public static extern unsafe nint CascFindFirstFile(nint hStorage, [MarshalAs(UnmanagedType.LPTStr)] string szMask, out nint phFindData, [MarshalAs(UnmanagedType.LPTStr)] string szListFile);

        [DllImport("CascLib.dll")]
        public static extern unsafe bool CascFindNextFile(nint hFindData, out nint phFindData);

        [DllImport("CascLib.dll")]
        public static extern unsafe bool CascReadFile(nint hFile, void* lpBuffer, uint dwToRead, out uint pdwRead);

    }
}
