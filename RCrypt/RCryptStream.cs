using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RCrypt
{
    public class RCryptStream : Stream
    {
        private Stream _stream;
        private bool _read;
        private string _scramble;
        private BitCube _cube;

        public RCryptStream(Stream baseStream, bool read, string scramble)
        {
            _stream = baseStream;
            _read = read;
            _scramble = scramble;
            _cube = new BitCube();
            _cube.SetScramble(scramble);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _cube.Init(buffer[0], buffer[1], buffer[2], buffer[3], buffer[4], buffer[5]);
            _stream.Write(_cube.DoScramble(), 0, 6);
        }
    }

    internal class BitCube
    {
        private byte[] _bytes;
        private List<Action> _moves = new List<Action>();

        public void Init(byte b1, byte b2, byte b3, byte b4, byte b5, byte b6)
        {
            _bytes = new[] { b1, b2, b3, b4, b5, b6 };
        }

        public void SetScramble(string scramble)
        {
            _moves.Clear();
            for (int i = 0; i < scramble.Length; ++i)
            {
                var move = scramble[i];
                switch (move)
                {
                    case 'R':
                        if (i + 1 < scramble.Length && scramble[i + 1] == '\'')
                        {
                            _moves.Add(AntiR);
                            ++i;
                        }
                        else
                            _moves.Add(R);
                        break;
                    case 'L':
                        if (i + 1 < scramble.Length && scramble[i + 1] == '\'')
                        {
                            _moves.Add(AntiL);
                            ++i;
                        }
                        else
                            _moves.Add(L);
                        break;
                    case 'U':
                        if (i + 1 < scramble.Length && scramble[i + 1] == '\'')
                        {
                            _moves.Add(AntiU);
                            ++i;
                        }
                        else
                            _moves.Add(U);
                        break;
                    case 'D':
                        if (i + 1 < scramble.Length && scramble[i + 1] == '\'')
                        {
                            _moves.Add(AntiD);
                            ++i;
                        }
                        else
                            _moves.Add(D);
                        break;
                    case 'F':
                        if (i + 1 < scramble.Length && scramble[i + 1] == '\'')
                        {
                            _moves.Add(AntiF);
                            ++i;
                        }
                        else
                            _moves.Add(F);
                        break;
                    case 'B':
                        if (i + 1 < scramble.Length && scramble[i + 1] == '\'')
                        {
                            _moves.Add(AntiB);
                            ++i;
                        }
                        else
                            _moves.Add(B);
                        break;
                    case 'M':
                        if (i + 1 < scramble.Length && scramble[i + 1] == '\'')
                        {
                            _moves.Add(AntiM);
                            ++i;
                        }
                        else
                            _moves.Add(M);
                        break;
                    case 'E':
                        if (i + 1 < scramble.Length && scramble[i + 1] == '\'')
                        {
                            _moves.Add(AntiE);
                            ++i;
                        }
                        else
                            _moves.Add(E);
                        break;
                    case 'S':
                        if (i + 1 < scramble.Length && scramble[i + 1] == '\'')
                        {
                            _moves.Add(AntiS);
                            ++i;
                        }
                        else
                            _moves.Add(S);
                        break;
                }
            }
        }

        public byte[] DoScramble()
        {
            var count = _moves.Count;
            for (int i = 0; i < count; ++i)
            {
                _moves[i]();
            }
            return _bytes;
        }

        private void R()
        {
            var maskGet = 0b10010100;
            var maskClear = 0b01101011;

            var tus = _bytes[0];
            var tfs = _bytes[2];
            var tbs = _bytes[4];
            var tds = _bytes[5];

            _bytes[4] = (byte)(tbs & maskClear | maskGet & tus);

            _bytes[5] = (byte)(tds & maskClear | maskGet & tbs);

            _bytes[2] = (byte)(tfs & maskClear | maskGet & tds);

            _bytes[0] = (byte)(tus & maskClear | maskGet & tfs);
            //---------------------
            MoveSideClockwise(3);
        }

        private void L()
        {
            var maskGet = 0b00101001;
            var maskClear = 0b11010110;//check

            var tus = _bytes[0];
            var tfs = _bytes[2];
            var tbs = _bytes[4];
            var tds = _bytes[5];

            _bytes[4] = (byte)(tfs & maskClear | maskGet & tus);

            _bytes[5] = (byte)(tds & maskClear | maskGet & tfs);

            _bytes[2] = (byte)(tbs & maskClear | maskGet & tds);

            _bytes[0] = (byte)(tus & maskClear | maskGet & tbs);
            //---------------------
            MoveSideClockwise(1);
        }

        private void F()
        {
            var maskClearForR = 0b11010110;
            var maskClearForD = 0b11111000;
            var maskClearForL = 0b01101011;
            var maskClearForU = 0b00011111;

            var tus = _bytes[0];
            var tls = _bytes[1];
            var trs = _bytes[3];
            var tds = _bytes[5];

            _bytes[3] = (byte)(trs & maskClearForR | tus >> 5 & 0b00000001 | tus >> 3 & 0b00001000 | tus >> 2 & 0b00100000);

            _bytes[5] = (byte)(tds & maskClearForD | trs >> 5 & 0b00000001 | trs >> 2 & 0b00000010 | trs << 2 & 0b00000100);

            _bytes[1] = (byte)(tls & maskClearForL | tds << 2 & 0b00000100 | tds << 3 & 0b00010000 | tds << 5 & 0b10000000);

            _bytes[0] = (byte)(tus & maskClearForU | tls << 5 & 0b10000000 | tls << 2 & 0b01000000 | tls >> 2 & 0b00100000);
            //---------------------
            MoveSideClockwise(2);
        }

        private void B()
        {
            var maskClearForR = 0b01101011;
            var maskClearForD = 0b00011111;
            var maskClearForL = 0b00101001;
            var maskClearForU = 0b11111000;

            var tus = _bytes[0];
            var tls = _bytes[1];
            var trs = _bytes[3];
            var tds = _bytes[5];

            _bytes[3] = (byte)(trs & maskClearForR | tds >> 5 & 0b00000100 | tds >> 2 & 0b00010000 | tds << 2 & 0b10000000);

            _bytes[5] = (byte)(tds & maskClearForD | tls << 2 & 0b10000000 | tls << 3 & 0b01000000 | tls << 5 & 0b00100000);

            _bytes[1] = (byte)(tls & maskClearForL | tus << 5 & 0b00100000 | tus << 2 & 0b00001000 | tus >> 2 & 0b00000001);

            _bytes[0] = (byte)(tus & maskClearForU | trs >> 2 & 0b00000001 | trs >> 3 & 0b00000010 | trs >> 5 & 0b00000100);
            //---------------------
            MoveSideClockwise(4);
        }

        private void U()
        {
            var maskClearForRFL = 0b11111000;
            var maskClearForB = 0b00011111;

            var trs = _bytes[3];
            var tls = _bytes[1];
            var tfs = _bytes[2];
            var tbs = _bytes[4];

            _bytes[3] = (byte)(trs & maskClearForRFL | tbs >> 7 & 0b00000001 | tbs >> 5 & 0b00000010 | tbs >> 3 & 0b00000100);

            _bytes[4] = (byte)(tbs & maskClearForB | tls << 7 & 0b10000000 | tls << 5 & 0b01000000 | tls << 3 & 0b00100000);

            _bytes[1] = (byte)(tls & maskClearForRFL | tfs & 0b00000111);

            _bytes[2] = (byte)(tfs & maskClearForRFL | trs & 0b00000111);
            //---------------------
            MoveSideClockwise(0);
        }

        private void D()
        {
            var maskClearForRFL = 0b00011111;
            var maskClearForB = 0b11111000;

            var trs = _bytes[3];
            var tls = _bytes[1];
            var tfs = _bytes[2];
            var tbs = _bytes[4];

            _bytes[1] = (byte)(tls & maskClearForRFL | tbs << 7 & 0b10000000 | tbs << 5 & 0b01000000 | tbs << 3 & 0b00100000);

            _bytes[4] = (byte)(tbs & maskClearForB | trs >> 7 & 0b00000001 | trs >> 5 & 0b00000010 | trs >> 3 & 0b00000100);

            _bytes[3] = (byte)(trs & maskClearForRFL | tfs & 0b11100000);

            _bytes[2] = (byte)(tfs & maskClearForRFL | tls & 0b11100000);
            //---------------------
            MoveSideClockwise(5);
        }


        private void AntiR()
        {
            var maskGet = 0b10010100;
            var maskClear = 0b01101011;

            var tus = _bytes[0];
            var tfs = _bytes[2];
            var tbs = _bytes[4];
            var tds = _bytes[5];

            _bytes[4] = (byte)(tfs & maskClear | maskGet & tus);

            _bytes[5] = (byte)(tds & maskClear | maskGet & tfs);

            _bytes[2] = (byte)(tbs & maskClear | maskGet & tds);

            _bytes[0] = (byte)(tus & maskClear | maskGet & tbs);
            //---------------------
            MoveSideCounterClockwise(3);
        }

        private void AntiL()
        {
            var maskGet = 0b00101001;
            var maskClear = 0b11010110;//check

            var tus = _bytes[0];
            var tfs = _bytes[2];
            var tbs = _bytes[4];
            var tds = _bytes[5];

            _bytes[4] = (byte)(tfs & maskClear | maskGet & tds);

            _bytes[5] = (byte)(tus & maskClear | maskGet & tfs);

            _bytes[2] = (byte)(tbs & maskClear | maskGet & tus);

            _bytes[0] = (byte)(tds & maskClear | maskGet & tbs);
            //---------------------
            MoveSideClockwise(1);
        }

        private void AntiF()
        {
            var maskClearForR = 0b11010110;
            var maskClearForD = 0b11111000;
            var maskClearForL = 0b01101011;
            var maskClearForU = 0b00011111;

            var tus = _bytes[0];
            var tls = _bytes[1];
            var trs = _bytes[3];
            var tds = _bytes[5];

            _bytes[1] = (byte)(tls & maskClearForL | tus << 2 & 0b10000000 | tus >> 2 & 0b00010000 | tus >> 5 & 0b00000100);

            _bytes[5] = (byte)(tds & maskClearForD | tls >> 5 & 0b00000100 | tls >> 3 & 0b00000010 | tls >> 2 & 0b00000001);

            _bytes[3] = (byte)(trs & maskClearForR | tds >> 2 & 0b00000001 | tds << 2 & 0b00001000 | tds << 5 & 0b00100000);

            _bytes[0] = (byte)(tus & maskClearForU | trs << 5 & 0b00100000 | trs << 3 & 0b01000000 | trs << 2 & 0b10000000);
            //---------------------
            MoveSideClockwise(2);
        }

        private void AntiB()
        {
            var maskClearForR = 0b01101011;
            var maskClearForD = 0b00011111;
            var maskClearForL = 0b00101001;
            var maskClearForU = 0b11111000;

            var tus = _bytes[0];
            var tls = _bytes[1];
            var trs = _bytes[3];
            var tds = _bytes[5];

            _bytes[3] = (byte)(trs & maskClearForR | tus << 5 & 0b10000000 | tus << 3 & 0b00010000 | tus << 2 & 0b00000100);

            _bytes[5] = (byte)(tds & maskClearForD | trs >> 2 & 0b00100000 | trs << 2 & 0b01000000 | trs << 5 & 0b10000000);

            _bytes[1] = (byte)(tls & maskClearForL | tds >> 5 & 0b00000001 | tds >> 3 & 0b00001000 | tds >> 2 & 0b00100000);

            _bytes[0] = (byte)(tus & maskClearForU | tls << 2 & 0b00000100 | tls >> 2 & 0b00000010 | tls >> 5 & 0b00000001);
            //---------------------
            MoveSideCounterClockwise(4);
        }

        private void AntiU()
        {
            var maskClearForRFL = 0b11111000;
            var maskClearForB = 0b00011111;

            var trs = _bytes[3];
            var tls = _bytes[1];
            var tfs = _bytes[2];
            var tbs = _bytes[4];

            _bytes[1] = (byte)(tls & maskClearForRFL | tbs >> 3 & 0b00000100 | tbs >> 5 & 0b00000010 | tbs >> 7 & 0b00000001);

            _bytes[2] = (byte)(tfs & maskClearForRFL | tls & 0b00000111);

            _bytes[3] = (byte)(trs & maskClearForRFL | tfs & 0b00000111);

            _bytes[4] = (byte)(tbs & maskClearForB | trs << 3 & 0b00100000 | trs << 5 & 0b01000000 | trs << 7 & 0b10000000);
            //---------------------
            MoveSideCounterClockwise(0);
        }

        private void AntiD()
        {
            var maskClearForRFL = 0b00011111;
            var maskClearForB = 0b11111000;

            var trs = _bytes[3];
            var tls = _bytes[1];
            var tfs = _bytes[2];
            var tbs = _bytes[4];

            _bytes[1] = (byte)(tls & maskClearForRFL | tfs & 0b11100000);

            _bytes[4] = (byte)(tbs & maskClearForB | tls >> 3 & 0b00000100 | tls >> 5 & 0b00000010 | tls >> 7 & 0b00000001);

            _bytes[3] = (byte)(trs & maskClearForRFL | tbs << 3 & 0b00100000 | tbs << 5 & 0b01000000 | tbs << 7 & 0b10000000);

            _bytes[2] = (byte)(tfs & maskClearForRFL | trs & 0b11100000);
            //---------------------
            MoveSideCounterClockwise(5);
        }


        private void M()
        {
            var maskClear = 0b10111101;
            var maskGet = 0b01000010;

            var tus = _bytes[0];
            var tfs = _bytes[2];
            var tds = _bytes[5];
            var tbs = _bytes[4];

            _bytes[0] = (byte)(tus & maskClear | tbs & maskGet);

            _bytes[4] = (byte)(tbs & maskClear | tds & maskGet);

            _bytes[5] = (byte)(tds & maskClear | tfs & maskGet);

            _bytes[2] = (byte)(tfs & maskClear | tus & maskGet);
        }

        private void AntiM()
        {
            var maskClear = 0b10111101;
            var maskGet = 0b01000010;

            var tus = _bytes[0];
            var tfs = _bytes[2];
            var tds = _bytes[5];
            var tbs = _bytes[4];

            _bytes[0] = (byte)(tus & maskClear | tfs & maskGet);

            _bytes[4] = (byte)(tbs & maskClear | tus & maskGet);

            _bytes[5] = (byte)(tds & maskClear | tbs & maskGet);

            _bytes[2] = (byte)(tfs & maskClear | tds & maskGet);
        }

        private void S()
        {
            var maskClearUD = 0b11100111;
            var maskClearRL = 0b10111101;

            var tus = _bytes[0];
            var trs = _bytes[3];
            var tds = _bytes[5];
            var tls = _bytes[1];

            _bytes[0] = (byte)(tus & maskClearUD | tls << 3 & 0b00001000 | tls >> 3 & 0b00001000);

            _bytes[3] = (byte)(trs & maskClearRL | tus << 2 & 0b01000000 | tus >> 2 & 0b00000010);

            _bytes[5] = (byte)(tds & maskClearUD | trs >> 3 & 0b00001000 | trs << 3 & 0b00010000);

            _bytes[1] = (byte)(tls & maskClearRL | tds >> 2 & 0b00000010 | tds << 2 & 0b01000000);
        }

        private void AntiS()
        {
            var maskClearUD = 0b11100111;
            var maskClearRL = 0b10111101;

            var tus = _bytes[0];
            var trs = _bytes[3];
            var tds = _bytes[5];
            var tls = _bytes[1];

            _bytes[0] = (byte)(tus & maskClearUD | trs << 2 & 0b00001000 | trs >> 2 & 0b00010000);

            _bytes[1] = (byte)(tls & maskClearRL | tus << 3 & 0b01000000 | tus >> 3 & 0b00000010);

            _bytes[5] = (byte)(tds & maskClearUD | tls >> 2 & 0b00010000 | tls << 2 & 0b00001000);

            _bytes[3] = (byte)(trs & maskClearRL | tds >> 3 & 0b00000010 | tds << 3 & 0b01000000);
        }


        private void E()
        {
            var maskClear = 0b11100111;
            var maskGetRFL = 0b00011000;

            var tls = _bytes[1];
            var tfs = _bytes[2];
            var trs = _bytes[3];
            var tbs = _bytes[4];

            _bytes[2] = (byte)(tfs & maskClear | tls & maskGetRFL);

            _bytes[3] = (byte)(trs & maskClear | tfs & maskGetRFL);

            _bytes[4] = (byte)(tbs & maskClear | trs >> 1 & 0b00001000 | trs << 1 & 0b00010000);

            _bytes[1] = (byte)(tls & maskClear | tbs >> 1 & 0b00010000 | tbs << 1 & 0b00001000);
        }

        private void AntiE()
        {
            var maskClear = 0b11100111;
            var maskGetRFL = 0b00011000;

            var tls = _bytes[1];
            var tfs = _bytes[2];
            var trs = _bytes[3];
            var tbs = _bytes[4];

            _bytes[2] = (byte)(tfs & maskClear | trs & maskGetRFL);

            _bytes[1] = (byte)(tls & maskClear | tfs & maskGetRFL);

            _bytes[4] = (byte)(tbs & maskClear | tls << 1 & 0b00010000 | tls >> 1 & 0b00001000);

            _bytes[3] = (byte)(trs & maskClear | tbs >> 1 & 0b00001000 | tbs << 1 & 0b00010000);
        }


        private void MoveSideClockwise(int sideIndex)
        {
            var ts = _bytes[sideIndex];
            _bytes[sideIndex] = (byte)(ts << 2 & 0b01000100 | ts << 3 & 0b00010000 | ts << 5 & 0b10000000
                | ts >> 2 & 0b00100010 | ts >> 3 & 0b00001000 | ts >> 5 & 0b00000001);
        }

        private void MoveSideCounterClockwise(int sideIndex)
        {
            var ts = _bytes[sideIndex];
            _bytes[sideIndex] = (byte)(ts >> 2 & 0b00010001 | ts >> 3 & 0b00000010 | ts >> 5 & 0b00000100
                | ts << 2 & 0b10001000 | ts << 3 & 0b01000000 | ts << 5 & 0b00100000);
        }

    }
}
