using DotnesktRemastered.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DotnesktRemastered.FileStorage
{
    public class XSub
    {
        public static CordycepProcess Cordycep = Program.Cordycep;

        public static Dictionary<ulong, PackageCacheObject> CacheObjects = new Dictionary<ulong, PackageCacheObject>();

        //TODO
        public static void InitializeCasc(string gamePath)
        {

        }

        public static void LoadFiles(string gamePath)
        {
            string[] files = System.IO.Directory.GetFiles(gamePath, "*.xsub", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                BinaryReader reader = new BinaryReader(File.Open(file, FileMode.Open));
                ReadXSub(reader, file);
            }
            //dump test
        }

        private static void ReadXSub(BinaryReader reader, string filePath)
        {
            uint magic = reader.ReadUInt32();
            ushort unknown1 = reader.ReadUInt16();
            ushort version = reader.ReadUInt16();
            ulong unknown = reader.ReadUInt64();
            ulong type = reader.ReadUInt64();
            ulong size = reader.ReadUInt64();
            byte[] unknownHashes = reader.ReadBytes(1896);
            ulong fileCount = reader.ReadUInt64();
            ulong dataOffset = reader.ReadUInt64();
            ulong dataSize = reader.ReadUInt64();
            ulong hashCount = reader.ReadUInt64();
            ulong hashOffset = reader.ReadUInt64();
            ulong hashSize = reader.ReadUInt64();
            ulong unknown3 = reader.ReadUInt64();
            ulong unknownOffset = reader.ReadUInt64();
            ulong unknown4 = reader.ReadUInt64();
            ulong indexCount = reader.ReadUInt64();
            ulong indexOffset = reader.ReadUInt64();
            ulong indexSize = reader.ReadUInt64();
            if (type != 3) return;
            if (magic != 0x4950414b || hashOffset >= (ulong)reader.BaseStream.Length)
            {
                Log.Error($"Invalid XSUB file {filePath}");
                return;
            }

            reader.BaseStream.Seek((long)hashOffset, SeekOrigin.Begin);
            for (ulong i = 0; i < hashCount; i++)
            {
                ulong key = reader.ReadUInt64();
                ulong packedInfo = reader.ReadUInt64();
                uint packedInfoEx = reader.ReadUInt32();

                PackageCacheObject cacheObject = new PackageCacheObject()
                {
                    Offset = (packedInfo >> 32) << 7,
                    CompressedSize = (packedInfo >> 1) & 0x3FFFFFFF,
                    UncompressedSize = 0,
                    PackageFilePath = filePath
                };

                if (!CacheObjects.ContainsKey(key))
                    CacheObjects.Add(key, cacheObject);
            }

            reader.Close();
        }
        public static unsafe byte[] ExtractXSubPackage(ulong key, uint size)
        {
            if (!CacheObjects.ContainsKey(key))
            {
                Log.Warning("{key} does not exist in the current package objects database.", key);
                return null;
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            PackageCacheObject cacheObject = CacheObjects[key];
            BinaryReader reader = new BinaryReader(File.Open(cacheObject.PackageFilePath, FileMode.Open));

            ulong dataRead = 0;
            int totalSize = 0;

            byte[] tempBuffer = new byte[size > 0 ? size : 0x2400000];

            ulong blockPosition = cacheObject.Offset;
            ulong blockEnd = cacheObject.Offset + cacheObject.CompressedSize;

            XSubBlock[] blocks = new XSubBlock[256];

            reader.BaseStream.Seek((long)blockPosition + 2, SeekOrigin.Begin);

            if (reader.ReadUInt64() != key)
            {
                reader.BaseStream.Seek((long)blockPosition, SeekOrigin.Begin);
                return reader.ReadBytes((int)cacheObject.CompressedSize);
            }
            while((ulong)reader.BaseStream.Position < blockEnd)
            {
                reader.BaseStream.Seek((long)blockPosition + 22, SeekOrigin.Begin);
                byte blockCount = reader.ReadByte();
                for (int i = 0; i < blockCount; i++)
                {
                    blocks[i] = new XSubBlock()
                    {
                        compressionType = reader.ReadByte(),
                        compressedSize = reader.ReadUInt32(),
                        decompressedSize = reader.ReadUInt32(),
                        blockOffset = reader.ReadUInt32(),
                        decompressedOffset = reader.ReadUInt32(),
                        unknown = reader.ReadUInt32()
                    };
                }

                for (int i = 0; i < blockCount; i++)
                {
                    reader.BaseStream.Seek((long)blockPosition + blocks[i].blockOffset, SeekOrigin.Begin);
                    byte[] compressedBlock = reader.ReadBytes((int)blocks[i].compressedSize);
                    switch (blocks[i].compressionType)
                    {
                        case 6: //Oodle
                            fixed(byte* compressedBlockPtr = compressedBlock)
                            {
                                fixed(byte* decompressedPtr = tempBuffer)
                                {
                                    ulong result = (ulong)NativeLib.OodleLZ_Decompress(
                                        compressedBlockPtr,
                                        blocks[i].compressedSize,
                                        decompressedPtr + blocks[i].decompressedOffset,
                                        (uint)blocks[i].decompressedSize,
                                        0, 0, 0, null, 0, 0, null, null, 0, 3);

                                    if (result != blocks[i].decompressedSize)
                                    {
                                        throw new Exception($"Failed to decompress Oodle buffer, expected {blocks[i].decompressedSize} got {result}");
                                    }
                                }
                            }
                            break;
                        default:
                            Log.Warning("Unknown compression type {blocks[i].compressionType}");
                            break;
                    }
                }
                blockPosition = (ulong)((reader.BaseStream.Position + 0x7F) & 0xFFFFFFFFFFFFF80);
            }
            reader.Close();
            stopwatch.Stop();
            Log.Information("Decompressed {key:X} in {time}ms. Buffer size: {size}", key, stopwatch.ElapsedMilliseconds, tempBuffer.Length);
            return tempBuffer;
        }
    }

    public struct PackageCacheObject
    {

        // The data offset of this object
        public ulong Offset;
        // The size of this object
        public ulong CompressedSize;
        // The size of this object uncompressed
        public ulong UncompressedSize;
        // The index of the package file used for this
        public string PackageFilePath;
    }

    public struct XSubBlock
    {
        public byte compressionType;
        public uint compressedSize;
        public uint decompressedSize;
        public uint blockOffset;
        public uint decompressedOffset;
        public uint unknown;
    };
}
