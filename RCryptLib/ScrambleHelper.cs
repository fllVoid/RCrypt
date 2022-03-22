using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RCryptLib
{
    public class ScrambleHelper
    {
        public static string ReverseScramble(string s)
        {
            var result = s.Reverse().ToArray();
            var list = new List<char>();
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] == '\'' && i + 1 != result.Length)
                {
                    list.Add(result[i + 1]);
                    ++i;
                }
                else if (result[i] == '2' && i + 1 != result.Length && result[i + 1] != '2')
                {
                    list.Add(result[i + 1]);
                    list.Add('2');
                    ++i;
                }
                else
                {
                    list.Add(result[i]);
                    list.Add('\'');
                }
            }
            return new string(list.ToArray());
        }
    }
}
