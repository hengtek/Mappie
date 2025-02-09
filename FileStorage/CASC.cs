using DotnesktRemastered.Utils;
using PhilLibX;
using PhilLibX.Compression;
using Serilog;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DotnesktRemastered.FileStorage
{
    public class CascStorage
    {
        /// <summary>
        /// STRUCTS
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct IndexFileBlock
        {
            public uint BlockSize;
            public uint BlockHash;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct IndexFileHeader
        {
            public ushort IndexVersion;
            public byte BucketIndex;
            public byte ExtraBytes;
            public byte EncodedSizeLength;
            public byte StorageOffsetLength;
            public byte EncodingKeyLength;
            public byte FileOffsetBits;
            public ulong SegmentSize;

        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BlockTableHeader
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] EncodingKey;
            public uint ContentSize;
            public ushort Flags;
            public uint JenkinsHash;
            public uint Checksum;
            public uint Signature;
            public uint HeaderSize;
            public byte TableFormat;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] FrameCountBE;
            public int GetFrameCount()
            {
                return (FrameCountBE[0] << 16) | (FrameCountBE[1] << 8) | FrameCountBE[2];
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BlockTableEntry
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] EncodedSizeBE;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] ContentSizeBE;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] Hash;

            public int GetEncodedSize()
            {
                return (EncodedSizeBE[0] << 24) | (EncodedSizeBE[1] << 16) | (EncodedSizeBE[2] << 8) | EncodedSizeBE[3];
            }
            public int GetContentSize()
            {
                return (ContentSizeBE[0] << 24) | (ContentSizeBE[1] << 16) | (ContentSizeBE[2] << 8) | ContentSizeBE[3];
            }
        }

        /// <summary>
        /// PROPERTIES
        /// </summary>
        // Main Path
        public string GamePath { get; set; }
        // Data Path
        public string DataPath { get; set; }
        // Data Path
        public string BuildInfoPath { get; set; }
        // Config Build Key
        public string BuildKey { get; set; }
        // VFS Root Key
        public string VFSRootKey { get; set; }

        public static List<DataFile> DataFiles = new List<DataFile>();

        public Dictionary<IndexKey, IndexEntry> DataEntries = new Dictionary<IndexKey, IndexEntry>();

        public FileSystem.TVFSHandler FileSystemHandler { get; set; }

        public class DataFile
        {
            public string FileName { get; set; }
            public DataFile(string fileName)
            {
                FileName = fileName;
            }
        }

        public class IndexKey
        {
            public byte[] EncodingKey = new byte[16];
            public int EncodingKeySize;
            public string Hasher;

            public string GetHasher() => Hasher;
            public override int GetHashCode()
            {
                return Hasher?.GetHashCode() ?? 0;
            }
            public override bool Equals(object other) => (other as IndexKey)?.Hasher == Hasher;

            public IndexKey(byte[] EntryBuffer, IndexFileHeader Header)
            {
                EncodingKeySize = Header.EncodingKeyLength > 16 ? 16 : Header.EncodingKeyLength;
                Buffer.BlockCopy(EntryBuffer, 0, EncodingKey, 0, EncodingKeySize);
                Hasher = string.Format("0x{0:x}", xxHash64.ComputeHash(EncodingKey, EncodingKeySize) & 0xFFFFFFFFFFFFFFFF);
            }

            public IndexKey(string Key, int KeySize)
            {
                EncodingKeySize = KeySize > 16 ? 16 : KeySize;
                byte[] hashmap = new byte[]
                {
                    0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
                    0x08, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                };
                // https://gist.github.com/vi/dd3b5569af8a26b97c8e20ae06e804cb
                for (byte pos = 0; ((pos < (EncodingKeySize * 2)) && (pos < Key.Length)); pos += 2)
                {
                    var idx0 = ((byte)Key[pos + 0] & 0x1F) ^ 0x10;
                    var idx1 = ((byte)Key[pos + 1] & 0x1F) ^ 0x10;
                    EncodingKey[pos / 2] = (byte)((hashmap[idx0] << 4) | hashmap[idx1]);
                };
                Hasher = string.Format("0x{0:x}", xxHash64.ComputeHash(EncodingKey, EncodingKeySize) & 0xFFFFFFFFFFFFFFFF);
            }

            public IndexKey(int KeySize)
            {
                EncodingKeySize = KeySize;
            }

        }

        public class IndexEntry
        {
            public byte[] EncodingKey = new byte[16];
            public int EncodingKeySize;
            public uint Offset;
            public uint Size;
            public uint ArchiveIndex;

            public IndexEntry(byte[] EntryBuffer, IndexFileHeader Header)
            {
                ulong FileOffsetMask = (uint)((1 << Header.FileOffsetBits) - 1);
                ulong PackedOffsetAndIndex = 0;

                PackedOffsetAndIndex = (PackedOffsetAndIndex << 0x08) | EntryBuffer[0 + Header.EncodingKeyLength];
                PackedOffsetAndIndex = (PackedOffsetAndIndex << 0x08) | EntryBuffer[1 + Header.EncodingKeyLength];
                PackedOffsetAndIndex = (PackedOffsetAndIndex << 0x08) | EntryBuffer[2 + Header.EncodingKeyLength];
                PackedOffsetAndIndex = (PackedOffsetAndIndex << 0x08) | EntryBuffer[3 + Header.EncodingKeyLength];
                PackedOffsetAndIndex = (PackedOffsetAndIndex << 0x08) | EntryBuffer[4 + Header.EncodingKeyLength];

                Size = 0;
                Size = (Size << 0x08) | EntryBuffer[3 + Header.EncodingKeyLength + Header.StorageOffsetLength];
                Size = (Size << 0x08) | EntryBuffer[2 + Header.EncodingKeyLength + Header.StorageOffsetLength];
                Size = (Size << 0x08) | EntryBuffer[1 + Header.EncodingKeyLength + Header.StorageOffsetLength];
                Size = (Size << 0x08) | EntryBuffer[0 + Header.EncodingKeyLength + Header.StorageOffsetLength];

                ArchiveIndex = (uint)(PackedOffsetAndIndex >> Header.FileOffsetBits);
                Offset = (uint)(PackedOffsetAndIndex & FileOffsetMask);
                EncodingKeySize = Header.EncodingKeyLength;

                Buffer.BlockCopy(EntryBuffer, 0, EncodingKey, 0, Header.EncodingKeyLength > 16 ? 16 : Header.EncodingKeyLength);
            }
        }

        public class FileFrame
        {
            public long VirtualStartOffset;
            // Virtual End Offset
            public long VirtualEndOffset;
            // Offset within the Data File
            public long ArchiveOffset;
            // Encoded Size (compressed, including flag, etc)
            public int EncodedSize;
            // Content Size
            public int ContentSize;

        }

        public class FileSpan
        {
            public List<FileFrame> Frames = new List<FileFrame>();
            // Virtual Start Offset
            public long VirtualStartOffset { get; set; }
            // Virtual End Offset
            public long VirtualEndOffset { get; set; }
            // Offset within the Data File
            public long ArchiveOffset { get; set; }
            // Data File Index
            public long ArchiveIndex { get; set; }

            public FileSpan(long Index)
            {
                ArchiveIndex = Index;
            }
            public FileFrame FindFrame(long Position)
            {
                foreach (var Frame in Frames)
                {
                    if (Position >= Frame.VirtualStartOffset && Position < Frame.VirtualEndOffset)
                        return Frame;
                }
                // Nothing happend
                return null;
            }

        }

        public class FileReader
        {
            public List<FileSpan> Spans = new List<FileSpan>();

            public byte[] Cache;

            public long CacheStartPosition;
            // End Position of the Cache
            public long CacheEndPosition;
            // Our Internal Position
            public long InternalPosition;
            // The length of the File
            public long Length;
            public FileSpan FindSpan(long Position)
            {
                foreach (var Span in Spans)
                {
                    if (Position >= Span.VirtualStartOffset && Position < Span.VirtualEndOffset)
                    {
                        return Span;
                    }
                }

                return null;
            }

            public void Consume()
            {
                Cache = new byte[Length];
                CacheStartPosition = 0;
                CacheEndPosition = Length;

                foreach (var Span in Spans)
                {
                    foreach (var Frame in Span.Frames)
                    {
                        var TempBuffer = ReadDataFile((int)Span.ArchiveIndex, Frame.ArchiveOffset, Frame.EncodedSize);

                        switch (TempBuffer[0])
                        {
                            case 0x4E:
                                Buffer.BlockCopy(TempBuffer, 1, Cache, (int)Frame.VirtualStartOffset, Frame.ContentSize);
                                break;
                            case 0x5A:
                                var decompressed = ZLIB.Decompress(TempBuffer.Skip(1).ToArray());
                                Buffer.BlockCopy(decompressed, 0, Cache, (int)Frame.VirtualStartOffset, decompressed.Length);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            public int ReadInt32()
            {
                return BitConverter.ToInt32(Read(0, 4), 0);
            }
            public uint ReadUInt32()
            {
                return BitConverter.ToUInt32(Read(0, 4), 0);
            }

            public ulong ReadUInt64()
            {
                return BitConverter.ToUInt64(Read(0, 8), 0);
            }
            public long ReadInt64()
            {
                return BitConverter.ToInt64(Read(0, 8), 0);
            }

            public byte[] ReadBytes(long count)
            {
                byte[] buffer = new byte[count];
                long readStartPos = InternalPosition;

                // Our we outside the file?
                if (readStartPos >= Length)
                    return null;

                long toRead = count;
                uint offset = 0;
                uint consumed = 0;
                while (true)
                {
                    long readEndPos = readStartPos + offset;
                    long cacheAvailable = CacheEndPosition - readStartPos;

                    if (cacheAvailable > 0 && Cache != null)
                    {
                        var p = (uint)(readStartPos - CacheStartPosition);
                        var n = (uint)((toRead <= cacheAvailable) ? toRead : cacheAvailable);

                        if (n <= 8)
                        {
                            int byteCount = (int)n;
                            while (--byteCount >= 0)
                                buffer[offset + byteCount] = Cache[p + byteCount];
                        }
                        else
                        {
                            Buffer.BlockCopy(Cache, (int)p, buffer, (int)offset, (int)n);
                        }

                        toRead -= n;
                        InternalPosition += n;
                        offset += n;
                        consumed += n;
                    }

                    // We've satisfied what we need
                    if (toRead == 0)
                        break;

                    readStartPos = InternalPosition;

                    // Our we outside the file?
                    if (readStartPos >= Length)
                        break;

                    var span = FindSpan(readStartPos);
                    var frame = span.FindFrame(readStartPos);

                    var TempBuffer = ReadDataFile((int)span.ArchiveIndex, frame.ArchiveOffset, frame.EncodedSize);

                    Cache = new byte[frame.ContentSize];
                    CacheStartPosition = frame.VirtualStartOffset;
                    CacheEndPosition = frame.VirtualEndOffset;

                    switch (TempBuffer[0])
                    {
                        case 0x4E:
                            Buffer.BlockCopy(TempBuffer, 1, Cache, 0, frame.ContentSize);
                            break;
                        case 0x5A:
                            var decompressed = ZLIB.Decompress(TempBuffer.Skip(1).ToArray());
                            Buffer.BlockCopy(decompressed, 0, Cache, 0, decompressed.Length);
                            break;
                        default:
                            break;
                    }
                }

                return buffer;
            }
            public byte[] Read(int offset, long count)
            {
                byte[] buffer = new byte[count];
                long readStartPos = InternalPosition;

                // Our we outside the file?
                if (readStartPos >= Length)
                    return null;

                long toRead = count;
                int consumed = 0;
                while (true)
                {
                    long readEndPos = readStartPos + offset;
                    long cacheAvailable = CacheEndPosition - readStartPos;

                    if (cacheAvailable > 0 && Cache != null)
                    {
                        var p = (int)(readStartPos - CacheStartPosition);
                        var n = (int)((toRead <= cacheAvailable) ? toRead : cacheAvailable);

                        if (n <= 8)
                        {
                            int byteCount = (int)n;
                            while (--byteCount >= 0)
                                buffer[offset + byteCount] = Cache[p + byteCount];
                        }
                        else
                        {
                            Buffer.BlockCopy(Cache, p, buffer, offset, n);
                        }

                        toRead -= n;
                        InternalPosition += n;
                        offset += n;
                        consumed += n;
                    }

                    // We've satisfied what we need
                    if (toRead == 0)
                        break;

                    readStartPos = InternalPosition;

                    // Our we outside the file?
                    if (readStartPos >= Length)
                        break;

                    var span = FindSpan(readStartPos);
                    var frame = span.FindFrame(readStartPos);

                    var TempBuffer = ReadDataFile((int)span.ArchiveIndex, frame.ArchiveOffset, frame.EncodedSize);

                    Cache = new byte[frame.ContentSize];
                    CacheStartPosition = frame.VirtualStartOffset;
                    CacheEndPosition = frame.VirtualEndOffset;

                    switch (TempBuffer[0])
                    {
                        case 0x4E:
                            Buffer.BlockCopy(TempBuffer, 1, Cache, 0, frame.ContentSize);
                            break;
                        case 0x5A:
                            var decompressed = ZLIB.Decompress(TempBuffer.Skip(1).ToArray());
                            Buffer.BlockCopy(decompressed, 0, Cache, 0, decompressed.Length);
                            break;
                        default:
                            break;
                    }
                }

                return buffer;
            }

            public byte[] Read(byte[] buffer, int offset, int count)
            {
                long readStartPos = InternalPosition;

                // Our we outside the file?
                if (readStartPos >= Length)
                    return null;

                int toRead = count;
                int consumed = 0;
                while (true)
                {
                    long readEndPos = readStartPos + offset;
                    long cacheAvailable = CacheEndPosition - readStartPos;

                    if (cacheAvailable > 0 && Cache != null)
                    {
                        var p = (int)(readStartPos - CacheStartPosition);
                        var n = (int)((toRead <= cacheAvailable) ? toRead : cacheAvailable);

                        if (n <= 8)
                        {
                            int byteCount = (int)n;
                            while (--byteCount >= 0)
                                buffer[offset + byteCount] = Cache[p + byteCount];
                        }
                        else
                        {
                            Buffer.BlockCopy(Cache, p, buffer, offset, n);
                        }

                        toRead -= n;
                        InternalPosition += n;
                        offset += n;
                        consumed += n;
                    }

                    // We've satisfied what we need
                    if (toRead == 0)
                        break;

                    readStartPos = InternalPosition;

                    // Our we outside the file?
                    if (readStartPos >= Length)
                        break;

                    var span = FindSpan(readStartPos);
                    var frame = span.FindFrame(readStartPos);

                    var TempBuffer = ReadDataFile((int)span.ArchiveIndex, frame.ArchiveOffset, frame.EncodedSize);

                    Cache = new byte[frame.ContentSize];
                    CacheStartPosition = frame.VirtualStartOffset;
                    CacheEndPosition = frame.VirtualEndOffset;


                    switch (TempBuffer[0])
                    {
                        case 0x4E:
                            //Array.Copy(TempBuffer, 1, Cache, 0, frame.ContentSize);
                            Buffer.BlockCopy(TempBuffer, 1, Cache, 0, frame.ContentSize);
                            break;
                        case 0x5A:
                            var decompressed = ZLIB.Decompress(TempBuffer.Skip(1).ToArray());
                            Buffer.BlockCopy(decompressed, 0, Cache, 0, decompressed.Length);
                            break;
                        default:
                            break;
                    }
                }

                return buffer;
            }

            public byte Peek()
            {
                var Temp = InternalPosition;
                var Result = Read(0, 1);
                InternalPosition = Temp;

                return Result[0];
            }

            public void SetPosition(long position)
            {
                InternalPosition = position;
            }

            public long GetPosition()
            {
                return InternalPosition;
            }

            public void Advance(long position)
            {
                InternalPosition += position;
            }

        }

        public class FileSystem
        {
            public class Entry
            {
                public string Name { get; set; }
                public List<IndexKey> KeyEntries = new List<IndexKey>();
                public int Size { get; set; }
                public bool Exists { get; set; }

                public Entry(string EntryName)
                {
                    Name = EntryName;
                    Size = 0;
                    Exists = true;
                }
            }

            public class TVFSHandler
            {
                public Dictionary<string, Entry> FileEntries = new Dictionary<string, Entry>();
                public TVFSHeader Header = new TVFSHeader();
                public enum PathTableNodeFlags : uint
                {
                    None = 0x0000,
                    PathSeparatorPre = 0x0001,
                    PathSeparatorPost = 0x0002,
                    IsNodeValue = 0x0004,
                };
                [StructLayout(LayoutKind.Sequential, Pack = 1)]
                public struct TVFSHeader
                {
                    public int Signature;
                    public byte FormatVersion;
                    public byte HeaderSize;
                    public byte EncodingKeySize;
                    public byte PatchKeySize;
                    public uint32be_t Flags;
                    public uint32be_t PathTableOffset;
                    public uint32be_t PathTableSize;
                    public uint32be_t VFSTableOffset;
                    public uint32be_t VFSTableSize;
                    public uint32be_t CFTTableOffset;
                    public uint32be_t CFTTableSize;
                    public ushort MaxDepth;

                    public void ParseValues()
                    {
                        Flags.Data = Flags.ToUInt32();
                        PathTableOffset.Data = PathTableOffset.ToUInt32();
                        PathTableSize.Data = PathTableSize.ToUInt32();
                        VFSTableOffset.Data = VFSTableOffset.ToUInt32();
                        VFSTableSize.Data = VFSTableSize.ToUInt32();
                        CFTTableOffset.Data = CFTTableOffset.ToUInt32();
                        CFTTableSize.Data = CFTTableSize.ToUInt32();
                    }

                }
                public struct uint32be_t
                {
                    public uint Data;

                    public uint ToUInt32()
                    {
                        var buffer = BitConverter.GetBytes(Data);
                        return (uint)((buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3]);
                    }
                }

                [StructLayout(LayoutKind.Sequential, Pack = 1)]
                public struct PathTableNode
                {
                    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 260)]
                    public char[] Name;
                    public byte NameSize;
                    public uint Flags;
                    public uint Value;

                }

                public void Parse(FileReader Reader)
                {
                    Reader.SetPosition(0);
                    var buffer = Reader.Read(0, Marshal.SizeOf<TVFSHeader>());
                    Header = CordycepProcess.BytesToStruct<TVFSHeader>(buffer);
                    Header.ParseValues();
                    StringBuilder Builder = new StringBuilder(260);

                    Reader.SetPosition(Header.PathTableOffset.Data);
                    ParsePathTable(Reader, Reader.InternalPosition + Header.PathTableSize.Data, "");
                }

                public void ParsePathTable(FileReader Reader, long end, string Builder)
                {
                    var CurrentPosition = Builder.Length;
                    while (Reader.GetPosition() < end)
                    {
                        if (Reader.GetPosition() == 625)
                            Console.WriteLine();
                        var Entry = ParsePathNode(Reader);
                        if (Convert.ToBoolean(Entry.Flags & (uint)PathTableNodeFlags.PathSeparatorPre))
                            Builder += "\\";
                        Builder += new string(Entry.Name);
                        if (Convert.ToBoolean(Entry.Flags & (uint)PathTableNodeFlags.PathSeparatorPost))
                            Builder += "\\";
                        if (Convert.ToBoolean(Entry.Flags & (uint)PathTableNodeFlags.IsNodeValue))
                        {
                            if ((Entry.Value & 0x80000000) != 0)
                            {
                                var FolderSize = Entry.Value & 0x7FFFFFFF;
                                var FolderStart = Reader.GetPosition();
                                var FolderEnd = FolderStart + FolderSize - 4;

                                ParsePathTable(Reader, FolderEnd, Builder);
                            }
                            else
                                AddFileEntry(Reader, Builder, Entry.Value);
                            Builder = Builder.Remove(CurrentPosition, Builder.Length - CurrentPosition);
                        }

                    }
                }

                public PathTableNode ParsePathNode(FileReader Reader)
                {
                    PathTableNode entry = new PathTableNode();
                    var buf = Reader.Peek();
                    if (buf == 0)
                    {
                        entry.Flags |= (uint)PathTableNodeFlags.PathSeparatorPre;
                        Reader.Advance(1);
                        buf = Reader.Peek();
                    }
                    if (buf < 0x7F && buf != 0xFF)
                    {
                        Reader.Advance(1);
                        entry.Name = Encoding.UTF8.GetString(Reader.Read(0, buf)).ToCharArray();
                        entry.NameSize = buf;
                        buf = Reader.Peek();
                    }

                    if (buf == 0)
                    {
                        entry.Flags |= (uint)PathTableNodeFlags.PathSeparatorPost;
                        Reader.Advance(1);
                        buf = Reader.Peek();
                    }

                    if (buf == 0xFF)
                    {
                        Reader.Advance(1);
                        var valueBuffer = Reader.Read(0, 4);
                        entry.Value = CordycepProcess.BytesToStruct<uint32be_t>(valueBuffer).ToUInt32();
                        entry.Flags |= (uint)PathTableNodeFlags.IsNodeValue;
                    }
                    else
                        entry.Flags |= (uint)PathTableNodeFlags.PathSeparatorPost;

                    return entry;
                }

                public void AddFileEntry(FileReader Reader, string Name, uint Offset)
                {
                    var PathTableCurrent = Reader.GetPosition();

                    Reader.SetPosition(Header.VFSTableOffset.Data + Offset);
                    var SpanCount = Reader.Read(0, 1)[0];
                    var FileEntry = new FileSystem.Entry(Name);
                    for (byte i = 0; i < SpanCount; i++)
                    {
                        var RefFileOffset = CordycepProcess.BytesToStruct<uint32be_t>(Reader.Read(0, 4)).ToUInt32();
                        var sizeOfSpan = CordycepProcess.BytesToStruct<uint32be_t>(Reader.Read(0, 4)).ToUInt32();
                        var CFTOffset = ReadCFTOffset(Reader);

                        var VFSTableCurrent = Reader.GetPosition();
                        Reader.SetPosition(Header.CFTTableOffset.Data + CFTOffset);
                        IndexKey Index = new IndexKey(Header.EncodingKeySize);
                        Reader.Read(Index.EncodingKey, 0, Header.EncodingKeySize);
                        Index.Hasher = string.Format("0x{0:x}", xxHash64.ComputeHash(Index.EncodingKey, Index.EncodingKeySize) & 0xFFFFFFFFFFFFFFFF);
                        FileEntry.KeyEntries.Add(Index);
                        Reader.SetPosition(VFSTableCurrent);
                    }

                    if (!FileEntries.ContainsKey(FileEntry.Name))
                        FileEntries.Add(FileEntry.Name, FileEntry);

                    Reader.SetPosition(PathTableCurrent);
                }

                public uint ReadCFTOffset(FileReader Reader)
                {
                    var Buffer = new byte[4];

                    if (Header.CFTTableSize.Data > 0xFFFFFF)
                    {
                        Buffer = Reader.Read(0, 4);
                        return (uint)((Buffer[0] << 24) | (Buffer[1] << 16) | (Buffer[2] << 8) | Buffer[3]);
                    }
                    else if (Header.CFTTableSize.Data > 0xFFFF)
                    {
                        Buffer = Reader.Read(0, 3);
                        return (uint)((Buffer[0] << 16) | (Buffer[1] << 8) | Buffer[2]);
                    }
                    else if (Header.CFTTableSize.Data > 0xFF)
                    {
                        Buffer = Reader.Read(0, 2);
                        return (uint)((Buffer[0] << 8) | Buffer[1]);
                    }
                    else
                    {
                        Buffer = Reader.Read(0, 1);
                        return Buffer[0];
                    }
                }
            }

        }

        public CascStorage(string path)
        {
            GamePath = path;
            DataPath = Path.Combine(GamePath, "Data\\data");
            BuildInfoPath = Path.Combine(GamePath, ".build.info");

            LoadBuildInfo();
            LoadConfigInfo();
            LoadDataFiles();
            LoadIndexFiles();

            var LookUpKey = new IndexKey(VFSRootKey, 9);
            var Entry = DataEntries[LookUpKey];
            var Reader = OpenFile(Entry);

            Reader.Consume();
            if (Reader.ReadUInt32() == 0x53465654)
            {
                FileSystemHandler = new FileSystem.TVFSHandler();
                FileSystemHandler.Parse(Reader);

                foreach (var File in FileSystemHandler.FileEntries)
                {
                    foreach (var FileEntry in File.Value.KeyEntries)
                    {
                        if (!DataEntries.ContainsKey(FileEntry))
                        {
                            File.Value.Exists = false;
                            break;
                        }
                    }
                }
            }
        }

        public FileReader OpenFile(IndexEntry Entry)
        {
            long VirtualOffset = 0;
            var DataFile = DataFiles[(int)Entry.ArchiveIndex];
            FileReader freader = new FileReader();
            using (FileStream fs = new FileStream(DataFile.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BinaryReader Reader = new BinaryReader(fs, Encoding.Default))
            {
                FileSpan Span = new FileSpan(Entry.ArchiveIndex);
                Reader.BaseStream.Position = Entry.Offset;
                var BLTEHeader = Reader.ReadStruct<BlockTableHeader>();

                if (BLTEHeader.HeaderSize > 0)
                {
                    int FrameCount = BLTEHeader.GetFrameCount();
                    var BLTEntries = new BlockTableEntry[FrameCount];
                    for (int i = 0; i < FrameCount; i++)
                    {
                        BLTEntries[i] = Reader.ReadStruct<BlockTableEntry>();
                    }
                    var ArchiveOffset = Reader.BaseStream.Position;
                    Span.ArchiveOffset = ArchiveOffset;
                    Span.VirtualStartOffset = VirtualOffset;
                    Span.VirtualEndOffset = VirtualOffset;
                    for (int i = 0; i < FrameCount; i++)
                    {
                        FileFrame Frame = new FileFrame();

                        Frame.ArchiveOffset = ArchiveOffset;
                        Frame.EncodedSize = BLTEntries[i].GetEncodedSize();
                        Frame.ContentSize = BLTEntries[i].GetContentSize();
                        Frame.VirtualStartOffset = VirtualOffset;
                        Frame.VirtualEndOffset = VirtualOffset + Frame.ContentSize;

                        Span.VirtualEndOffset += Frame.ContentSize;

                        ArchiveOffset += Frame.EncodedSize;
                        VirtualOffset += Frame.ContentSize;

                        Span.Frames.Add(Frame);
                    }
                    freader.Spans.Add(Span);

                    freader.Length = VirtualOffset;

                    return freader;
                }
            }


            return null;
        }

        public FileReader OpenFile(string FileName)
        {
            if (FileSystemHandler == null)
                return null;

            var File = FileSystemHandler.FileEntries[FileName];
            if (!File.Exists)
                return null;
            long VirtualOffset = 0;
            FileReader freader = new FileReader();
            foreach (var KeyEntry in File.KeyEntries)
            {
                var Entry = DataEntries[KeyEntry];
                var DataFile = DataFiles[(int)Entry.ArchiveIndex];
                using (FileStream fs = new FileStream(DataFile.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (BinaryReader Reader = new BinaryReader(fs, Encoding.Default))
                {
                    FileSpan Span = new FileSpan(Entry.ArchiveIndex);
                    Reader.BaseStream.Position = Entry.Offset;
                    var BLTEHeader = Reader.ReadStruct<BlockTableHeader>();

                    if (BLTEHeader.HeaderSize > 0)
                    {
                        int FrameCount = BLTEHeader.GetFrameCount();
                        var BLTEntries = new BlockTableEntry[FrameCount];
                        for (int i = 0; i < FrameCount; i++)
                        {
                            BLTEntries[i] = Reader.ReadStruct<BlockTableEntry>();
                        }
                        var ArchiveOffset = Reader.BaseStream.Position;
                        Span.ArchiveOffset = ArchiveOffset;
                        Span.VirtualStartOffset = VirtualOffset;
                        Span.VirtualEndOffset = VirtualOffset;
                        for (int i = 0; i < FrameCount; i++)
                        {
                            FileFrame Frame = new FileFrame();

                            Frame.ArchiveOffset = ArchiveOffset;
                            Frame.EncodedSize = BLTEntries[i].GetEncodedSize();
                            Frame.ContentSize = BLTEntries[i].GetContentSize();
                            Frame.VirtualStartOffset = VirtualOffset;
                            Frame.VirtualEndOffset = VirtualOffset + Frame.ContentSize;

                            Span.VirtualEndOffset += Frame.ContentSize;

                            ArchiveOffset += Frame.EncodedSize;
                            VirtualOffset += Frame.ContentSize;

                            Span.Frames.Add(Frame);
                        }
                    }
                    freader.Spans.Add(Span);

                }
            }

            freader.Length = VirtualOffset;

            return freader;
        }

        public static byte[] ReadDataFile(int ArchiveIndex, long Offset, int Size)
        {
            var File = DataFiles[ArchiveIndex];
            using (FileStream fs = new FileStream(File.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                reader.BaseStream.Position = Offset;

                return reader.ReadBytes(Size);
            }
        }

        private void LoadBuildInfo()
        {
            if (!File.Exists(BuildInfoPath))
                return;

            using (FileStream fs = new FileStream(BuildInfoPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(fs))
            {
                int BuildKeyIndex = 0;
                bool Success = true;

                for (int i = 0; i < 2; i++)
                {
                    string Line = reader.ReadLine();
                    if (!Success)
                        break;
                    var Split = Line.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    if (i == 0)
                    {
                        for (int j = 0; j < Split.Length; j++)
                        {
                            if (Split[j] == "Build Key!HEX:16")
                            {
                                BuildKeyIndex = j;
                                break;
                            }
                        }
                    }
                    else
                    {
                        BuildKey = Split[BuildKeyIndex];
                        break;
                    }
                }
            }
        }
        private void LoadConfigInfo()
        {
            if (BuildKey.Length < 4)
                return;

            string FolderName = Path.Combine(GamePath, Path.Combine("Data\\config", Path.Combine(BuildKey.Substring(0, 2), BuildKey.Substring(2, 2))));
            string FileName = Path.Combine(FolderName, BuildKey);
            if (!File.Exists(FileName))
                return;
            using (FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(fs))
            {
                while (true)
                {
                    bool Success = true;
                    string Line = reader.ReadLine();

                    if (!Success)
                        break;
                    if (Line == null || String.IsNullOrWhiteSpace(Line) || Line.StartsWith("#"))
                        continue;

                    var LineSplit = Line.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                    if (LineSplit[0].Trim() == "vfs-root")
                    {
                        var ValueSplit = LineSplit[1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        VFSRootKey = ValueSplit[1];
                        break;
                    }
                }
            }
        }
        private void LoadDataFiles()
        {
            string[] DataFileNames = Directory.GetFiles(DataPath, "data.*");
            foreach (string DataFileName in DataFileNames)
            {
                string[] IndexSplit = DataFileName.Split('.');
                if (IndexSplit.Length >= 2)
                {
                    int ArchiveIndex = Convert.ToInt32(IndexSplit.Last());
                    DataFiles.Add(new DataFile(DataFileName));
                }
            }
        }
        private void LoadIndexFiles()
        {
            string[] IndexFiles = Directory.GetFiles(DataPath, "*.idx");
            foreach (string IndexFile in IndexFiles)
            {
#if DEBUG
                Console.WriteLine(string.Format("LoadIndexFiles(): Loading {0}...", Path.GetFileName(IndexFile)));
#endif
                using (FileStream fs = new FileStream(IndexFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var IndexReader = new BinaryReader(fs, Encoding.Default))
                {
                    var HeaderBlock = IndexReader.ReadStruct<IndexFileBlock>();
                    if (HeaderBlock.BlockSize == Marshal.SizeOf<IndexFileHeader>())
                    {
                        var Header = IndexReader.ReadStruct<IndexFileHeader>();
                        if (Header.ExtraBytes == 0 &&
                            Header.EncodedSizeLength == 4 &&
                            Header.StorageOffsetLength == 5 &&
                            Header.EncodingKeyLength == 9)
                        {
                            IndexReader.BaseStream.Position = (IndexReader.BaseStream.Position + 0x17) & 0xFFFFFFF0;
                            var DataBlock = IndexReader.ReadStruct<IndexFileBlock>();
                            int EntrySize = Header.EncodedSizeLength + Header.StorageOffsetLength + Header.EncodingKeyLength;
                            for (uint bytesConsumed = 0; bytesConsumed < DataBlock.BlockSize; bytesConsumed += (uint)EntrySize)
                            {
                                var EntryBuffer = IndexReader.ReadBytes(EntrySize);
                                var EntryIndexKey = new IndexKey(EntryBuffer, Header);
                                var indexEntry = new IndexEntry(EntryBuffer, Header);
                                if (!DataEntries.ContainsKey(EntryIndexKey))
                                    DataEntries.Add(EntryIndexKey, indexEntry);
                                else
                                    DataEntries[EntryIndexKey] = indexEntry;
                            }
                        }
                    }
                }
            }
        }
    }

    public class CASCPackage
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct XSubv2Header
        {
            public int Magic;
            public short Unknown1;
            public short Version;
            public long Unknown2;
            public long Type;
            public long Size;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1896)]
            public byte[] UnknownHashes;
            public long FileCount;
            public long DataOffset;
            public long DataSize;
            public long HashCount;
            public long HashOffset;
            public long HashSize;
            public long Unknown3;
            public long UnknownOffset;
            public long Unknown4;
            public long IndexCount;
            public long IndexOffset;
            public long IndexSize;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct XSubv2HashEntry
        {
            public ulong Key;
            public ulong PackedInfo;
            public uint PackedInfoEx;
        }

        public static List<string> files = new List<string>();
        public static Dictionary<ulong, PackageCacheObject> Assets = new Dictionary<ulong, PackageCacheObject>();
        public static CascStorage storage;

        public static void LoadFiles(string gamePath)
        {
            if (File.Exists(Path.Combine(gamePath, ".build.info")))
            {
                storage = new CascStorage(gamePath);
                var FileNames = storage.FileSystemHandler.FileEntries.Values;
                int FileIndex = 0;
                foreach (var cascFile in FileNames)
                {
                    if (!cascFile.Exists || !Path.GetExtension(cascFile.Name).EndsWith("xsub"))
                        continue;
                    var Reader = storage.OpenFile(cascFile.Name);

                    ReadCascXSub(Reader, FileIndex, Assets);

                    files.Add(cascFile.Name);
                    FileIndex++;
                }
            }
        }

        public unsafe static void ReadCascXSub(CascStorage.FileReader cascReader, int fileIndex, Dictionary<ulong, PackageCacheObject> assetPackages)
        {
            // Read header
            var Header = CordycepProcess.BytesToStruct<XSubv2Header>(cascReader.Read(0, Marshal.SizeOf<XSubv2Header>()));
            if (Header.Magic == 0x4950414b && Header.HashOffset < cascReader.Length)
            {
                // Jump to hash offset
                cascReader.SetPosition(Header.HashOffset);
                byte[] HashData = new byte[(int)Header.HashCount * Marshal.SizeOf<XSubv2HashEntry>()];
                cascReader.Read(HashData, 0, (int)Header.HashCount * Marshal.SizeOf<XSubv2HashEntry>());
                int hashBufferOffset = 0;
                // Loop through hashes
                for (long i = 0; i < Header.HashCount; i++)
                {
                    // Read it
                    var Entry = CordycepProcess.BytesToStruct<XSubv2HashEntry>(HashData, hashBufferOffset);
                    hashBufferOffset += Marshal.SizeOf<XSubv2HashEntry>();
                    // Add it
                    // Prepare a cache entry
                    var NewObject = new PackageCacheObject()
                    {
                        Offset = (Entry.PackedInfo >> 32) << 7,
                        CompressedSize = (Entry.PackedInfo >> 1) & 0x3FFFFFFF,
                        UncompressedSize = 0,
                        PackageFileIndex = fileIndex
                    };

                    // Add object
                    if (!assetPackages.ContainsKey(Entry.Key))
                        assetPackages.Add(Entry.Key, NewObject);
                }

                cascReader.SetPosition(0);
                cascReader.CacheEndPosition = 0;
                cascReader.CacheStartPosition = 0;
            }
        }

        public static unsafe byte[] ExtractXSubPackage(ulong key, uint size)
        {
            if (!Assets.ContainsKey(key))
            {
                Log.Warning("{key} does not exist in the current package objects database.", key);
                return null;
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            PackageCacheObject cacheObject = Assets[key];

            var XSUBFileName = files[(int)cacheObject.PackageFileIndex];

            var reader = storage.OpenFile(XSUBFileName);

            ulong dataRead = 0;
            int totalSize = 0;

            byte[] tempBuffer = new byte[size > 0 ? size : 0x2400000];

            ulong blockPosition = cacheObject.Offset;
            ulong blockEnd = cacheObject.Offset + cacheObject.CompressedSize;

            XSubBlock[] blocks = new XSubBlock[256];

            reader.SetPosition((long)blockPosition + 2);

            if (reader.ReadUInt64() != key)
            {
                reader.SetPosition((long)blockPosition);
                return reader.ReadBytes((int)cacheObject.CompressedSize);
            }
            while ((ulong)reader.GetPosition() < blockEnd)
            {
                reader.SetPosition((long)blockPosition + 22);
                byte blockCount = reader.ReadBytes(1)[0];
                for (int i = 0; i < blockCount; i++)
                {
                    blocks[i] = new XSubBlock()
                    {
                        compressionType = reader.ReadBytes(1)[0],
                        compressedSize = reader.ReadUInt32(),
                        decompressedSize = reader.ReadUInt32(),
                        blockOffset = reader.ReadUInt32(),
                        decompressedOffset = reader.ReadUInt32(),
                        unknown = reader.ReadUInt32()
                    };
                }

                for (int i = 0; i < blockCount; i++)
                {
                    reader.SetPosition((long)blockPosition + blocks[i].blockOffset);
                    byte[] compressedBlock = reader.ReadBytes((int)blocks[i].compressedSize);
                    switch (blocks[i].compressionType)
                    {
                        case 6: //Oodle
                            fixed (byte* compressedBlockPtr = compressedBlock)
                            {
                                fixed (byte* decompressedPtr = tempBuffer)
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
                blockPosition = (ulong)(reader.GetPosition() + 0x7F) & 0xFFFFFFFFFFFFF80;
            }
            stopwatch.Stop();
            Log.Debug("Decompressed {key:X} in {time}ms. Buffer size: {size}", key, stopwatch.ElapsedMilliseconds, tempBuffer.Length);
            return tempBuffer;
        }
    }
}
