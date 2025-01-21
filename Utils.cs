using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DotnesktRemastered
{
    public class Utils
    {
        public static Vector3 UnpackCoDQTangent(uint packed)
        {
            uint idx = packed >> 30;

            float tx = ((packed >> 00 & 0x3FF) / 511.5f - 1.0f) / 1.4142135f;
            float ty = ((packed >> 10 & 0x3FF) / 511.5f - 1.0f) / 1.4142135f;
            float tz = ((packed >> 20 & 0x1FF) / 255.5f - 1.0f) / 1.4142135f;
            float tw = 0.0f;
            float sum = tx * tx + ty * ty + tz * tz;

            if (sum <= 1.0f)
                tw = (float)Math.Sqrt(1.0f - sum);

            float qX = 0.0f;
            float qY = 0.0f;
            float qZ = 0.0f;
            float qW = 0.0f;

            switch (idx)
            {
                case 0:
                    qX = tw; qY = tx; qZ = ty; qW = tz;
                    break;
                case 1:
                    qX = tx; qY = tw; qZ = ty; qW = tz;
                    break;
                case 2:
                    qX = tx; qY = ty; qZ = tw; qW = tz;
                    break;
                case 3:
                    qX = tx; qY = ty; qZ = tz; qW = tw;
                    break;
                default:
                    throw new Exception("Invalid CoD Q-Tangent index");
            }

            Vector3 tangent = new Vector3(
                1 - 2 * (qY * qY + qZ * qZ),
                2 * (qX * qY + qW * qZ),
                2 * (qX * qZ - qW * qY)
            );

            Vector3 bitangent = new Vector3(
                2 * (qX * qY - qW * qZ),
                1 - 2 * (qX * qX + qZ * qZ),
                2 * (qY * qZ + qW * qX));

            Vector3 normal = new Vector3(
                (tangent.Y * bitangent.Z) - (tangent.Z * bitangent.Y),
                (tangent.Z * bitangent.X) - (tangent.X * bitangent.Z),
                (tangent.X * bitangent.Y) - (tangent.Y * bitangent.X));

            return normal;
        }
    }
}
