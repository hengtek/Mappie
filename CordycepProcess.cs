using Serilog;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Mappie
{

    public struct XAsset64
    {
        // The asset header.
        public nint Header;
        // Whether or not this asset is a temp slot.
        public nint Temp;
        // The next xasset in the list.
        public nint Next;
        // The previous xasset in the list.
        public nint Previous;
    };

    // Parasytes XAsset Pool Structure.
    public struct XAssetPool64
    {
        // The start of the asset chain.
        public nint Root;
        // The end of the asset chain.
        public nint End;
        // The asset hash table for this pool.
        public nint LookupTable;
        // Storage for asset headers for this pool.
        public nint HeaderMemory;
        // Storage for asset entries for this pool.
        public nint AssetMemory;
    };
    public unsafe class CordycepProcess
    {
        public SafeHandle ProcessHandle;
        public string WorkingEnvironment;

        public ulong GameID;
        public nint PoolsAddress;
        public nint StringsAddress;
        public string GameDirectory;

        public string[] Flags;

        public CordycepProcess(Process process)
        {
            ProcessHandle = process.SafeHandle;
            WorkingEnvironment = System.IO.Path.GetDirectoryName(process.MainModule.FileName);
        }
        public unsafe void EnumerableAssetPool(uint poolIdx, Action<XAsset64> action)
        {
            nint poolPtr = (nint)(PoolsAddress + poolIdx * sizeof(XAssetPool64));
            XAssetPool64 pool = ReadMemory<XAssetPool64>(poolPtr);
            for (nint assetPtr = pool.Root; assetPtr != 0; assetPtr = ReadMemory<XAsset64>(assetPtr).Next)
            {
                XAsset64 asset = ReadMemory<XAsset64>(assetPtr);
                if (asset.Header == 0) continue;
                action(asset);
            }
        }

        public unsafe XAsset64 FindAsset(uint poolIdx, ulong hash)
        {
            nint poolPtr = (nint)(PoolsAddress + poolIdx * sizeof(XAssetPool64));
            XAssetPool64 pool = ReadMemory<XAssetPool64>(poolPtr);
            for (nint assetPtr = pool.Root; assetPtr != 0; assetPtr = ReadMemory<XAsset64>(assetPtr).Next)
            {
                XAsset64 asset = ReadMemory<XAsset64>(assetPtr);
                if (asset.Header == 0) continue;
                if (hash == ReadMemory<ulong>(asset.Header)) return asset;
            }
            return new XAsset64() {
                Header = 0
            };
        }

        public void LoadState()
        {
            string statePath = System.IO.Path.Combine(WorkingEnvironment, "Data\\CurrentHandler.csi");
            if(!System.IO.File.Exists(statePath))
            {
                Log.Error("CurrentHandler.csi not found.");
                return;
            }
            //TODO: Check if these are still valid, don't think they get clean up on start / on close
            BinaryReader reader = new BinaryReader(System.IO.File.OpenRead(statePath));
            GameID = reader.ReadUInt64();
            PoolsAddress = (nint)reader.ReadUInt64();
            StringsAddress = (nint)reader.ReadUInt64();
            int gameDirectoryLength = reader.ReadInt32();
            GameDirectory = new string(reader.ReadChars(gameDirectoryLength));
            
            uint flagsCount = reader.ReadUInt32();
            Flags = new string[flagsCount];
            for (int i = 0; i < flagsCount; i++)
            {
                int flagLength = reader.ReadInt32();
                Flags[i] = new string(reader.ReadChars(flagLength));
            }
            reader.Close();
        }
        public bool IsSinglePlayer()
        {
            return Flags.Contains("sp");
        }


        //isLocal: check if its address is local or not,
        //yes i know this is stupid and dumb
        //but its the least "breaking" way for me to do this
        public unsafe T ReadMemory<T>(nint address, bool isLocal = false) where T : unmanaged
        {
            if(isLocal)
            {
                return *(T*)address;
            }
            T result = new();
            var size = (nuint)Marshal.SizeOf<T>();

            _ = PInvoke.ReadProcessMemory((HANDLE)ProcessHandle.DangerousGetHandle(), (void*)address, &result, size);

            return result;
        }

        public unsafe byte[] ReadRawMemory(nint address, long size)
        {
            var buffer = new byte[size];

            fixed (void* bufferPtr = buffer)
            {
                _ = PInvoke.ReadProcessMemory((HANDLE)ProcessHandle.DangerousGetHandle(), (void*)address, bufferPtr, (nuint)size);
            }

            return buffer;
        }

        public unsafe string ReadString(nint address)
        {
            List<byte> bytes = new List<byte>();
            while (true)
            {
                byte c = ReadMemory<byte>(address);
                if (c == 0x00) break;
                bytes.Add(c);
                address += 1;
            }

            return System.Text.Encoding.ASCII.GetString(bytes.ToArray());
        }

        public static T BytesToStruct<T>(byte[] data)
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            T theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return theStructure;
        }

        public static T BytesToStruct<T>(byte[] data, int startIndex)
        {
            // Size of Struct
            int size = Marshal.SizeOf<T>();
            // Create new byte array
            byte[] buffer = new byte[size];
            //File.WriteAllText("test.txt", string.Format("startIndex = {0}", startIndex));
            // Copy it
            Buffer.BlockCopy(data, startIndex, buffer, 0, size);
            // Return result
            return BytesToStruct<T>(buffer);
        }

        public unsafe void WriteMemory<T>(nint address, T value) where T : unmanaged
        {
            var size = (nuint)Marshal.SizeOf<T>();
            _ = PInvoke.WriteProcessMemory((HANDLE)ProcessHandle.DangerousGetHandle(), (void*)address, &value, size);
        }
    }
}
