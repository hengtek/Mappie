using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DotnesktRemastered
{
    public class Utils
    {
        public static ulong[] DecryptString(ulong[] data)
        {
            ulong[] result = new ulong[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                ulong const1 = data[i];
                ulong lastByte = (const1 & 0xFF);
                result[i] =
                    ~((~((lastByte | 0xDE) +~lastByte + 34) &2)
                    *(((const1 | 0x4095625D92059ADE) +~const1 - 0x4095625D92059ADE) &0xFFFFFFFFFFFFFFFD)
                    +(((const1 | 0x4095625D92059ADE) +~const1 - 0x4095625D92059ADE) | 2)
                    *(((lastByte | 0xDE) +~lastByte + 34) &2))
                    -(const1 & 0x4095625D92059ADE)
                    +(~const1 & 0xBF6A9DA26DFA6521)
                    -1;
                Console.WriteLine(Encoding.UTF8.GetString(BitConverter.GetBytes(result[i])));
            }
            return result;
        }

        public static ulong HashTest(string str)
        {
            ulong result = 0x47F5817A5EF961BA;

            for (int i = 0; i < str.Length; i++)
            {
                ulong value = (byte) str[i].ToString().ToLower()[0];

                if (value == '\\')
                    value = '/';

                result = 0x100000001B3 * (value ^ result);
            }

            return result & 0x7FFFFFFFFFFFFFFF;
        }
    }
}
