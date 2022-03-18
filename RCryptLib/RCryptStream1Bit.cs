namespace RCryptLib
{
    internal class RCryptStream1Bit : Stream
    {
        private Stream _stream;
        private bool _read;
        private Cube1Bit _cube;
        public bool LastWrite { get; set; }

        public RCryptStream1Bit(Stream baseStream, bool read, string scramble, int seed)
        {
            _stream = baseStream;
            _read = read;
            _cube = new Cube1Bit(read, seed);
            _cube.SetScramble(scramble);
        }

        public override bool CanRead => _read;

        public override bool CanSeek => false;

        public override bool CanWrite => !_read;

        public override long Length => _read ? _stream.Length : throw new InvalidOperationException("Operation not possible in write mode.");

        public override long Position
        {
            get => _stream.Position;
            set => throw new NotImplementedException();
        }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            _stream.Read(buffer, offset, 6);
            _cube.Init(buffer[0], buffer[1], buffer[2], buffer[3], buffer[4], buffer[5]);
            if (_stream.Length <= _stream.Position + 6)
            {
                var lastPeace = new byte[6];
                _cube.DoScramble().CopyTo(lastPeace, 0);
                _stream.Read(buffer, offset, 6);
                _cube.Init(buffer[0], buffer[1], buffer[2], buffer[3], buffer[4], buffer[5]);
                _cube.DoScramble().CopyTo(buffer, 0);
                if ((buffer[5] & 0b11111000) != 0)
                    return 0;
                var read = 6;
                var fake = buffer[5];
                read = 6 - fake;
                lastPeace.CopyTo(buffer, 0);
                return read;
            }
            _cube.DoScramble().CopyTo(buffer, 0);
            return 6;
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
            if (LastWrite)
            {
                for (int i = 0; i < 5; i++)
                    buffer[i] = 255;
                buffer[5] = (byte)(6 - count);
                _cube.Init(buffer[0], buffer[1], buffer[2], buffer[3], buffer[4], buffer[5]);
                _stream.Write(_cube.DoScramble(), 0, 6);
            }
        }
    }

    internal class Cube1Bit
    {
        private byte[] _bytes = new byte[6];
        private List<Action> _moves = new List<Action>();
        private bool _decryptMode;
        private Random _rnd;
        private List<int> _nums = new List<int>();
        private int _numIndex;
        private int _startNumIndex;
        private int _offset;

        public Cube1Bit(bool decrypt, int seed)
        {
            _decryptMode = decrypt;
            _offset = _decryptMode ? -1 : 1;
            _rnd = new Random(seed);
        }

        public void Init(byte b1, byte b2, byte b3, byte b4, byte b5, byte b6)
        {
            _bytes = new[] { b1, b2, b3, b4, b5, b6 };
        }

        public void SetScramble(string scramble)
        {
            _moves.Clear();
            int i = 0;
            Action<Action, Action> addMove = (Action move, Action antiMove) =>
            {
                if (i + 1 == scramble.Length)
                    _moves.Add(move);
                else if (scramble[i + 1] == '\'')
                {
                    _moves.Add(antiMove);
                    ++i;
                }
                else if (scramble[i + 1] == '2')
                {
                    if (_decryptMode)
                    {
                        _moves.Add(antiMove);
                        _moves.Add(antiMove);
                    }
                    else
                    {
                        _moves.Add(move);
                        _moves.Add(move);
                    }
                    ++i;
                }
                else
                    _moves.Add(move);
            };
            var dict = new Dictionary<char, Action>();
            dict['R'] = () => addMove(R, AntiR);
            dict['L'] = () => addMove(L, AntiL);
            dict['U'] = () => addMove(U, AntiU);
            dict['D'] = () => addMove(D, AntiD);
            dict['F'] = () => addMove(F, AntiF);
            dict['B'] = () => addMove(B, AntiB);
            dict['S'] = () => addMove(S, AntiS);
            dict['E'] = () => addMove(E, AntiE);
            dict['M'] = () => addMove(M, AntiM);
            for (i = 0; i < scramble.Length; ++i)
            {
                if (dict.ContainsKey(scramble[i]))
                {
                    dict[scramble[i]]();
                }
            }
            for (i = 0; i < _moves.Count; ++i)
                    _nums.Add(_rnd.Next(256));
            _startNumIndex = _decryptMode ? _nums.Count - 1 : 0;
        }

        public byte[] DoScramble()
        {
            _numIndex = _startNumIndex;
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
            byte tus, tfs, tbs, tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                tus = (byte)(_bytes[0] - mud);
                tfs = (byte)(_bytes[2] - mud);
                tbs = (byte)(_bytes[4] - mud);
                tds = (byte)(_bytes[5] - mud);

                _bytes[4] = (byte)(tbs & maskClear | maskGet & tus);

                _bytes[5] = (byte)(tds & maskClear | maskGet & tbs);

                _bytes[2] = (byte)(tfs & maskClear | maskGet & tds);

                _bytes[0] = (byte)(tus & maskClear | maskGet & tfs);
            }
            else
            {
                tus = _bytes[0];
                tfs = _bytes[2];
                tbs = _bytes[4];
                tds = _bytes[5];

                _bytes[4] = (byte)((tbs & maskClear | maskGet & tus) + mud);

                _bytes[5] = (byte)((tds & maskClear | maskGet & tbs) + mud);

                _bytes[2] = (byte)((tfs & maskClear | maskGet & tds) + mud);

                _bytes[0] = (byte)((tus & maskClear | maskGet & tfs) + mud);
            }
            _numIndex += _offset;
            //---------------------
            MoveSideClockwise(3);
        }

        private void L()
        {
            var maskGet = 0b00101001;
            var maskClear = 0b11010110;//check
            byte tus, tfs, tbs, tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                tus = (byte)(_bytes[0] - mud);
                tfs = (byte)(_bytes[2] - mud);
                tbs = (byte)(_bytes[4] - mud);
                tds = (byte)(_bytes[5] - mud);
                _bytes[2] = (byte)(tfs & maskClear | maskGet & tus);

                _bytes[5] = (byte)(tds & maskClear | maskGet & tfs);

                _bytes[4] = (byte)(tbs & maskClear | maskGet & tds);

                _bytes[0] = (byte)(tus & maskClear | maskGet & tbs);
            }
            else
            {
                tus = _bytes[0];
                tfs = _bytes[2];
                tbs = _bytes[4];
                tds = _bytes[5];
                _bytes[2] = (byte)((tfs & maskClear | maskGet & tus) + mud);

                _bytes[5] = (byte)((tds & maskClear | maskGet & tfs) + mud);

                _bytes[4] = (byte)((tbs & maskClear | maskGet & tds) + mud);

                _bytes[0] = (byte)((tus & maskClear | maskGet & tbs) + mud);
            }
            _numIndex += _offset;
            //---------------------
            MoveSideClockwise(1);
        }

        private void F()
        {
            var maskClearForR = 0b11010110;
            var maskClearForD = 0b11111000;
            var maskClearForL = 0b01101011;
            var maskClearForU = 0b00011111;
            byte tus, tls, trs, tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                tus = (byte)(_bytes[0] - mud);
                tls = (byte)(_bytes[1] - mud);
                trs = (byte)(_bytes[3] - mud);
                tds = (byte)(_bytes[5] - mud);
                _bytes[3] = (byte)(trs & maskClearForR | tus >> 5 & 0b00000001 | tus >> 3 & 0b00001000 | tus >> 2 & 0b00100000);

                _bytes[5] = (byte)(tds & maskClearForD | trs >> 5 & 0b00000001 | trs >> 2 & 0b00000010 | trs << 2 & 0b00000100);

                _bytes[1] = (byte)(tls & maskClearForL | tds << 2 & 0b00000100 | tds << 3 & 0b00010000 | tds << 5 & 0b10000000);

                _bytes[0] = (byte)(tus & maskClearForU | tls << 5 & 0b10000000 | tls << 2 & 0b01000000 | tls >> 2 & 0b00100000);
            }
            else
            {
                tus = _bytes[0];
                tls = _bytes[1];
                trs = _bytes[3];
                tds = _bytes[5];
                _bytes[3] = (byte)((trs & maskClearForR | tus >> 5 & 0b00000001 | tus >> 3 & 0b00001000 | tus >> 2 & 0b00100000) + mud);

                _bytes[5] = (byte)((tds & maskClearForD | trs >> 5 & 0b00000001 | trs >> 2 & 0b00000010 | trs << 2 & 0b00000100) + mud);

                _bytes[1] = (byte)((tls & maskClearForL | tds << 2 & 0b00000100 | tds << 3 & 0b00010000 | tds << 5 & 0b10000000) + mud);

                _bytes[0] = (byte)((tus & maskClearForU | tls << 5 & 0b10000000 | tls << 2 & 0b01000000 | tls >> 2 & 0b00100000) + mud);
            }
            _numIndex += _offset;
            //---------------------
            MoveSideClockwise(2);
        }

        private void B()
        {
            var maskClearForR = 0b01101011;
            var maskClearForD = 0b00011111;
            var maskClearForL = 0b11010110;
            var maskClearForU = 0b11111000;
            byte tus, tls, trs, tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                tus = (byte)(_bytes[0] - mud);
                tls = (byte)(_bytes[1] - mud);
                trs = (byte)(_bytes[3] - mud);
                tds = (byte)(_bytes[5] - mud);
                _bytes[3] = (byte)(trs & maskClearForR | tds >> 5 & 0b00000100 | tds >> 2 & 0b00010000 | tds << 2 & 0b10000000);

                _bytes[5] = (byte)(tds & maskClearForD | tls << 2 & 0b10000000 | tls << 3 & 0b01000000 | tls << 5 & 0b00100000);

                _bytes[1] = (byte)(tls & maskClearForL | tus << 5 & 0b00100000 | tus << 2 & 0b00001000 | tus >> 2 & 0b00000001);

                _bytes[0] = (byte)(tus & maskClearForU | trs >> 2 & 0b00000001 | trs >> 3 & 0b00000010 | trs >> 5 & 0b00000100);
            }
            else
            {
                tus = _bytes[0];
                tls = _bytes[1];
                trs = _bytes[3];
                tds = _bytes[5];
                _bytes[3] = (byte)((trs & maskClearForR | tds >> 5 & 0b00000100 | tds >> 2 & 0b00010000 | tds << 2 & 0b10000000) + mud);

                _bytes[5] = (byte)((tds & maskClearForD | tls << 2 & 0b10000000 | tls << 3 & 0b01000000 | tls << 5 & 0b00100000) + mud);

                _bytes[1] = (byte)((tls & maskClearForL | tus << 5 & 0b00100000 | tus << 2 & 0b00001000 | tus >> 2 & 0b00000001) + mud);

                _bytes[0] = (byte)((tus & maskClearForU | trs >> 2 & 0b00000001 | trs >> 3 & 0b00000010 | trs >> 5 & 0b00000100) + mud);
            }
            _numIndex += _offset;
            //---------------------
            MoveSideClockwise(4);
        }

        private void U()
        {
            var maskClearForRFL = 0b11111000;
            var maskClearForB = 0b00011111;
            byte trs, tls, tfs, tbs;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                trs = (byte)(_bytes[3] - mud);
                tls = (byte)(_bytes[1] - mud);
                tfs = (byte)(_bytes[2] - mud);
                tbs = (byte)(_bytes[4] - mud);
                _bytes[3] = (byte)(trs & maskClearForRFL | tbs >> 7 & 0b00000001 | tbs >> 5 & 0b00000010 | tbs >> 3 & 0b00000100);

                _bytes[4] = (byte)(tbs & maskClearForB | tls << 7 & 0b10000000 | tls << 5 & 0b01000000 | tls << 3 & 0b00100000);

                _bytes[1] = (byte)(tls & maskClearForRFL | tfs & 0b00000111);

                _bytes[2] = (byte)(tfs & maskClearForRFL | trs & 0b00000111);
            }
            else
            {
                trs = _bytes[3];
                tls = _bytes[1];
                tfs = _bytes[2];
                tbs = _bytes[4];
                _bytes[3] = (byte)((trs & maskClearForRFL | tbs >> 7 & 0b00000001 | tbs >> 5 & 0b00000010 | tbs >> 3 & 0b00000100) + mud);

                _bytes[4] = (byte)((tbs & maskClearForB | tls << 7 & 0b10000000 | tls << 5 & 0b01000000 | tls << 3 & 0b00100000) + mud);

                _bytes[1] = (byte)((tls & maskClearForRFL | tfs & 0b00000111) + mud);

                _bytes[2] = (byte)((tfs & maskClearForRFL | trs & 0b00000111) + mud);
            }
            _numIndex += _offset;
            //---------------------
            MoveSideClockwise(0);
        }

        private void D()
        {
            var maskClearForRFL = 0b00011111;
            var maskClearForB = 0b11111000;
            byte trs, tls, tfs, tbs;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                trs = (byte)(_bytes[3] - mud);
                tls = (byte)(_bytes[1] - mud);
                tfs = (byte)(_bytes[2] - mud);
                tbs = (byte)(_bytes[4] - mud);
                _bytes[1] = (byte)(tls & maskClearForRFL | tbs << 7 & 0b10000000 | tbs << 5 & 0b01000000 | tbs << 3 & 0b00100000);

                _bytes[4] = (byte)(tbs & maskClearForB | trs >> 7 & 0b00000001 | trs >> 5 & 0b00000010 | trs >> 3 & 0b00000100);

                _bytes[3] = (byte)(trs & maskClearForRFL | tfs & 0b11100000);

                _bytes[2] = (byte)(tfs & maskClearForRFL | tls & 0b11100000);
            }
            else
            {
                trs = _bytes[3];
                tls = _bytes[1];
                tfs = _bytes[2];
                tbs = _bytes[4];
                _bytes[1] = (byte)((tls & maskClearForRFL | tbs << 7 & 0b10000000 | tbs << 5 & 0b01000000 | tbs << 3 & 0b00100000) + mud);

                _bytes[4] = (byte)((tbs & maskClearForB | trs >> 7 & 0b00000001 | trs >> 5 & 0b00000010 | trs >> 3 & 0b00000100) + mud);

                _bytes[3] = (byte)((trs & maskClearForRFL | tfs & 0b11100000) + mud);

                _bytes[2] = (byte)((tfs & maskClearForRFL | tls & 0b11100000) + mud);
            }
            _numIndex += _offset;
            //---------------------
            MoveSideClockwise(5);
        }

        private void AntiR()
        {
            var maskGet = 0b10010100;
            var maskClear = 0b01101011;
            byte tus, tfs, tbs, tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                tus = (byte)(_bytes[0] - mud);
                tfs = (byte)(_bytes[2] - mud);
                tbs = (byte)(_bytes[4] - mud);
                tds = (byte)(_bytes[5] - mud);

                _bytes[2] = (byte)(tfs & maskClear | maskGet & tus);

                _bytes[5] = (byte)(tds & maskClear | maskGet & tfs);

                _bytes[4] = (byte)(tbs & maskClear | maskGet & tds);

                _bytes[0] = (byte)(tus & maskClear | maskGet & tbs);
            }
            else
            {
                tus = _bytes[0];
                tfs = _bytes[2];
                tbs = _bytes[4];
                tds = _bytes[5];

                _bytes[2] = (byte)((tfs & maskClear | maskGet & tus) + mud);

                _bytes[5] = (byte)((tds & maskClear | maskGet & tfs) + mud);

                _bytes[4] = (byte)((tbs & maskClear | maskGet & tds) + mud);

                _bytes[0] = (byte)((tus & maskClear | maskGet & tbs) + mud);
            }
            _numIndex += _offset;
            //---------------------
            MoveSideCounterClockwise(3);
        }

        private void AntiL()
        {
            var maskGet = 0b00101001;
            var maskClear = 0b11010110;//check
            byte tus, tfs, tbs, tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                tus = (byte)(_bytes[0] - mud);
                tfs = (byte)(_bytes[2] - mud);
                tbs = (byte)(_bytes[4] - mud);
                tds = (byte)(_bytes[5] - mud);
                _bytes[2] = (byte)(tfs & maskClear | maskGet & tds);

                _bytes[0] = (byte)(tus & maskClear | maskGet & tfs);

                _bytes[4] = (byte)(tbs & maskClear | maskGet & tus);

                _bytes[5] = (byte)(tds & maskClear | maskGet & tbs);
            }
            else
            {
                tus = _bytes[0];
                tfs = _bytes[2];
                tbs = _bytes[4];
                tds = _bytes[5];
                _bytes[2] = (byte)((tfs & maskClear | maskGet & tds) + mud);

                _bytes[0] = (byte)((tus & maskClear | maskGet & tfs) + mud);

                _bytes[4] = (byte)((tbs & maskClear | maskGet & tus) + mud);

                _bytes[5] = (byte)((tds & maskClear | maskGet & tbs) + mud);
            }
            _numIndex += _offset;
            //---------------------
            MoveSideCounterClockwise(1);
        }

        private void AntiF()
        {
            var maskClearForR = 0b11010110;
            var maskClearForD = 0b11111000;
            var maskClearForL = 0b01101011;
            var maskClearForU = 0b00011111;
            byte tus, tls, trs, tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                tus = (byte)(_bytes[0] - mud);
                tls = (byte)(_bytes[1] - mud);
                trs = (byte)(_bytes[3] - mud);
                tds = (byte)(_bytes[5] - mud);

                _bytes[1] = (byte)(tls & maskClearForL | tus << 2 & 0b10000000 | tus >> 2 & 0b00010000 | tus >> 5 & 0b00000100);

                _bytes[5] = (byte)(tds & maskClearForD | tls >> 5 & 0b00000100 | tls >> 3 & 0b00000010 | tls >> 2 & 0b00000001);

                _bytes[3] = (byte)(trs & maskClearForR | tds >> 2 & 0b00000001 | tds << 2 & 0b00001000 | tds << 5 & 0b00100000);

                _bytes[0] = (byte)(tus & maskClearForU | trs << 5 & 0b00100000 | trs << 3 & 0b01000000 | trs << 2 & 0b10000000);
            }
            else
            {
                tus = _bytes[0];
                tls = _bytes[1];
                trs = _bytes[3];
                tds = _bytes[5];

                _bytes[1] = (byte)((tls & maskClearForL | tus << 2 & 0b10000000 | tus >> 2 & 0b00010000 | tus >> 5 & 0b00000100) + mud);

                _bytes[5] = (byte)((tds & maskClearForD | tls >> 5 & 0b00000100 | tls >> 3 & 0b00000010 | tls >> 2 & 0b00000001) + mud);

                _bytes[3] = (byte)((trs & maskClearForR | tds >> 2 & 0b00000001 | tds << 2 & 0b00001000 | tds << 5 & 0b00100000) + mud);

                _bytes[0] = (byte)((tus & maskClearForU | trs << 5 & 0b00100000 | trs << 3 & 0b01000000 | trs << 2 & 0b10000000) + mud);
            }
            _numIndex += _offset;
            //---------------------
            MoveSideCounterClockwise(2);
        }

        private void AntiB()
        {
            var maskClearForR = 0b01101011;
            var maskClearForD = 0b00011111;
            var maskClearForL = 0b11010110;
            var maskClearForU = 0b11111000;
            byte tus, tls, trs, tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                tus = (byte)(_bytes[0] - mud);
                tls = (byte)(_bytes[1] - mud);
                trs = (byte)(_bytes[3] - mud);
                tds = (byte)(_bytes[5] - mud);
                _bytes[3] = (byte)(trs & maskClearForR | tus << 5 & 0b10000000 | tus << 3 & 0b00010000 | tus << 2 & 0b00000100);

                _bytes[5] = (byte)(tds & maskClearForD | trs >> 2 & 0b00100000 | trs << 2 & 0b01000000 | trs << 5 & 0b10000000);

                _bytes[1] = (byte)(tls & maskClearForL | tds >> 5 & 0b00000001 | tds >> 3 & 0b00001000 | tds >> 2 & 0b00100000);

                _bytes[0] = (byte)(tus & maskClearForU | tls << 2 & 0b00000100 | tls >> 2 & 0b00000010 | tls >> 5 & 0b00000001);
            }
            else
            {
                tus = _bytes[0];
                tls = _bytes[1];
                trs = _bytes[3];
                tds = _bytes[5];
                _bytes[3] = (byte)((trs & maskClearForR | tus << 5 & 0b10000000 | tus << 3 & 0b00010000 | tus << 2 & 0b00000100) + mud);

                _bytes[5] = (byte)((tds & maskClearForD | trs >> 2 & 0b00100000 | trs << 2 & 0b01000000 | trs << 5 & 0b10000000) + mud);

                _bytes[1] = (byte)((tls & maskClearForL | tds >> 5 & 0b00000001 | tds >> 3 & 0b00001000 | tds >> 2 & 0b00100000) + mud);

                _bytes[0] = (byte)((tus & maskClearForU | tls << 2 & 0b00000100 | tls >> 2 & 0b00000010 | tls >> 5 & 0b00000001) + mud);
            }
            _numIndex += _offset;
            //---------------------
            MoveSideCounterClockwise(4);
        }

        private void AntiU()
        {
            var maskClearForRFL = 0b11111000;
            var maskClearForB = 0b00011111;
            byte trs, tls, tfs, tbs;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                trs = (byte)(_bytes[3] - mud);
                tls = (byte)(_bytes[1] - mud);
                tfs = (byte)(_bytes[2] - mud);
                tbs = (byte)(_bytes[4] - mud);
                _bytes[1] = (byte)(tls & maskClearForRFL | tbs >> 3 & 0b00000100 | tbs >> 5 & 0b00000010 | tbs >> 7 & 0b00000001);

                _bytes[2] = (byte)(tfs & maskClearForRFL | tls & 0b00000111);

                _bytes[3] = (byte)(trs & maskClearForRFL | tfs & 0b00000111);

                _bytes[4] = (byte)(tbs & maskClearForB | trs << 3 & 0b00100000 | trs << 5 & 0b01000000 | trs << 7 & 0b10000000);
            }
            else
            {
                trs = _bytes[3];
                tls = _bytes[1];
                tfs = _bytes[2];
                tbs = _bytes[4];
                _bytes[1] = (byte)((tls & maskClearForRFL | tbs >> 3 & 0b00000100 | tbs >> 5 & 0b00000010 | tbs >> 7 & 0b00000001) + mud);

                _bytes[2] = (byte)((tfs & maskClearForRFL | tls & 0b00000111) + mud);

                _bytes[3] = (byte)((trs & maskClearForRFL | tfs & 0b00000111) + mud);

                _bytes[4] = (byte)((tbs & maskClearForB | trs << 3 & 0b00100000 | trs << 5 & 0b01000000 | trs << 7 & 0b10000000) + mud);
            }
            _numIndex += _offset;
            //---------------------
            MoveSideCounterClockwise(0);
        }

        private void AntiD()
        {
            var maskClearForRFL = 0b00011111;
            var maskClearForB = 0b11111000;
            byte trs, tls, tfs, tbs;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                trs = (byte)(_bytes[3] - mud);
                tls = (byte)(_bytes[1] - mud);
                tfs = (byte)(_bytes[2] - mud);
                tbs = (byte)(_bytes[4] - mud);
                _bytes[1] = (byte)(tls & maskClearForRFL | tfs & 0b11100000);

                _bytes[4] = (byte)(tbs & maskClearForB | tls >> 3 & 0b00000100 | tls >> 5 & 0b00000010 | tls >> 7 & 0b00000001);

                _bytes[3] = (byte)(trs & maskClearForRFL | tbs << 3 & 0b00100000 | tbs << 5 & 0b01000000 | tbs << 7 & 0b10000000);

                _bytes[2] = (byte)(tfs & maskClearForRFL | trs & 0b11100000);
            }
            else
            {
                trs = _bytes[3];
                tls = _bytes[1];
                tfs = _bytes[2];
                tbs = _bytes[4];
                _bytes[1] = (byte)((tls & maskClearForRFL | tfs & 0b11100000) + mud);

                _bytes[4] = (byte)((tbs & maskClearForB | tls >> 3 & 0b00000100 | tls >> 5 & 0b00000010 | tls >> 7 & 0b00000001) + mud);

                _bytes[3] = (byte)((trs & maskClearForRFL | tbs << 3 & 0b00100000 | tbs << 5 & 0b01000000 | tbs << 7 & 0b10000000) + mud);

                _bytes[2] = (byte)((tfs & maskClearForRFL | trs & 0b11100000) + mud);
            }
            _numIndex += _offset;
            //---------------------
            MoveSideCounterClockwise(5);
        }

        private void M()
        {
            var maskClear = 0b10111101;
            var maskGet = 0b01000010;
            byte tus, tfs, tbs, tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                tus = (byte)(_bytes[0] - mud);
                tfs = (byte)(_bytes[2] - mud);
                tbs = (byte)(_bytes[4] - mud);
                tds = (byte)(_bytes[5] - mud);

                _bytes[0] = (byte)(tus & maskClear | tbs & maskGet);

                _bytes[4] = (byte)(tbs & maskClear | tds & maskGet);

                _bytes[5] = (byte)(tds & maskClear | tfs & maskGet);

                _bytes[2] = (byte)(tfs & maskClear | tus & maskGet);
            }
            else
            {
                tus = _bytes[0];
                tfs = _bytes[2];
                tbs = _bytes[4];
                tds = _bytes[5];

                _bytes[0] = (byte)((tus & maskClear | tbs & maskGet) + mud);

                _bytes[4] = (byte)((tbs & maskClear | tds & maskGet) + mud);

                _bytes[5] = (byte)((tds & maskClear | tfs & maskGet) + mud);

                _bytes[2] = (byte)((tfs & maskClear | tus & maskGet) + mud);
            }
            _numIndex += _offset;
        }

        private void AntiM()
        {
            var maskClear = 0b10111101;
            var maskGet = 0b01000010;
            byte tus, tfs, tbs, tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                tus = (byte)(_bytes[0] - mud);
                tfs = (byte)(_bytes[2] - mud);
                tbs = (byte)(_bytes[4] - mud);
                tds = (byte)(_bytes[5] - mud);

                _bytes[0] = (byte)(tus & maskClear | tfs & maskGet);

                _bytes[4] = (byte)(tbs & maskClear | tus & maskGet);

                _bytes[5] = (byte)(tds & maskClear | tbs & maskGet);

                _bytes[2] = (byte)(tfs & maskClear | tds & maskGet);
            }
            else
            {
                tus = _bytes[0];
                tfs = _bytes[2];
                tbs = _bytes[4];
                tds = _bytes[5];

                _bytes[0] = (byte)((tus & maskClear | tfs & maskGet) + mud);

                _bytes[4] = (byte)((tbs & maskClear | tus & maskGet) + mud);

                _bytes[5] = (byte)((tds & maskClear | tbs & maskGet) + mud);

                _bytes[2] = (byte)((tfs & maskClear | tds & maskGet) + mud);
            }
            _numIndex += _offset;
        }

        private void S()
        {
            var maskClearUD = 0b11100111;
            var maskClearRL = 0b10111101;
            byte tus, tls, trs, tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                tus = (byte)(_bytes[0] - mud);
                tls = (byte)(_bytes[1] - mud);
                trs = (byte)(_bytes[3] - mud);
                tds = (byte)(_bytes[5] - mud);

                _bytes[0] = (byte)(tus & maskClearUD | tls << 3 & 0b00010000 | tls >> 3 & 0b00001000);

                _bytes[3] = (byte)(trs & maskClearRL | tus << 2 & 0b01000000 | tus >> 2 & 0b00000010);

                _bytes[5] = (byte)(tds & maskClearUD | trs >> 3 & 0b00001000 | trs << 3 & 0b00010000);

                _bytes[1] = (byte)(tls & maskClearRL | tds >> 2 & 0b00000010 | tds << 2 & 0b01000000);
            }
            else
            {
                tus = _bytes[0];
                tls = _bytes[1];
                trs = _bytes[3];
                tds = _bytes[5];

                _bytes[0] = (byte)((tus & maskClearUD | tls << 3 & 0b00010000 | tls >> 3 & 0b00001000) + mud);

                _bytes[3] = (byte)((trs & maskClearRL | tus << 2 & 0b01000000 | tus >> 2 & 0b00000010) + mud);

                _bytes[5] = (byte)((tds & maskClearUD | trs >> 3 & 0b00001000 | trs << 3 & 0b00010000) + mud);

                _bytes[1] = (byte)((tls & maskClearRL | tds >> 2 & 0b00000010 | tds << 2 & 0b01000000) + mud);
            }
            _numIndex += _offset;
        }

        private void AntiS()
        {
            var maskClearUD = 0b11100111;
            var maskClearRL = 0b10111101;
            byte tus, tls, trs, tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                tus = (byte)(_bytes[0] - mud);
                tls = (byte)(_bytes[1] - mud);
                trs = (byte)(_bytes[3] - mud);
                tds = (byte)(_bytes[5] - mud);
                _bytes[0] = (byte)(tus & maskClearUD | trs << 2 & 0b00001000 | trs >> 2 & 0b00010000);

                _bytes[1] = (byte)(tls & maskClearRL | tus << 3 & 0b01000000 | tus >> 3 & 0b00000010);

                _bytes[5] = (byte)(tds & maskClearUD | tls >> 2 & 0b00010000 | tls << 2 & 0b00001000);

                _bytes[3] = (byte)(trs & maskClearRL | tds >> 3 & 0b00000010 | tds << 3 & 0b01000000);
            }
            else
            {
                tus = _bytes[0];
                tls = _bytes[1];
                trs = _bytes[3];
                tds = _bytes[5];
                _bytes[0] = (byte)((tus & maskClearUD | trs << 2 & 0b00001000 | trs >> 2 & 0b00010000) + mud);

                _bytes[1] = (byte)((tls & maskClearRL | tus << 3 & 0b01000000 | tus >> 3 & 0b00000010) + mud);

                _bytes[5] = (byte)((tds & maskClearUD | tls >> 2 & 0b00010000 | tls << 2 & 0b00001000) + mud);

                _bytes[3] = (byte)((trs & maskClearRL | tds >> 3 & 0b00000010 | tds << 3 & 0b01000000) + mud);
            }
            _numIndex += _offset;
        }

        private void E()
        {
            var maskClear = 0b11100111;
            var maskGetRFL = 0b00011000;
            byte trs, tls, tfs, tbs;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                trs = (byte)(_bytes[3] - mud);
                tls = (byte)(_bytes[1] - mud);
                tfs = (byte)(_bytes[2] - mud);
                tbs = (byte)(_bytes[4] - mud);

                _bytes[2] = (byte)(tfs & maskClear | tls & maskGetRFL);

                _bytes[3] = (byte)(trs & maskClear | tfs & maskGetRFL);

                _bytes[4] = (byte)(tbs & maskClear | trs >> 1 & 0b00001000 | trs << 1 & 0b00010000);

                _bytes[1] = (byte)(tls & maskClear | tbs << 1 & 0b00010000 | tbs >> 1 & 0b00001000);
            }
            else
            {
                trs = _bytes[3];
                tls = _bytes[1];
                tfs = _bytes[2];
                tbs = _bytes[4];

                _bytes[2] = (byte)((tfs & maskClear | tls & maskGetRFL) + mud);

                _bytes[3] = (byte)((trs & maskClear | tfs & maskGetRFL) + mud);

                _bytes[4] = (byte)((tbs & maskClear | trs >> 1 & 0b00001000 | trs << 1 & 0b00010000) + mud);

                _bytes[1] = (byte)((tls & maskClear | tbs << 1 & 0b00010000 | tbs >> 1 & 0b00001000) + mud);
            }
            _numIndex += _offset;
        }

        private void AntiE()
        {
            var maskClear = 0b11100111;
            var maskGetRFL = 0b00011000;
            byte trs, tls, tfs, tbs;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                trs = (byte)(_bytes[3] - mud);
                tls = (byte)(_bytes[1] - mud);
                tfs = (byte)(_bytes[2] - mud);
                tbs = (byte)(_bytes[4] - mud);
                _bytes[2] = (byte)(tfs & maskClear | trs & maskGetRFL);

                _bytes[1] = (byte)(tls & maskClear | tfs & maskGetRFL);

                _bytes[4] = (byte)(tbs & maskClear | tls << 1 & 0b00010000 | tls >> 1 & 0b00001000);

                _bytes[3] = (byte)(trs & maskClear | tbs >> 1 & 0b00001000 | tbs << 1 & 0b00010000);
            }
            else
            {
                trs = _bytes[3];
                tls = _bytes[1];
                tfs = _bytes[2];
                tbs = _bytes[4];
                _bytes[2] = (byte)((tfs & maskClear | trs & maskGetRFL) + mud);

                _bytes[1] = (byte)((tls & maskClear | tfs & maskGetRFL) + mud);

                _bytes[4] = (byte)((tbs & maskClear | tls << 1 & 0b00010000 | tls >> 1 & 0b00001000) + mud);

                _bytes[3] = (byte)((trs & maskClear | tbs >> 1 & 0b00001000 | tbs << 1 & 0b00010000) + mud);
            }
            _numIndex += _offset;
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