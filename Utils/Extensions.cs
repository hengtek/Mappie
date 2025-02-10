using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace DotnesktRemastered.Utils
{
    public static class BinaryReaderExtensions
    {
        public static void Seek(this BinaryReader br, long offset, SeekOrigin seekOrigin)
        {
            // Set stream position
            br.BaseStream.Seek(offset, seekOrigin);
        }

        public static string ReadNullTerminatedString(this BinaryReader br, int maxSize = -1)
        {
            // Create String Builder
            StringBuilder str = new StringBuilder();
            // Current Byte Read
            int byteRead;
            // Size of String
            int size = 0;
            // Loop Until we hit terminating null character
            while ((byteRead = br.BaseStream.ReadByte()) != 0x0 && size++ != maxSize)
                str.Append(Convert.ToChar(byteRead));
            // Ship back Result
            return str.ToString();
        }
        public static string ReadNullTerminatedString(this BinaryReader br, long offset, int maxSize = -1)
        {
            long currentOffset = br.BaseStream.Position;
            br.BaseStream.Position = offset;
            var result = br.ReadNullTerminatedString(maxSize);
            br.BaseStream.Position = currentOffset;
            return result;
        }

        public static T[] ReadArray<T>(this BinaryReader br, long offset, int count)
        {
            long currentOffset = br.BaseStream.Position;
            br.BaseStream.Position = offset;
            var result = br.ReadArray<T>(count);
            br.BaseStream.Position = currentOffset;
            return result;
        }
        public static T[] ReadArray<T>(this BinaryReader br, int count)
        {
            // Get Byte Count
            var size = count * Marshal.SizeOf<T>();
            // Allocate Array
            var result = new T[count];
            // Check for primitives, we can use BlockCopy for them
            if (typeof(T).IsPrimitive)
            {
                // Copy
                Buffer.BlockCopy(br.ReadBytes(size), 0, result, 0, size);
            }
            // Slightly more complex structures, we can use the struct functs
            else
            {
                // Loop through
                for (int i = 0; i < count; i++)
                {
                    // Read it into result
                    result[i] = br.ReadStruct<T>();
                }
            }
            // Done
            return result;
        }
        public static T ReadStruct<T>(this BinaryReader br)
        {
            byte[] data = br.ReadBytes(Marshal.SizeOf<T>());
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            T theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return theStructure;
        }


        public static long ReadInt64(this BinaryReader br)
        {
            return BitConverter.ToInt64(br.ReadBytes(8), 0);
        }

        public static ulong ReadUInt64(this BinaryReader br)
        {
            return BitConverter.ToUInt64(br.ReadBytes(8), 0);
        }

        public unsafe static byte* ReadPointer(this BinaryReader reader, uint size)
        {
            byte[] array = new byte[size];
            for (uint i = 0; i < size; i++)
            {
                array[i] = reader.ReadByte();
            }

            fixed (byte* arrayPtr = array)
            {
                return arrayPtr;
            }
        }

        public static Vector3 ReadVec3(this BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
        public unsafe static byte* ReadPointer(this BinaryReader reader, ulong size)
        {
            byte[] array = new byte[size];
            for (ulong i = 0; i < size; i++)
            {
                array[i] = reader.ReadByte();
            }

            fixed (byte* arrayPtr = array)
            {
                return arrayPtr;
            }
        }
        public static string ReadNullTerminatedString(this BinaryReader reader)
        {
            string str = "";
            char ch;
            while (reader.BaseStream.Length > reader.BaseStream.Position && (int)(ch = reader.ReadChar()) != 0)
                str = str + ch;
            return str;
        }

    }


}
