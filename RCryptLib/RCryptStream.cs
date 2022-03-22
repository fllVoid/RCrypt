using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RCryptLib
{
    public abstract class RCryptStream : Stream
    {
        public abstract bool LastWrite { get; set; }
        public abstract int BlockSizeInBytes { get; }
    }
}
