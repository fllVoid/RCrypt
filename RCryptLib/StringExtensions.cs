using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RCryptLib
{
    internal static class StringExtensions
    {
        public static unsafe int GetCustomHashCode(this string s)
        {
            fixed (char* str = s)
            {
                char* chPtr = str;
                int num = 352654597;
                int num2 = num;
                int* numPtr = (int*)chPtr;
                for (int i = s.Length; i > 0; i -= 4)
                {
                    num = (((num << 5) + num) + (num >> 27)) ^ numPtr[0];
                    if (i <= 2)
                    {
                        break;
                    }
                    num2 = (((num2 << 5) + num2) + (num2 >> 27)) ^ numPtr[1];
                    numPtr += 2;
                }
                return (num + (num2 * 1566083941));
            }
        }
    }
}
