using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DotnesktRemastered.Utils
{
    public class MW6FaceIndices
    {
        public static CordycepProcess Cordycep = Program.Cordycep;

        //Ref: https://github.com/Scobalula/Greyhound/blob/master/src/WraithXCOD/WraithXCOD/CoDXModelMeshHelper.cpp#L37
        public static ushort[] UnpackFaceIndices(nint tables, nint tableCount, nint packedIndices, nint indices, uint faceIndex, bool isLocal = false)
        {
            uint currentFaceIndex = faceIndex;
            for (int i = 0; i < tableCount; i++)
            {
                nint tablePtr = tables + (i * 40);
                nint tableIndicesPtr = packedIndices + (nint)Cordycep.ReadMemory<uint>(tablePtr + 36, isLocal);
                byte count = Cordycep.ReadMemory<byte>(tablePtr + 35, isLocal);
                if (currentFaceIndex < count)
                {
                    ushort[] faceIndices = new ushort[3];
                    byte bits = (byte)(Cordycep.ReadMemory<byte>(tablePtr + 34, isLocal) - 1);
                    faceIndex = Cordycep.ReadMemory<uint>(tablePtr + 28, isLocal);

                    uint faceIndex1Offset = FindFaceIndex(tableIndicesPtr, currentFaceIndex * 3 + 0, bits, isLocal) + faceIndex;
                    uint faceIndex2Offset = FindFaceIndex(tableIndicesPtr, currentFaceIndex * 3 + 1, bits, isLocal) + faceIndex;
                    uint faceIndex3Offset = FindFaceIndex(tableIndicesPtr, currentFaceIndex * 3 + 2, bits, isLocal) + faceIndex;

                    faceIndices[0] = Cordycep.ReadMemory<ushort>(indices + (nint)(faceIndex1Offset * 2), isLocal);
                    faceIndices[1] = Cordycep.ReadMemory<ushort>(indices + (nint)(faceIndex2Offset * 2), isLocal);
                    faceIndices[2] = Cordycep.ReadMemory<ushort>(indices + (nint)(faceIndex3Offset * 2), isLocal);
                    return faceIndices;
                }
                currentFaceIndex -= count;
            }
            return null;
        }

        private static byte FindFaceIndex(nint packedIndices, uint index, byte bits, bool isLocal = false)
        {
            int bitIndex;

            if (!Bits.BitScanReverse64(bits, out bitIndex))
                bitIndex = 64;
            else
                bitIndex ^= 0x3F;

            ushort offset = (ushort)(index * (byte)(64 - bitIndex));
            byte bitCount = (byte)(64 - bitIndex);
            nint packedIndicesPtr = packedIndices + (offset >> 3);
            byte bitOffset = (byte)(offset & 7);

            byte packedIndice = Cordycep.ReadMemory<byte>(packedIndicesPtr, isLocal);

            if (bitOffset == 0)
                return (byte) (packedIndice & ((1 << bitCount) - 1));

            if (8 - bitOffset < bitCount)
            {
                byte nextPackedIndice = Cordycep.ReadMemory<byte>(packedIndicesPtr + 1, isLocal);
                return (byte)((packedIndice >> bitOffset) & ((1 << (8 - bitOffset)) - 1) | ((nextPackedIndice & ((1 << (64 - bitIndex - (8 - bitOffset))) - 1)) << (8 - bitOffset)));
            }

            return (byte)((packedIndice >> bitOffset) & ((1 << bitCount) - 1));
        }
    }
}
