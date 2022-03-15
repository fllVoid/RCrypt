namespace RCryptLib
{
    internal class RCryptStream4Bit : Stream
    {
        private Stream _stream;
        private Cube4Bit _cube;
        private bool _read;
        public bool LastWrite { get; set; }

        public RCryptStream4Bit(Stream baseStream, bool read, string scramble)
        {
            _stream = baseStream;
            _read = read;
            _cube = new Cube4Bit(read);
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
            _stream.Read(buffer, offset, 24);
            var cubeBytes = new CubeBytes
            {
                U = new SideBytes { b1 = buffer[0], b2 = buffer[1], b3 = buffer[2], b4 = buffer[3] },
                L = new SideBytes { b1 = buffer[4], b2 = buffer[5], b3 = buffer[6], b4 = buffer[7] },
                F = new SideBytes { b1 = buffer[8], b2 = buffer[9], b3 = buffer[10], b4 = buffer[11] },
                R = new SideBytes { b1 = buffer[12], b2 = buffer[13], b3 = buffer[14], b4 = buffer[15] },
                B = new SideBytes { b1 = buffer[16], b2 = buffer[17], b3 = buffer[18], b4 = buffer[19] },
                D = new SideBytes { b1 = buffer[20], b2 = buffer[21], b3 = buffer[22], b4 = buffer[23] },
            };
            _cube.Init(ref cubeBytes);
            if (_stream.Length <= _stream.Position + 24)
            {
                var lastPeace = new byte[24];
                _cube.DoScramble(lastPeace);
                _stream.Read(buffer, offset, 24);
                cubeBytes = new CubeBytes
                {
                    U = new SideBytes { b1 = buffer[0], b2 = buffer[1], b3 = buffer[2], b4 = buffer[3] },
                    L = new SideBytes { b1 = buffer[4], b2 = buffer[5], b3 = buffer[6], b4 = buffer[7] },
                    F = new SideBytes { b1 = buffer[8], b2 = buffer[9], b3 = buffer[10], b4 = buffer[11] },
                    R = new SideBytes { b1 = buffer[12], b2 = buffer[13], b3 = buffer[14], b4 = buffer[15] },
                    B = new SideBytes { b1 = buffer[16], b2 = buffer[17], b3 = buffer[18], b4 = buffer[19] },
                    D = new SideBytes { b1 = buffer[20], b2 = buffer[21], b3 = buffer[22], b4 = buffer[23] },
                };
                _cube.Init(ref cubeBytes);
                _cube.DoScramble(buffer);
                if ((buffer[23] & 0b11100000) != 0)
                    return 0;
                var read = 24;
                var fake = buffer[23];
                read = 24 - fake;
                lastPeace.CopyTo(buffer, 0);
                return read;
            }
            _cube.DoScramble(buffer);
            return 24;
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
            var cubeBytes = new CubeBytes
            {
                U = new SideBytes { b1 = buffer[0], b2 = buffer[1], b3 = buffer[2], b4 = buffer[3] },
                L = new SideBytes { b1 = buffer[4], b2 = buffer[5], b3 = buffer[6], b4 = buffer[7] },
                F = new SideBytes { b1 = buffer[8], b2 = buffer[9], b3 = buffer[10], b4 = buffer[11] },
                R = new SideBytes { b1 = buffer[12], b2 = buffer[13], b3 = buffer[14], b4 = buffer[15] },
                B = new SideBytes { b1 = buffer[16], b2 = buffer[17], b3 = buffer[18], b4 = buffer[19] },
                D = new SideBytes { b1 = buffer[20], b2 = buffer[21], b3 = buffer[22], b4 = buffer[23] },
            };
            _cube.Init(ref cubeBytes);
            _stream.Write(_cube.DoScramble(), 0, 24);
            if (LastWrite)
            {
                for (int i = 0; i < 23; i++)
                    buffer[i] = 255;
                buffer[23] = (byte)(24 - count);
                cubeBytes = new CubeBytes
                {
                    U = new SideBytes { b1 = buffer[0], b2 = buffer[1], b3 = buffer[2], b4 = buffer[3] },
                    L = new SideBytes { b1 = buffer[4], b2 = buffer[5], b3 = buffer[6], b4 = buffer[7] },
                    F = new SideBytes { b1 = buffer[8], b2 = buffer[9], b3 = buffer[10], b4 = buffer[11] },
                    R = new SideBytes { b1 = buffer[12], b2 = buffer[13], b3 = buffer[14], b4 = buffer[15] },
                    B = new SideBytes { b1 = buffer[16], b2 = buffer[17], b3 = buffer[18], b4 = buffer[19] },
                    D = new SideBytes { b1 = buffer[20], b2 = buffer[21], b3 = buffer[22], b4 = buffer[23] },
                };
                _cube.Init(ref cubeBytes);
                _stream.Write(_cube.DoScramble(), 0, 24);
            }
        }
    }

    internal class Cube4Bit
    {
        private CubeBytes _bytes;
        private byte[] _resultBytes = new byte[24];
        private List<Action> _moves = new List<Action>();
        private bool _decryptMode;
        private Random? _rnd;
        private List<int> _nums = new List<int>();
        private int _numIndex;
        private int _startNumIndex;
        private int _offset;

        public Cube4Bit(bool decrypt)
        {
            _decryptMode = decrypt;
            _offset = _decryptMode ? -1 : 1;
        }

        public void Init(ref CubeBytes bytes) => _bytes = bytes;

        public void SetScramble(string scramble)
        {
            _moves.Clear();
            int i = 0;
            var sum = 0;
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
                    sum += scramble[i];
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
                    sum += scramble[i];
                    dict[scramble[i]]();
                }
            }
            _rnd = new Random(sum);
            for (i = 0; i < scramble.Length; ++i)
                if (dict.ContainsKey(scramble[i]))
                {
                    _nums.Add(_rnd.Next(256));
                    if (i + 1 < scramble.Length && scramble[i + 1] == '2')
                        _nums.Add(_rnd.Next(256));
                }
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
            _resultBytes[0] = _bytes.U.b1;
            _resultBytes[1] = _bytes.U.b2;
            _resultBytes[2] = _bytes.U.b3;
            _resultBytes[3] = _bytes.U.b4;

            _resultBytes[4] = _bytes.L.b1;
            _resultBytes[5] = _bytes.L.b2;
            _resultBytes[6] = _bytes.L.b3;
            _resultBytes[7] = _bytes.L.b4;

            _resultBytes[8] = _bytes.F.b1;
            _resultBytes[9] = _bytes.F.b2;
            _resultBytes[10] = _bytes.F.b3;
            _resultBytes[11] = _bytes.F.b4;

            _resultBytes[12] = _bytes.R.b1;
            _resultBytes[13] = _bytes.R.b2;
            _resultBytes[14] = _bytes.R.b3;
            _resultBytes[15] = _bytes.R.b4;

            _resultBytes[16] = _bytes.B.b1;
            _resultBytes[17] = _bytes.B.b2;
            _resultBytes[18] = _bytes.B.b3;
            _resultBytes[19] = _bytes.B.b4;

            _resultBytes[20] = _bytes.D.b1;
            _resultBytes[21] = _bytes.D.b2;
            _resultBytes[22] = _bytes.D.b3;
            _resultBytes[23] = _bytes.D.b4;
            return _resultBytes;
        }

        public void DoScramble(byte[] bytes)
        {
            _numIndex = _startNumIndex;
            var count = _moves.Count;
            for (int i = 0; i < count; ++i)
            {
                _moves[i]();
            }
            bytes[0] = _bytes.U.b1;
            bytes[1] = _bytes.U.b2;
            bytes[2] = _bytes.U.b3;
            bytes[3] = _bytes.U.b4;

            bytes[4] = _bytes.L.b1;
            bytes[5] = _bytes.L.b2;
            bytes[6] = _bytes.L.b3;
            bytes[7] = _bytes.L.b4;

            bytes[8] = _bytes.F.b1;
            bytes[9] = _bytes.F.b2;
            bytes[10] = _bytes.F.b3;
            bytes[11] = _bytes.F.b4;

            bytes[12] = _bytes.R.b1;
            bytes[13] = _bytes.R.b2;
            bytes[14] = _bytes.R.b3;
            bytes[15] = _bytes.R.b4;

            bytes[16] = _bytes.B.b1;
            bytes[17] = _bytes.B.b2;
            bytes[18] = _bytes.B.b3;
            bytes[19] = _bytes.B.b4;

            bytes[20] = _bytes.D.b1;
            bytes[21] = _bytes.D.b2;
            bytes[22] = _bytes.D.b3;
            bytes[23] = _bytes.D.b4;
        }

        private void R()
        {
            SideBytes tus;
            SideBytes tfs;
            SideBytes tbs;
            SideBytes tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.U.b2 -= mud;
                _bytes.U.b3 -= mud;
                _bytes.U.b4 -= mud;

                _bytes.F.b2 -= mud;
                _bytes.F.b3 -= mud;
                _bytes.F.b4 -= mud;

                _bytes.B.b2 -= mud;
                _bytes.B.b3 -= mud;
                _bytes.B.b4 -= mud;

                _bytes.D.b2 -= mud;
                _bytes.D.b3 -= mud;
                _bytes.D.b4 -= mud;
                tus = _bytes.U;
                tfs = _bytes.F;
                tbs = _bytes.B;
                tds = _bytes.D;

                _bytes.U.b2 &= 0b11110000;
                _bytes.U.b3 &= 0b11110000;
                _bytes.U.b4 &= 0b00001111;
                _bytes.U.b2 |= (byte)(tfs.b2 & 0b00001111);
                _bytes.U.b3 |= (byte)(tfs.b3 & 0b00001111);
                _bytes.U.b4 |= (byte)(tfs.b4 & 0b11110000);

                _bytes.B.b2 &= 0b11110000;
                _bytes.B.b3 &= 0b11110000;
                _bytes.B.b4 &= 0b00001111;
                _bytes.B.b2 |= (byte)(tus.b2 & 0b00001111);
                _bytes.B.b3 |= (byte)(tus.b3 & 0b00001111);
                _bytes.B.b4 |= (byte)(tus.b4 & 0b11110000);

                _bytes.D.b2 &= 0b11110000;
                _bytes.D.b3 &= 0b11110000;
                _bytes.D.b4 &= 0b00001111;
                _bytes.D.b2 |= (byte)(tbs.b2 & 0b00001111);
                _bytes.D.b3 |= (byte)(tbs.b3 & 0b00001111);
                _bytes.D.b4 |= (byte)(tbs.b4 & 0b11110000);

                _bytes.F.b2 &= 0b11110000;
                _bytes.F.b3 &= 0b11110000;
                _bytes.F.b4 &= 0b00001111;
                _bytes.F.b2 |= (byte)(tds.b2 & 0b00001111);
                _bytes.F.b3 |= (byte)(tds.b3 & 0b00001111);
                _bytes.F.b4 |= (byte)(tds.b4 & 0b11110000);
            }
            else
            {
                tus = _bytes.U;
                tfs = _bytes.F;
                tbs = _bytes.B;
                tds = _bytes.D;
                _bytes.U.b2 &= 0b11110000;
                _bytes.U.b3 &= 0b11110000;
                _bytes.U.b4 &= 0b00001111;
                _bytes.U.b2 |= (byte)(tfs.b2 & 0b00001111);
                _bytes.U.b3 |= (byte)(tfs.b3 & 0b00001111);
                _bytes.U.b4 |= (byte)(tfs.b4 & 0b11110000);
                _bytes.U.b2 += mud;
                _bytes.U.b3 += mud;
                _bytes.U.b4 += mud;

                _bytes.B.b2 &= 0b11110000;
                _bytes.B.b3 &= 0b11110000;
                _bytes.B.b4 &= 0b00001111;
                _bytes.B.b2 |= (byte)(tus.b2 & 0b00001111);
                _bytes.B.b3 |= (byte)(tus.b3 & 0b00001111);
                _bytes.B.b4 |= (byte)(tus.b4 & 0b11110000);
                _bytes.B.b2 += mud;
                _bytes.B.b3 += mud;
                _bytes.B.b4 += mud;

                _bytes.D.b2 &= 0b11110000;
                _bytes.D.b3 &= 0b11110000;
                _bytes.D.b4 &= 0b00001111;
                _bytes.D.b2 |= (byte)(tbs.b2 & 0b00001111);
                _bytes.D.b3 |= (byte)(tbs.b3 & 0b00001111);
                _bytes.D.b4 |= (byte)(tbs.b4 & 0b11110000);
                _bytes.D.b2 += mud;
                _bytes.D.b3 += mud;
                _bytes.D.b4 += mud;

                _bytes.F.b2 &= 0b11110000;
                _bytes.F.b3 &= 0b11110000;
                _bytes.F.b4 &= 0b00001111;
                _bytes.F.b2 |= (byte)(tds.b2 & 0b00001111);
                _bytes.F.b3 |= (byte)(tds.b3 & 0b00001111);
                _bytes.F.b4 |= (byte)(tds.b4 & 0b11110000);
                _bytes.F.b2 += mud;
                _bytes.F.b3 += mud;
                _bytes.F.b4 += mud;
            }
            _numIndex += _offset;
            //---------------------
            MoveSideClockwise(ref _bytes.R);
        }

        private void L()
        {
            SideBytes tus;
            SideBytes tfs;
            SideBytes tbs;
            SideBytes tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.U.b1 -= mud;
                _bytes.U.b2 -= mud;
                _bytes.U.b3 -= mud;

                _bytes.F.b1 -= mud;
                _bytes.F.b2 -= mud;
                _bytes.F.b3 -= mud;

                _bytes.B.b1 -= mud;
                _bytes.B.b2 -= mud;
                _bytes.B.b3 -= mud;

                _bytes.D.b1 -= mud;
                _bytes.D.b2 -= mud;
                _bytes.D.b3 -= mud;
                tus = _bytes.U;
                tfs = _bytes.F;
                tbs = _bytes.B;
                tds = _bytes.D;

                _bytes.U.b1 &= 0b11110000;
                _bytes.U.b2 &= 0b00001111;
                _bytes.U.b3 &= 0b00001111;
                _bytes.U.b1 |= (byte)(tbs.b1 & 0b00001111);
                _bytes.U.b2 |= (byte)(tbs.b2 & 0b11110000);
                _bytes.U.b3 |= (byte)(tbs.b3 & 0b11110000);

                _bytes.F.b1 &= 0b11110000;
                _bytes.F.b2 &= 0b00001111;
                _bytes.F.b3 &= 0b00001111;
                _bytes.F.b1 |= (byte)(tus.b1 & 0b00001111);
                _bytes.F.b2 |= (byte)(tus.b2 & 0b11110000);
                _bytes.F.b3 |= (byte)(tus.b3 & 0b11110000);

                _bytes.D.b1 &= 0b11110000;
                _bytes.D.b2 &= 0b00001111;
                _bytes.D.b3 &= 0b00001111;
                _bytes.D.b1 |= (byte)(tfs.b1 & 0b00001111);
                _bytes.D.b2 |= (byte)(tfs.b2 & 0b11110000);
                _bytes.D.b3 |= (byte)(tfs.b3 & 0b11110000);

                _bytes.B.b1 &= 0b11110000;
                _bytes.B.b2 &= 0b00001111;
                _bytes.B.b3 &= 0b00001111;
                _bytes.B.b1 |= (byte)(tds.b1 & 0b00001111);
                _bytes.B.b2 |= (byte)(tds.b2 & 0b11110000);
                _bytes.B.b3 |= (byte)(tds.b3 & 0b11110000);
            }
            else
            {
                tus = _bytes.U;
                tfs = _bytes.F;
                tbs = _bytes.B;
                tds = _bytes.D;
                _bytes.U.b1 &= 0b11110000;
                _bytes.U.b2 &= 0b00001111;
                _bytes.U.b3 &= 0b00001111;
                _bytes.U.b1 |= (byte)(tbs.b1 & 0b00001111);
                _bytes.U.b2 |= (byte)(tbs.b2 & 0b11110000);
                _bytes.U.b3 |= (byte)(tbs.b3 & 0b11110000);
                _bytes.U.b1 += mud;
                _bytes.U.b2 += mud;
                _bytes.U.b3 += mud;

                _bytes.F.b1 &= 0b11110000;
                _bytes.F.b2 &= 0b00001111;
                _bytes.F.b3 &= 0b00001111;
                _bytes.F.b1 |= (byte)(tus.b1 & 0b00001111);
                _bytes.F.b2 |= (byte)(tus.b2 & 0b11110000);
                _bytes.F.b3 |= (byte)(tus.b3 & 0b11110000);
                _bytes.F.b1 += mud;
                _bytes.F.b2 += mud;
                _bytes.F.b3 += mud;

                _bytes.D.b1 &= 0b11110000;
                _bytes.D.b2 &= 0b00001111;
                _bytes.D.b3 &= 0b00001111;
                _bytes.D.b1 |= (byte)(tfs.b1 & 0b00001111);
                _bytes.D.b2 |= (byte)(tfs.b2 & 0b11110000);
                _bytes.D.b3 |= (byte)(tfs.b3 & 0b11110000);
                _bytes.D.b1 += mud;
                _bytes.D.b2 += mud;
                _bytes.D.b3 += mud;

                _bytes.B.b1 &= 0b11110000;
                _bytes.B.b2 &= 0b00001111;
                _bytes.B.b3 &= 0b00001111;
                _bytes.B.b1 |= (byte)(tds.b1 & 0b00001111);
                _bytes.B.b2 |= (byte)(tds.b2 & 0b11110000);
                _bytes.B.b3 |= (byte)(tds.b3 & 0b11110000);
                _bytes.B.b1 += mud;
                _bytes.B.b2 += mud;
                _bytes.B.b3 += mud;
            }
            _numIndex += _offset;
            //---------------------
            MoveSideClockwise(ref _bytes.L);
        }

        private void F()
        {
            SideBytes tus;
            SideBytes trs;
            SideBytes tds;
            SideBytes tls;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.U.b3 -= mud;
                _bytes.U.b4 -= mud;

                _bytes.R.b1 -= mud;
                _bytes.R.b2 -= mud;
                _bytes.R.b3 -= mud;

                _bytes.L.b2 -= mud;
                _bytes.L.b3 -= mud;
                _bytes.L.b4 -= mud;

                _bytes.D.b1 -= mud;
                _bytes.D.b2 -= mud;
                tus = _bytes.U;
                trs = _bytes.R;
                tds = _bytes.D;
                tls = _bytes.L;

                _bytes.R.b1 &= 0b11110000;
                _bytes.R.b2 &= 0b00001111;
                _bytes.R.b3 &= 0b00001111;
                _bytes.R.b1 |= (byte)(tus.b3 >> 4 & 0b00001111);
                _bytes.R.b2 |= (byte)(tus.b4 << 4 & 0b11110000);
                _bytes.R.b3 |= (byte)(tus.b4 & 0b11110000);

                _bytes.D.b2 &= 0b11110000;
                _bytes.D.b1 &= 0b00000000;
                _bytes.D.b2 |= (byte)(trs.b1 & 0b00001111);
                _bytes.D.b1 |= (byte)(trs.b2 & 0b11110000);
                _bytes.D.b1 |= (byte)(trs.b3 >> 4 & 0b00001111);

                _bytes.L.b2 &= 0b11110000;
                _bytes.L.b3 &= 0b11110000;
                _bytes.L.b4 &= 0b00001111;
                _bytes.L.b2 |= (byte)(tds.b1 & 0b00001111);
                _bytes.L.b3 |= (byte)(tds.b1 >> 4 & 0b00001111);
                _bytes.L.b4 |= (byte)(tds.b2 << 4 & 0b11110000);

                _bytes.U.b3 &= 0b00001111;
                _bytes.U.b4 &= 0b00000000;
                _bytes.U.b3 |= (byte)(tls.b4 & 0b11110000);
                _bytes.U.b4 |= (byte)(tls.b3 & 0b00001111);
                _bytes.U.b4 |= (byte)(tls.b2 << 4 & 0b11110000);
            }
            else
            {
                tus = _bytes.U;
                trs = _bytes.R;
                tds = _bytes.D;
                tls = _bytes.L;
                _bytes.R.b1 &= 0b11110000;
                _bytes.R.b2 &= 0b00001111;
                _bytes.R.b3 &= 0b00001111;
                _bytes.R.b1 |= (byte)(tus.b3 >> 4 & 0b00001111);
                _bytes.R.b2 |= (byte)(tus.b4 << 4 & 0b11110000);
                _bytes.R.b3 |= (byte)(tus.b4 & 0b11110000);
                _bytes.R.b1 += mud;
                _bytes.R.b2 += mud;
                _bytes.R.b3 += mud;

                _bytes.D.b2 &= 0b11110000;
                _bytes.D.b1 &= 0b00000000;
                _bytes.D.b2 |= (byte)(trs.b1 & 0b00001111);
                _bytes.D.b1 |= (byte)(trs.b2 & 0b11110000);
                _bytes.D.b1 |= (byte)(trs.b3 >> 4 & 0b00001111);
                _bytes.D.b1 += mud;
                _bytes.D.b2 += mud;

                _bytes.L.b2 &= 0b11110000;
                _bytes.L.b3 &= 0b11110000;
                _bytes.L.b4 &= 0b00001111;
                _bytes.L.b2 |= (byte)(tds.b1 & 0b00001111);
                _bytes.L.b3 |= (byte)(tds.b1 >> 4 & 0b00001111);
                _bytes.L.b4 |= (byte)(tds.b2 << 4 & 0b11110000);
                _bytes.L.b4 += mud;
                _bytes.L.b2 += mud;
                _bytes.L.b3 += mud;

                _bytes.U.b3 &= 0b00001111;
                _bytes.U.b4 &= 0b00000000;
                _bytes.U.b3 |= (byte)(tls.b4 & 0b11110000);
                _bytes.U.b4 |= (byte)(tls.b3 & 0b00001111);
                _bytes.U.b4 |= (byte)(tls.b2 << 4 & 0b11110000);
                _bytes.U.b3 += mud;
                _bytes.U.b4 += mud;
            }
            _numIndex += _offset;
            //---------------------
            MoveSideClockwise(ref _bytes.F);
        }

        private void B()
        {
            SideBytes tus;
            SideBytes trs;
            SideBytes tds;
            SideBytes tls;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.D.b3 -= mud;
                _bytes.D.b4 -= mud;

                _bytes.L.b1 -= mud;
                _bytes.L.b2 -= mud;
                _bytes.L.b3 -= mud;

                _bytes.R.b2 -= mud;
                _bytes.R.b3 -= mud;
                _bytes.R.b4 -= mud;

                _bytes.U.b1 -= mud;
                _bytes.U.b2 -= mud;
                tus = _bytes.U;
                trs = _bytes.R;
                tds = _bytes.D;
                tls = _bytes.L;

                _bytes.L.b1 &= 0b11110000;
                _bytes.L.b2 &= 0b00001111;
                _bytes.L.b3 &= 0b00001111;
                _bytes.L.b1 |= (byte)(tus.b2 & 0b00001111);
                _bytes.L.b2 |= (byte)(tus.b1 & 0b11110000);
                _bytes.L.b3 |= (byte)(tus.b1 << 4 & 0b11110000);

                _bytes.D.b3 &= 0b00001111;
                _bytes.D.b4 &= 0b00000000;
                _bytes.D.b3 |= (byte)(tls.b1 << 4 & 0b11110000);
                _bytes.D.b4 |= (byte)(tls.b2 >> 4 & 0b00001111);
                _bytes.D.b4 |= (byte)(tls.b3 & 0b11110000);

                _bytes.R.b2 &= 0b11110000;
                _bytes.R.b3 &= 0b11110000;
                _bytes.R.b4 &= 0b00001111;
                _bytes.R.b2 |= (byte)(tds.b4 >> 4 & 0b00001111);
                _bytes.R.b3 |= (byte)(tds.b4 & 0b00001111);
                _bytes.R.b4 |= (byte)(tds.b3 & 0b11110000);

                _bytes.U.b1 &= 0b00000000;
                _bytes.U.b2 &= 0b11110000;
                _bytes.U.b1 |= (byte)(trs.b2 & 0b00001111);
                _bytes.U.b1 |= (byte)(trs.b3 << 4 & 0b11110000);
                _bytes.U.b2 |= (byte)(trs.b4 >> 4 & 0b00001111);
            }
            else
            {
                tus = _bytes.U;
                trs = _bytes.R;
                tds = _bytes.D;
                tls = _bytes.L;
                _bytes.L.b1 &= 0b11110000;
                _bytes.L.b2 &= 0b00001111;
                _bytes.L.b3 &= 0b00001111;
                _bytes.L.b1 |= (byte)(tus.b2 & 0b00001111);
                _bytes.L.b2 |= (byte)(tus.b1 & 0b11110000);
                _bytes.L.b3 |= (byte)(tus.b1 << 4 & 0b11110000);
                _bytes.L.b1 += mud;
                _bytes.L.b2 += mud;
                _bytes.L.b3 += mud;

                _bytes.D.b3 &= 0b00001111;
                _bytes.D.b4 &= 0b00000000;
                _bytes.D.b3 |= (byte)(tls.b1 << 4 & 0b11110000);
                _bytes.D.b4 |= (byte)(tls.b2 >> 4 & 0b00001111);
                _bytes.D.b4 |= (byte)(tls.b3 & 0b11110000);
                _bytes.D.b4 += mud;
                _bytes.D.b3 += mud;

                _bytes.R.b2 &= 0b11110000;
                _bytes.R.b3 &= 0b11110000;
                _bytes.R.b4 &= 0b00001111;
                _bytes.R.b2 |= (byte)(tds.b4 >> 4 & 0b00001111);
                _bytes.R.b3 |= (byte)(tds.b4 & 0b00001111);
                _bytes.R.b4 |= (byte)(tds.b3 & 0b11110000);
                _bytes.R.b4 += mud;
                _bytes.R.b2 += mud;
                _bytes.R.b3 += mud;

                _bytes.U.b1 &= 0b00000000;
                _bytes.U.b2 &= 0b11110000;
                _bytes.U.b1 |= (byte)(trs.b2 & 0b00001111);
                _bytes.U.b1 |= (byte)(trs.b3 << 4 & 0b11110000);
                _bytes.U.b2 |= (byte)(trs.b4 >> 4 & 0b00001111);
                _bytes.U.b1 += mud;
                _bytes.U.b2 += mud;
            }
            _numIndex += _offset;
            //---------------------
            MoveSideClockwise(ref _bytes.B);
        }

        private void U()
        {
            SideBytes tfs;
            SideBytes tls;
            SideBytes tbs;
            SideBytes trs;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.B.b3 -= mud;
                _bytes.B.b4 -= mud;

                _bytes.R.b1 -= mud;
                _bytes.R.b2 -= mud;

                _bytes.F.b1 -= mud;
                _bytes.F.b2 -= mud;

                _bytes.L.b1 -= mud;
                _bytes.L.b2 -= mud;
                tfs = _bytes.F;
                tls = _bytes.L;
                tbs = _bytes.B;
                trs = _bytes.R;

                _bytes.L.b2 &= 0b11110000;
                _bytes.L.b1 = (byte)(tfs.b1);
                _bytes.L.b2 |= (byte)(tfs.b2 & 0b00001111);

                _bytes.B.b3 &= 0b00001111;
                _bytes.B.b4 &= 0b00000000;
                _bytes.B.b3 |= (byte)(tls.b2 << 4 & 0b11110000);
                _bytes.B.b4 |= (byte)(tls.b1 >> 4 & 0b00001111);
                _bytes.B.b4 |= (byte)(tls.b1 << 4 & 0b11110000);

                _bytes.R.b2 &= 0b11110000;
                _bytes.R.b1 &= 0b00000000;
                _bytes.R.b2 |= (byte)(tbs.b3 >> 4 & 0b00001111);
                _bytes.R.b1 |= (byte)(tbs.b4 << 4 & 0b11110000);
                _bytes.R.b1 |= (byte)(tbs.b4 >> 4 & 0b00001111);

                _bytes.F.b2 &= 0b11110000;
                _bytes.F.b1 = (byte)(trs.b1);
                _bytes.F.b2 |= (byte)(trs.b2 & 0b00001111);
            }
            else
            {
                tfs = _bytes.F;
                tls = _bytes.L;
                tbs = _bytes.B;
                trs = _bytes.R;

                _bytes.L.b2 &= 0b11110000;
                _bytes.L.b1 = (byte)(tfs.b1);
                _bytes.L.b2 |= (byte)(tfs.b2 & 0b00001111);
                _bytes.L.b1 += mud;
                _bytes.L.b2 += mud;

                _bytes.B.b3 &= 0b00001111;
                _bytes.B.b4 &= 0b00000000;
                _bytes.B.b3 |= (byte)(tls.b2 << 4 & 0b11110000);
                _bytes.B.b4 |= (byte)(tls.b1 >> 4 & 0b00001111);
                _bytes.B.b4 |= (byte)(tls.b1 << 4 & 0b11110000);
                _bytes.B.b3 += mud;
                _bytes.B.b4 += mud;

                _bytes.R.b2 &= 0b11110000;
                _bytes.R.b1 &= 0b00000000;
                _bytes.R.b2 |= (byte)(tbs.b3 >> 4 & 0b00001111);
                _bytes.R.b1 |= (byte)(tbs.b4 << 4 & 0b11110000);
                _bytes.R.b1 |= (byte)(tbs.b4 >> 4 & 0b00001111);
                _bytes.R.b1 += mud;
                _bytes.R.b2 += mud;

                _bytes.F.b2 &= 0b11110000;
                _bytes.F.b1 = (byte)(trs.b1);
                _bytes.F.b2 |= (byte)(trs.b2 & 0b00001111);
                _bytes.F.b1 += mud;
                _bytes.F.b2 += mud;
            }
            _numIndex += _offset;
            //---------------------
            MoveSideClockwise(ref _bytes.U);
        }

        private void D()
        {
            SideBytes tfs;
            SideBytes tls;
            SideBytes tbs;
            SideBytes trs;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.B.b1 -= mud;
                _bytes.B.b2 -= mud;

                _bytes.R.b3 -= mud;
                _bytes.R.b4 -= mud;

                _bytes.F.b3 -= mud;
                _bytes.F.b4 -= mud;

                _bytes.L.b3 -= mud;
                _bytes.L.b4 -= mud;
                tfs = _bytes.F;
                tls = _bytes.L;
                tbs = _bytes.B;
                trs = _bytes.R;

                _bytes.L.b3 &= 0b00001111;
                _bytes.L.b4 &= 0b00000000;
                _bytes.L.b3 |= (byte)(tbs.b2 << 4 & 0b11110000);
                _bytes.L.b4 |= (byte)(tbs.b1 >> 4 & 0b00001111);
                _bytes.L.b4 |= (byte)(tbs.b1 << 4 & 0b11110000);

                _bytes.F.b3 &= 0b00001111;
                _bytes.F.b4 = (byte)(tls.b4);
                _bytes.F.b3 |= (byte)(tls.b3 & 0b11110000);

                _bytes.R.b3 &= 0b00001111;
                _bytes.R.b4 = (byte)(tfs.b4);
                _bytes.R.b3 |= (byte)(tfs.b3 & 0b11110000);

                _bytes.B.b2 &= 0b11110000;
                _bytes.B.b1 &= 0b00000000;
                _bytes.B.b2 |= (byte)(trs.b3 >> 4 & 0b00001111);
                _bytes.B.b1 |= (byte)(trs.b4 << 4 & 0b11110000);
                _bytes.B.b1 |= (byte)(trs.b4 >> 4 & 0b00001111);
            }
            else
            {
                tfs = _bytes.F;
                tls = _bytes.L;
                tbs = _bytes.B;
                trs = _bytes.R;
                _bytes.L.b3 &= 0b00001111;
                _bytes.L.b4 &= 0b00000000;
                _bytes.L.b3 |= (byte)(tbs.b2 << 4 & 0b11110000);
                _bytes.L.b4 |= (byte)(tbs.b1 >> 4 & 0b00001111);
                _bytes.L.b4 |= (byte)(tbs.b1 << 4 & 0b11110000);
                _bytes.L.b3 += mud;
                _bytes.L.b4 += mud;

                _bytes.F.b3 &= 0b00001111;
                _bytes.F.b4 = (byte)(tls.b4);
                _bytes.F.b3 |= (byte)(tls.b3 & 0b11110000);
                _bytes.F.b3 += mud;
                _bytes.F.b4 += mud;

                _bytes.R.b3 &= 0b00001111;
                _bytes.R.b4 = (byte)(tfs.b4);
                _bytes.R.b3 |= (byte)(tfs.b3 & 0b11110000);
                _bytes.R.b3 += mud;
                _bytes.R.b4 += mud;

                _bytes.B.b2 &= 0b11110000;
                _bytes.B.b1 &= 0b00000000;
                _bytes.B.b2 |= (byte)(trs.b3 >> 4 & 0b00001111);
                _bytes.B.b1 |= (byte)(trs.b4 << 4 & 0b11110000);
                _bytes.B.b1 |= (byte)(trs.b4 >> 4 & 0b00001111);
                _bytes.B.b1 += mud;
                _bytes.B.b2 += mud;
            }
            _numIndex += _offset;
            //---------------------
            MoveSideClockwise(ref _bytes.D);
        }

        private void AntiR()
        {
            SideBytes tus;
            SideBytes tfs;
            SideBytes tbs;
            SideBytes tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.U.b2 -= mud;
                _bytes.U.b3 -= mud;
                _bytes.U.b4 -= mud;

                _bytes.F.b2 -= mud;
                _bytes.F.b3 -= mud;
                _bytes.F.b4 -= mud;

                _bytes.B.b2 -= mud;
                _bytes.B.b3 -= mud;
                _bytes.B.b4 -= mud;

                _bytes.D.b2 -= mud;
                _bytes.D.b3 -= mud;
                _bytes.D.b4 -= mud;
                tus = _bytes.U;
                tfs = _bytes.F;
                tbs = _bytes.B;
                tds = _bytes.D;

                _bytes.U.b2 &= 0b11110000;
                _bytes.U.b3 &= 0b11110000;
                _bytes.U.b4 &= 0b00001111;
                _bytes.U.b2 |= (byte)(tbs.b2 & 0b00001111);
                _bytes.U.b3 |= (byte)(tbs.b3 & 0b00001111);
                _bytes.U.b4 |= (byte)(tbs.b4 & 0b11110000);

                _bytes.B.b2 &= 0b11110000;
                _bytes.B.b3 &= 0b11110000;
                _bytes.B.b4 &= 0b00001111;
                _bytes.B.b2 |= (byte)(tds.b2 & 0b00001111);
                _bytes.B.b3 |= (byte)(tds.b3 & 0b00001111);
                _bytes.B.b4 |= (byte)(tds.b4 & 0b11110000);

                _bytes.D.b2 &= 0b11110000;
                _bytes.D.b3 &= 0b11110000;
                _bytes.D.b4 &= 0b00001111;
                _bytes.D.b2 |= (byte)(tfs.b2 & 0b00001111);
                _bytes.D.b3 |= (byte)(tfs.b3 & 0b00001111);
                _bytes.D.b4 |= (byte)(tfs.b4 & 0b11110000);

                _bytes.F.b2 &= 0b11110000;
                _bytes.F.b3 &= 0b11110000;
                _bytes.F.b4 &= 0b00001111;
                _bytes.F.b2 |= (byte)(tus.b2 & 0b00001111);
                _bytes.F.b3 |= (byte)(tus.b3 & 0b00001111);
                _bytes.F.b4 |= (byte)(tus.b4 & 0b11110000);
            }
            else
            {
                tus = _bytes.U;
                tfs = _bytes.F;
                tbs = _bytes.B;
                tds = _bytes.D;
                _bytes.U.b2 &= 0b11110000;
                _bytes.U.b3 &= 0b11110000;
                _bytes.U.b4 &= 0b00001111;
                _bytes.U.b2 |= (byte)(tbs.b2 & 0b00001111);
                _bytes.U.b3 |= (byte)(tbs.b3 & 0b00001111);
                _bytes.U.b4 |= (byte)(tbs.b4 & 0b11110000);

                _bytes.B.b2 &= 0b11110000;
                _bytes.B.b3 &= 0b11110000;
                _bytes.B.b4 &= 0b00001111;
                _bytes.B.b2 |= (byte)(tds.b2 & 0b00001111);
                _bytes.B.b3 |= (byte)(tds.b3 & 0b00001111);
                _bytes.B.b4 |= (byte)(tds.b4 & 0b11110000);

                _bytes.D.b2 &= 0b11110000;
                _bytes.D.b3 &= 0b11110000;
                _bytes.D.b4 &= 0b00001111;
                _bytes.D.b2 |= (byte)(tfs.b2 & 0b00001111);
                _bytes.D.b3 |= (byte)(tfs.b3 & 0b00001111);
                _bytes.D.b4 |= (byte)(tfs.b4 & 0b11110000);

                _bytes.F.b2 &= 0b11110000;
                _bytes.F.b3 &= 0b11110000;
                _bytes.F.b4 &= 0b00001111;
                _bytes.F.b2 |= (byte)(tus.b2 & 0b00001111);
                _bytes.F.b3 |= (byte)(tus.b3 & 0b00001111);
                _bytes.F.b4 |= (byte)(tus.b4 & 0b11110000);

                _bytes.U.b2 += mud;
                _bytes.U.b3 += mud;
                _bytes.U.b4 += mud;

                _bytes.B.b2 += mud;
                _bytes.B.b3 += mud;
                _bytes.B.b4 += mud;

                _bytes.D.b2 += mud;
                _bytes.D.b3 += mud;
                _bytes.D.b4 += mud;

                _bytes.F.b2 += mud;
                _bytes.F.b3 += mud;
                _bytes.F.b4 += mud;
            }
            _numIndex += _offset;
            //---------------------
            MoveSideCounterClockwise(ref _bytes.R);
        }

        private void AntiL()
        {
            SideBytes tus;
            SideBytes tfs;
            SideBytes tbs;
            SideBytes tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.U.b1 -= mud;
                _bytes.U.b2 -= mud;
                _bytes.U.b3 -= mud;

                _bytes.F.b1 -= mud;
                _bytes.F.b2 -= mud;
                _bytes.F.b3 -= mud;

                _bytes.B.b1 -= mud;
                _bytes.B.b2 -= mud;
                _bytes.B.b3 -= mud;

                _bytes.D.b1 -= mud;
                _bytes.D.b2 -= mud;
                _bytes.D.b3 -= mud;
                tus = _bytes.U;
                tfs = _bytes.F;
                tbs = _bytes.B;
                tds = _bytes.D;

                _bytes.U.b1 &= 0b11110000;
                _bytes.U.b2 &= 0b00001111;
                _bytes.U.b3 &= 0b00001111;
                _bytes.U.b1 |= (byte)(tfs.b1 & 0b00001111);
                _bytes.U.b2 |= (byte)(tfs.b2 & 0b11110000);
                _bytes.U.b3 |= (byte)(tfs.b3 & 0b11110000);

                _bytes.F.b1 &= 0b11110000;
                _bytes.F.b2 &= 0b00001111;
                _bytes.F.b3 &= 0b00001111;
                _bytes.F.b1 |= (byte)(tds.b1 & 0b00001111);
                _bytes.F.b2 |= (byte)(tds.b2 & 0b11110000);
                _bytes.F.b3 |= (byte)(tds.b3 & 0b11110000);

                _bytes.D.b1 &= 0b11110000;
                _bytes.D.b2 &= 0b00001111;
                _bytes.D.b3 &= 0b00001111;
                _bytes.D.b1 |= (byte)(tbs.b1 & 0b00001111);
                _bytes.D.b2 |= (byte)(tbs.b2 & 0b11110000);
                _bytes.D.b3 |= (byte)(tbs.b3 & 0b11110000);

                _bytes.B.b1 &= 0b11110000;
                _bytes.B.b2 &= 0b00001111;
                _bytes.B.b3 &= 0b00001111;
                _bytes.B.b1 |= (byte)(tus.b1 & 0b00001111);
                _bytes.B.b2 |= (byte)(tus.b2 & 0b11110000);
                _bytes.B.b3 |= (byte)(tus.b3 & 0b11110000);
            }
            else
            {
                tus = _bytes.U;
                tfs = _bytes.F;
                tbs = _bytes.B;
                tds = _bytes.D;
                _bytes.U.b1 &= 0b11110000;
                _bytes.U.b2 &= 0b00001111;
                _bytes.U.b3 &= 0b00001111;
                _bytes.U.b1 |= (byte)(tfs.b1 & 0b00001111);
                _bytes.U.b2 |= (byte)(tfs.b2 & 0b11110000);
                _bytes.U.b3 |= (byte)(tfs.b3 & 0b11110000);

                _bytes.F.b1 &= 0b11110000;
                _bytes.F.b2 &= 0b00001111;
                _bytes.F.b3 &= 0b00001111;
                _bytes.F.b1 |= (byte)(tds.b1 & 0b00001111);
                _bytes.F.b2 |= (byte)(tds.b2 & 0b11110000);
                _bytes.F.b3 |= (byte)(tds.b3 & 0b11110000);

                _bytes.D.b1 &= 0b11110000;
                _bytes.D.b2 &= 0b00001111;
                _bytes.D.b3 &= 0b00001111;
                _bytes.D.b1 |= (byte)(tbs.b1 & 0b00001111);
                _bytes.D.b2 |= (byte)(tbs.b2 & 0b11110000);
                _bytes.D.b3 |= (byte)(tbs.b3 & 0b11110000);

                _bytes.B.b1 &= 0b11110000;
                _bytes.B.b2 &= 0b00001111;
                _bytes.B.b3 &= 0b00001111;
                _bytes.B.b1 |= (byte)(tus.b1 & 0b00001111);
                _bytes.B.b2 |= (byte)(tus.b2 & 0b11110000);
                _bytes.B.b3 |= (byte)(tus.b3 & 0b11110000);

                _bytes.U.b1 += mud;
                _bytes.U.b2 += mud;
                _bytes.U.b3 += mud;

                _bytes.F.b1 += mud;
                _bytes.F.b2 += mud;
                _bytes.F.b3 += mud;

                _bytes.D.b1 += mud;
                _bytes.D.b2 += mud;
                _bytes.D.b3 += mud;

                _bytes.B.b1 += mud;
                _bytes.B.b2 += mud;
                _bytes.B.b3 += mud;
            }
            _numIndex += _offset;
            //---------------------
            MoveSideCounterClockwise(ref _bytes.L);
        }

        private void AntiF()
        {
            SideBytes tus;
            SideBytes trs;
            SideBytes tds;
            SideBytes tls;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.U.b3 -= mud;
                _bytes.U.b4 -= mud;

                _bytes.R.b1 -= mud;
                _bytes.R.b2 -= mud;
                _bytes.R.b3 -= mud;

                _bytes.L.b2 -= mud;
                _bytes.L.b3 -= mud;
                _bytes.L.b4 -= mud;

                _bytes.D.b1 -= mud;
                _bytes.D.b2 -= mud;
                tus = _bytes.U;
                trs = _bytes.R;
                tds = _bytes.D;
                tls = _bytes.L;
                _bytes.R.b1 &= 0b11110000;
                _bytes.R.b2 &= 0b00001111;
                _bytes.R.b3 &= 0b00001111;
                _bytes.R.b1 |= (byte)(tds.b2 & 0b00001111);
                _bytes.R.b2 |= (byte)(tds.b1 & 0b11110000);
                _bytes.R.b3 |= (byte)(tds.b1 << 4 & 0b11110000);

                _bytes.D.b2 &= 0b11110000;
                _bytes.D.b1 &= 0b00000000;
                _bytes.D.b2 |= (byte)(tls.b4 >> 4 & 0b00001111);
                _bytes.D.b1 |= (byte)(tls.b3 << 4 & 0b11110000);
                _bytes.D.b1 |= (byte)(tls.b2 & 0b00001111);

                _bytes.L.b2 &= 0b11110000;
                _bytes.L.b3 &= 0b11110000;
                _bytes.L.b4 &= 0b00001111;
                _bytes.L.b2 |= (byte)(tus.b4 >> 4 & 0b00001111);
                _bytes.L.b3 |= (byte)(tus.b4 & 0b00001111);
                _bytes.L.b4 |= (byte)(tus.b3 & 0b11110000);

                _bytes.U.b3 &= 0b00001111;
                _bytes.U.b4 &= 0b00000000;
                _bytes.U.b3 |= (byte)(trs.b1 << 4 & 0b11110000);
                _bytes.U.b4 |= (byte)(trs.b2 >> 4 & 0b00001111);
                _bytes.U.b4 |= (byte)(trs.b3 & 0b11110000);
            }
            else
            {
                tus = _bytes.U;
                trs = _bytes.R;
                tds = _bytes.D;
                tls = _bytes.L;
                _bytes.R.b1 &= 0b11110000;
                _bytes.R.b2 &= 0b00001111;
                _bytes.R.b3 &= 0b00001111;
                _bytes.R.b1 |= (byte)(tds.b2 & 0b00001111);
                _bytes.R.b2 |= (byte)(tds.b1 & 0b11110000);
                _bytes.R.b3 |= (byte)(tds.b1 << 4 & 0b11110000);

                _bytes.D.b2 &= 0b11110000;
                _bytes.D.b1 &= 0b00000000;
                _bytes.D.b2 |= (byte)(tls.b4 >> 4 & 0b00001111);
                _bytes.D.b1 |= (byte)(tls.b3 << 4 & 0b11110000);
                _bytes.D.b1 |= (byte)(tls.b2 & 0b00001111);

                _bytes.L.b2 &= 0b11110000;
                _bytes.L.b3 &= 0b11110000;
                _bytes.L.b4 &= 0b00001111;
                _bytes.L.b2 |= (byte)(tus.b4 >> 4 & 0b00001111);
                _bytes.L.b3 |= (byte)(tus.b4 & 0b00001111);
                _bytes.L.b4 |= (byte)(tus.b3 & 0b11110000);

                _bytes.U.b3 &= 0b00001111;
                _bytes.U.b4 &= 0b00000000;
                _bytes.U.b3 |= (byte)(trs.b1 << 4 & 0b11110000);
                _bytes.U.b4 |= (byte)(trs.b2 >> 4 & 0b00001111);
                _bytes.U.b4 |= (byte)(trs.b3 & 0b11110000);
                _bytes.R.b1 += mud;
                _bytes.R.b2 += mud;
                _bytes.R.b3 += mud;

                _bytes.D.b1 += mud;
                _bytes.D.b2 += mud;

                _bytes.L.b4 += mud;
                _bytes.L.b2 += mud;
                _bytes.L.b3 += mud;

                _bytes.U.b3 += mud;
                _bytes.U.b4 += mud;
            }
            _numIndex += _offset;
            //---------------------
            MoveSideCounterClockwise(ref _bytes.F);
        }

        private void AntiB()
        {
            SideBytes tus;
            SideBytes trs;
            SideBytes tds;
            SideBytes tls;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.D.b3 -= mud;
                _bytes.D.b4 -= mud;

                _bytes.L.b1 -= mud;
                _bytes.L.b2 -= mud;
                _bytes.L.b3 -= mud;

                _bytes.R.b2 -= mud;
                _bytes.R.b3 -= mud;
                _bytes.R.b4 -= mud;

                _bytes.U.b1 -= mud;
                _bytes.U.b2 -= mud;
                tus = _bytes.U;
                trs = _bytes.R;
                tds = _bytes.D;
                tls = _bytes.L;
                _bytes.L.b1 &= 0b11110000;
                _bytes.L.b2 &= 0b00001111;
                _bytes.L.b3 &= 0b00001111;
                _bytes.L.b1 |= (byte)(tds.b3 >> 4 & 0b00001111);
                _bytes.L.b2 |= (byte)(tds.b4 << 4 & 0b11110000);
                _bytes.L.b3 |= (byte)(tds.b4 & 0b11110000);

                _bytes.D.b3 &= 0b00001111;
                _bytes.D.b4 &= 0b00000000;
                _bytes.D.b3 |= (byte)(trs.b4 & 0b11110000);
                _bytes.D.b4 |= (byte)(trs.b3 & 0b00001111);
                _bytes.D.b4 |= (byte)(trs.b2 << 4 & 0b11110000);

                _bytes.R.b2 &= 0b11110000;
                _bytes.R.b3 &= 0b11110000;
                _bytes.R.b4 &= 0b00001111;
                _bytes.R.b2 |= (byte)(tus.b1 & 0b00001111);
                _bytes.R.b3 |= (byte)(tus.b1 >> 4 & 0b00001111);
                _bytes.R.b4 |= (byte)(tus.b2 << 4 & 0b11110000);

                _bytes.U.b1 &= 0b00000000;
                _bytes.U.b2 &= 0b11110000;
                _bytes.U.b1 |= (byte)(tls.b3 >> 4 & 0b00001111);
                _bytes.U.b1 |= (byte)(tls.b2 & 0b11110000);
                _bytes.U.b2 |= (byte)(tls.b1 & 0b00001111);
            }
            else
            {
                tus = _bytes.U;
                trs = _bytes.R;
                tds = _bytes.D;
                tls = _bytes.L;
                _bytes.L.b1 &= 0b11110000;
                _bytes.L.b2 &= 0b00001111;
                _bytes.L.b3 &= 0b00001111;
                _bytes.L.b1 |= (byte)(tds.b3 >> 4 & 0b00001111);
                _bytes.L.b2 |= (byte)(tds.b4 << 4 & 0b11110000);
                _bytes.L.b3 |= (byte)(tds.b4 & 0b11110000);

                _bytes.D.b3 &= 0b00001111;
                _bytes.D.b4 &= 0b00000000;
                _bytes.D.b3 |= (byte)(trs.b4 & 0b11110000);
                _bytes.D.b4 |= (byte)(trs.b3 & 0b00001111);
                _bytes.D.b4 |= (byte)(trs.b2 << 4 & 0b11110000);

                _bytes.R.b2 &= 0b11110000;
                _bytes.R.b3 &= 0b11110000;
                _bytes.R.b4 &= 0b00001111;
                _bytes.R.b2 |= (byte)(tus.b1 & 0b00001111);
                _bytes.R.b3 |= (byte)(tus.b1 >> 4 & 0b00001111);
                _bytes.R.b4 |= (byte)(tus.b2 << 4 & 0b11110000);

                _bytes.U.b1 &= 0b00000000;
                _bytes.U.b2 &= 0b11110000;
                _bytes.U.b1 |= (byte)(tls.b3 >> 4 & 0b00001111);
                _bytes.U.b1 |= (byte)(tls.b2 & 0b11110000);
                _bytes.U.b2 |= (byte)(tls.b1 & 0b00001111);
                _bytes.L.b1 += mud;
                _bytes.L.b2 += mud;
                _bytes.L.b3 += mud;

                _bytes.D.b4 += mud;
                _bytes.D.b3 += mud;

                _bytes.R.b4 += mud;
                _bytes.R.b2 += mud;
                _bytes.R.b3 += mud;

                _bytes.U.b1 += mud;
                _bytes.U.b2 += mud;
            }
            _numIndex += _offset;
            //---------------------
            MoveSideCounterClockwise(ref _bytes.B);
        }

        private void AntiU()
        {
            SideBytes tfs;
            SideBytes tls;
            SideBytes tbs;
            SideBytes trs;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.B.b3 -= mud;
                _bytes.B.b4 -= mud;

                _bytes.R.b1 -= mud;
                _bytes.R.b2 -= mud;

                _bytes.F.b1 -= mud;
                _bytes.F.b2 -= mud;

                _bytes.L.b1 -= mud;
                _bytes.L.b2 -= mud;
                tfs = _bytes.F;
                tls = _bytes.L;
                tbs = _bytes.B;
                trs = _bytes.R;
                _bytes.L.b2 &= 0b11110000;
                _bytes.L.b1 &= 0b00000000;
                _bytes.L.b2 |= (byte)(tbs.b3 >> 4 & 0b00001111);
                _bytes.L.b1 |= (byte)(tbs.b4 << 4 & 0b11110000);
                _bytes.L.b1 |= (byte)(tbs.b4 >> 4 & 0b00001111);

                _bytes.B.b3 &= 0b00001111;
                _bytes.B.b4 &= 0b00000000;
                _bytes.B.b3 |= (byte)(trs.b2 << 4 & 0b11110000);
                _bytes.B.b4 |= (byte)(trs.b1 >> 4 & 0b00001111);
                _bytes.B.b4 |= (byte)(trs.b1 << 4 & 0b11110000);

                _bytes.R.b2 &= 0b11110000;
                _bytes.R.b1 = (byte)(tfs.b1);
                _bytes.R.b2 |= (byte)(tfs.b2 & 0b00001111);

                _bytes.F.b2 &= 0b11110000;
                _bytes.F.b1 = (byte)(tls.b1);
                _bytes.F.b2 |= (byte)(tls.b2 & 0b00001111);
            }
            else
            {
                tfs = _bytes.F;
                tls = _bytes.L;
                tbs = _bytes.B;
                trs = _bytes.R;
                _bytes.L.b2 &= 0b11110000;
                _bytes.L.b1 &= 0b00000000;
                _bytes.L.b2 |= (byte)(tbs.b3 >> 4 & 0b00001111);
                _bytes.L.b1 |= (byte)(tbs.b4 << 4 & 0b11110000);
                _bytes.L.b1 |= (byte)(tbs.b4 >> 4 & 0b00001111);

                _bytes.B.b3 &= 0b00001111;
                _bytes.B.b4 &= 0b00000000;
                _bytes.B.b3 |= (byte)(trs.b2 << 4 & 0b11110000);
                _bytes.B.b4 |= (byte)(trs.b1 >> 4 & 0b00001111);
                _bytes.B.b4 |= (byte)(trs.b1 << 4 & 0b11110000);

                _bytes.R.b2 &= 0b11110000;
                _bytes.R.b1 = (byte)(tfs.b1);
                _bytes.R.b2 |= (byte)(tfs.b2 & 0b00001111);

                _bytes.F.b2 &= 0b11110000;
                _bytes.F.b1 = (byte)(tls.b1);
                _bytes.F.b2 |= (byte)(tls.b2 & 0b00001111);
                _bytes.L.b1 += mud;
                _bytes.L.b2 += mud;

                _bytes.B.b3 += mud;
                _bytes.B.b4 += mud;

                _bytes.R.b1 += mud;
                _bytes.R.b2 += mud;

                _bytes.F.b1 += mud;
                _bytes.F.b2 += mud;
            }
            _numIndex += _offset;
            //---------------------
            MoveSideCounterClockwise(ref _bytes.U);
        }

        private void AntiD()
        {
            SideBytes tfs;
            SideBytes tls;
            SideBytes tbs;
            SideBytes trs;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.B.b1 -= mud;
                _bytes.B.b2 -= mud;

                _bytes.R.b3 -= mud;
                _bytes.R.b4 -= mud;

                _bytes.F.b3 -= mud;
                _bytes.F.b4 -= mud;

                _bytes.L.b3 -= mud;
                _bytes.L.b4 -= mud;
                tfs = _bytes.F;
                tls = _bytes.L;
                tbs = _bytes.B;
                trs = _bytes.R;
                _bytes.R.b3 &= 0b00001111;
                _bytes.R.b4 &= 0b00000000;
                _bytes.R.b3 |= (byte)(tbs.b2 << 4 & 0b11110000);
                _bytes.R.b4 |= (byte)(tbs.b1 >> 4 & 0b00001111);
                _bytes.R.b4 |= (byte)(tbs.b1 << 4 & 0b11110000);

                _bytes.F.b3 &= 0b00001111;
                _bytes.F.b4 = (byte)(trs.b4);
                _bytes.F.b3 |= (byte)(trs.b3 & 0b11110000);

                _bytes.L.b3 &= 0b00001111;
                _bytes.L.b4 = (byte)(tfs.b4);
                _bytes.L.b3 |= (byte)(tfs.b3 & 0b11110000);

                _bytes.B.b2 &= 0b11110000;
                _bytes.B.b1 &= 0b00000000;
                _bytes.B.b2 |= (byte)(tls.b3 >> 4 & 0b00001111);
                _bytes.B.b1 |= (byte)(tls.b4 << 4 & 0b11110000);
                _bytes.B.b1 |= (byte)(tls.b4 >> 4 & 0b00001111);
            }
            else
            {
                tfs = _bytes.F;
                tls = _bytes.L;
                tbs = _bytes.B;
                trs = _bytes.R;
                _bytes.R.b3 &= 0b00001111;
                _bytes.R.b4 &= 0b00000000;
                _bytes.R.b3 |= (byte)(tbs.b2 << 4 & 0b11110000);
                _bytes.R.b4 |= (byte)(tbs.b1 >> 4 & 0b00001111);
                _bytes.R.b4 |= (byte)(tbs.b1 << 4 & 0b11110000);

                _bytes.F.b3 &= 0b00001111;
                _bytes.F.b4 = (byte)(trs.b4);
                _bytes.F.b3 |= (byte)(trs.b3 & 0b11110000);

                _bytes.L.b3 &= 0b00001111;
                _bytes.L.b4 = (byte)(tfs.b4);
                _bytes.L.b3 |= (byte)(tfs.b3 & 0b11110000);

                _bytes.B.b2 &= 0b11110000;
                _bytes.B.b1 &= 0b00000000;
                _bytes.B.b2 |= (byte)(tls.b3 >> 4 & 0b00001111);
                _bytes.B.b1 |= (byte)(tls.b4 << 4 & 0b11110000);
                _bytes.B.b1 |= (byte)(tls.b4 >> 4 & 0b00001111);
                _bytes.L.b3 += mud;
                _bytes.L.b4 += mud;

                _bytes.F.b3 += mud;
                _bytes.F.b4 += mud;

                _bytes.R.b3 += mud;
                _bytes.R.b4 += mud;

                _bytes.B.b1 += mud;
                _bytes.B.b2 += mud;
            }
            _numIndex += _offset;
            //---------------------
            MoveSideCounterClockwise(ref _bytes.D);
        }

        private void M()
        {
            SideBytes tus;
            SideBytes tfs;
            SideBytes tbs;
            SideBytes tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.U.b1 -= mud;
                _bytes.U.b4 -= mud;

                _bytes.F.b1 -= mud;
                _bytes.F.b4 -= mud;

                _bytes.D.b1 -= mud;
                _bytes.D.b4 -= mud;

                _bytes.B.b1 -= mud;
                _bytes.B.b4 -= mud;
                tus = _bytes.U;
                tfs = _bytes.F;
                tbs = _bytes.B;
                tds = _bytes.D;

                _bytes.U.b1 &= 0b00001111;
                _bytes.U.b4 &= 0b11110000;
                _bytes.U.b1 |= (byte)(tbs.b1 & 0b11110000);
                _bytes.U.b4 |= (byte)(tbs.b4 & 0b00001111);

                _bytes.F.b1 &= 0b00001111;
                _bytes.F.b4 &= 0b11110000;
                _bytes.F.b1 |= (byte)(tus.b1 & 0b11110000);
                _bytes.F.b4 |= (byte)(tus.b4 & 0b00001111);

                _bytes.D.b1 &= 0b00001111;
                _bytes.D.b4 &= 0b11110000;
                _bytes.D.b1 |= (byte)(tfs.b1 & 0b11110000);
                _bytes.D.b4 |= (byte)(tfs.b4 & 0b00001111);

                _bytes.B.b1 &= 0b00001111;
                _bytes.B.b4 &= 0b11110000;
                _bytes.B.b1 |= (byte)(tds.b1 & 0b11110000);
                _bytes.B.b4 |= (byte)(tds.b4 & 0b00001111);
            }
            else
            {
                tus = _bytes.U;
                tfs = _bytes.F;
                tbs = _bytes.B;
                tds = _bytes.D;
                _bytes.U.b1 &= 0b00001111;
                _bytes.U.b4 &= 0b11110000;
                _bytes.U.b1 |= (byte)(tbs.b1 & 0b11110000);
                _bytes.U.b4 |= (byte)(tbs.b4 & 0b00001111);
                _bytes.U.b1 += mud;
                _bytes.U.b4 += mud;

                _bytes.F.b1 &= 0b00001111;
                _bytes.F.b4 &= 0b11110000;
                _bytes.F.b1 |= (byte)(tus.b1 & 0b11110000);
                _bytes.F.b4 |= (byte)(tus.b4 & 0b00001111);
                _bytes.F.b1 += mud;
                _bytes.F.b4 += mud;

                _bytes.D.b1 &= 0b00001111;
                _bytes.D.b4 &= 0b11110000;
                _bytes.D.b1 |= (byte)(tfs.b1 & 0b11110000);
                _bytes.D.b4 |= (byte)(tfs.b4 & 0b00001111);
                _bytes.D.b1 += mud;
                _bytes.D.b4 += mud;

                _bytes.B.b1 &= 0b00001111;
                _bytes.B.b4 &= 0b11110000;
                _bytes.B.b1 |= (byte)(tds.b1 & 0b11110000);
                _bytes.B.b4 |= (byte)(tds.b4 & 0b00001111);
                _bytes.B.b1 += mud;
                _bytes.B.b4 += mud;
            }
            _numIndex += _offset;
        }

        private void AntiM()
        {
            SideBytes tus;
            SideBytes tfs;
            SideBytes tbs;
            SideBytes tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.U.b1 -= mud;
                _bytes.U.b4 -= mud;

                _bytes.F.b1 -= mud;
                _bytes.F.b4 -= mud;

                _bytes.D.b1 -= mud;
                _bytes.D.b4 -= mud;

                _bytes.B.b1 -= mud;
                _bytes.B.b4 -= mud;
                tus = _bytes.U;
                tfs = _bytes.F;
                tbs = _bytes.B;
                tds = _bytes.D;
                _bytes.U.b1 &= 0b00001111;
                _bytes.U.b4 &= 0b11110000;
                _bytes.U.b1 |= (byte)(tfs.b1 & 0b11110000);
                _bytes.U.b4 |= (byte)(tfs.b4 & 0b00001111);

                _bytes.F.b1 &= 0b00001111;
                _bytes.F.b4 &= 0b11110000;
                _bytes.F.b1 |= (byte)(tds.b1 & 0b11110000);
                _bytes.F.b4 |= (byte)(tds.b4 & 0b00001111);

                _bytes.D.b1 &= 0b00001111;
                _bytes.D.b4 &= 0b11110000;
                _bytes.D.b1 |= (byte)(tbs.b1 & 0b11110000);
                _bytes.D.b4 |= (byte)(tbs.b4 & 0b00001111);

                _bytes.B.b1 &= 0b00001111;
                _bytes.B.b4 &= 0b11110000;
                _bytes.B.b1 |= (byte)(tus.b1 & 0b11110000);
                _bytes.B.b4 |= (byte)(tus.b4 & 0b00001111);
            }
            else
            {
                tus = _bytes.U;
                tfs = _bytes.F;
                tbs = _bytes.B;
                tds = _bytes.D;
                _bytes.U.b1 &= 0b00001111;
                _bytes.U.b4 &= 0b11110000;
                _bytes.U.b1 |= (byte)(tfs.b1 & 0b11110000);
                _bytes.U.b4 |= (byte)(tfs.b4 & 0b00001111);

                _bytes.F.b1 &= 0b00001111;
                _bytes.F.b4 &= 0b11110000;
                _bytes.F.b1 |= (byte)(tds.b1 & 0b11110000);
                _bytes.F.b4 |= (byte)(tds.b4 & 0b00001111);

                _bytes.D.b1 &= 0b00001111;
                _bytes.D.b4 &= 0b11110000;
                _bytes.D.b1 |= (byte)(tbs.b1 & 0b11110000);
                _bytes.D.b4 |= (byte)(tbs.b4 & 0b00001111);

                _bytes.B.b1 &= 0b00001111;
                _bytes.B.b4 &= 0b11110000;
                _bytes.B.b1 |= (byte)(tus.b1 & 0b11110000);
                _bytes.B.b4 |= (byte)(tus.b4 & 0b00001111);
                _bytes.U.b1 += mud;
                _bytes.U.b4 += mud;

                _bytes.F.b1 += mud;
                _bytes.F.b4 += mud;

                _bytes.D.b1 += mud;
                _bytes.D.b4 += mud;

                _bytes.B.b1 += mud;
                _bytes.B.b4 += mud;
            }
            _numIndex += _offset;
        }

        private void S()
        {
            SideBytes tus;
            SideBytes trs;
            SideBytes tls;
            SideBytes tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.U.b2 -= mud;
                _bytes.U.b3 -= mud;

                _bytes.R.b1 -= mud;
                _bytes.R.b4 -= mud;

                _bytes.D.b2 -= mud;
                _bytes.D.b3 -= mud;

                _bytes.L.b1 -= mud;
                _bytes.L.b4 -= mud;
                tus = _bytes.U;
                trs = _bytes.R;
                tls = _bytes.L;
                tds = _bytes.D;

                _bytes.U.b2 &= 0b00001111;
                _bytes.U.b3 &= 0b11110000;
                _bytes.U.b2 |= (byte)(tls.b4 << 4 & 0b11110000);
                _bytes.U.b3 |= (byte)(tls.b1 >> 4 & 0b00001111);

                _bytes.R.b1 &= 0b00001111;
                _bytes.R.b4 &= 0b11110000;
                _bytes.R.b1 |= (byte)(tus.b2 & 0b11110000);
                _bytes.R.b4 |= (byte)(tus.b3 & 0b00001111);

                _bytes.D.b2 &= 0b00001111;
                _bytes.D.b3 &= 0b11110000;
                _bytes.D.b2 |= (byte)(trs.b4 << 4 & 0b11110000);
                _bytes.D.b3 |= (byte)(trs.b1 >> 4 & 0b00001111);

                _bytes.L.b1 &= 0b00001111;
                _bytes.L.b4 &= 0b11110000;
                _bytes.L.b1 |= (byte)(tds.b2 & 0b11110000);
                _bytes.L.b4 |= (byte)(tds.b3 & 0b00001111);
            }
            else
            {
                tus = _bytes.U;
                trs = _bytes.R;
                tls = _bytes.L;
                tds = _bytes.D;

                _bytes.U.b2 &= 0b00001111;
                _bytes.U.b3 &= 0b11110000;
                _bytes.U.b2 |= (byte)(tls.b4 << 4 & 0b11110000);
                _bytes.U.b3 |= (byte)(tls.b1 >> 4 & 0b00001111);
                _bytes.U.b2 += mud;
                _bytes.U.b3 += mud;

                _bytes.R.b1 &= 0b00001111;
                _bytes.R.b4 &= 0b11110000;
                _bytes.R.b1 |= (byte)(tus.b2 & 0b11110000);
                _bytes.R.b4 |= (byte)(tus.b3 & 0b00001111);
                _bytes.R.b1 += mud;
                _bytes.R.b4 += mud;

                _bytes.D.b2 &= 0b00001111;
                _bytes.D.b3 &= 0b11110000;
                _bytes.D.b2 |= (byte)(trs.b4 << 4 & 0b11110000);
                _bytes.D.b3 |= (byte)(trs.b1 >> 4 & 0b00001111);
                _bytes.D.b2 += mud;
                _bytes.D.b3 += mud;

                _bytes.L.b1 &= 0b00001111;
                _bytes.L.b4 &= 0b11110000;
                _bytes.L.b1 |= (byte)(tds.b2 & 0b11110000);
                _bytes.L.b4 |= (byte)(tds.b3 & 0b00001111);
                _bytes.L.b1 += mud;
                _bytes.L.b4 += mud;
            }
            _numIndex += _offset;
        }

        private void AntiS()
        {
            SideBytes tus;
            SideBytes trs;
            SideBytes tls;
            SideBytes tds;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.U.b2 -= mud;
                _bytes.U.b3 -= mud;

                _bytes.R.b1 -= mud;
                _bytes.R.b4 -= mud;

                _bytes.D.b2 -= mud;
                _bytes.D.b3 -= mud;

                _bytes.L.b1 -= mud;
                _bytes.L.b4 -= mud;
                tus = _bytes.U;
                trs = _bytes.R;
                tls = _bytes.L;
                tds = _bytes.D;
                _bytes.U.b2 &= 0b00001111;
                _bytes.U.b3 &= 0b11110000;
                _bytes.U.b2 |= (byte)(trs.b1 & 0b11110000);
                _bytes.U.b3 |= (byte)(trs.b4 & 0b00001111);

                _bytes.R.b1 &= 0b00001111;
                _bytes.R.b4 &= 0b11110000;
                _bytes.R.b1 |= (byte)(tds.b3 << 4 & 0b11110000);
                _bytes.R.b4 |= (byte)(tds.b2 >> 4 & 0b00001111);

                _bytes.D.b2 &= 0b00001111;
                _bytes.D.b3 &= 0b11110000;
                _bytes.D.b2 |= (byte)(tls.b1 & 0b11110000);
                _bytes.D.b3 |= (byte)(tls.b4 & 0b00001111);

                _bytes.L.b1 &= 0b00001111;
                _bytes.L.b4 &= 0b11110000;
                _bytes.L.b1 |= (byte)(tus.b3 << 4 & 0b11110000);
                _bytes.L.b4 |= (byte)(tus.b2 >> 4 & 0b00001111);
            }
            else
            {
                tus = _bytes.U;
                trs = _bytes.R;
                tls = _bytes.L;
                tds = _bytes.D;
                _bytes.U.b2 &= 0b00001111;
                _bytes.U.b3 &= 0b11110000;
                _bytes.U.b2 |= (byte)(trs.b1 & 0b11110000);
                _bytes.U.b3 |= (byte)(trs.b4 & 0b00001111);

                _bytes.R.b1 &= 0b00001111;
                _bytes.R.b4 &= 0b11110000;
                _bytes.R.b1 |= (byte)(tds.b3 << 4 & 0b11110000);
                _bytes.R.b4 |= (byte)(tds.b2 >> 4 & 0b00001111);

                _bytes.D.b2 &= 0b00001111;
                _bytes.D.b3 &= 0b11110000;
                _bytes.D.b2 |= (byte)(tls.b1 & 0b11110000);
                _bytes.D.b3 |= (byte)(tls.b4 & 0b00001111);

                _bytes.L.b1 &= 0b00001111;
                _bytes.L.b4 &= 0b11110000;
                _bytes.L.b1 |= (byte)(tus.b3 << 4 & 0b11110000);
                _bytes.L.b4 |= (byte)(tus.b2 >> 4 & 0b00001111);
                _bytes.U.b2 += mud;
                _bytes.U.b3 += mud;

                _bytes.R.b1 += mud;
                _bytes.R.b4 += mud;

                _bytes.D.b2 += mud;
                _bytes.D.b3 += mud;

                _bytes.L.b1 += mud;
                _bytes.L.b4 += mud;
            }
            _numIndex += _offset;
        }

        private void E()
        {
            SideBytes tfs;
            SideBytes trs;
            SideBytes tls;
            SideBytes tbs;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.F.b2 -= mud;
                _bytes.F.b3 -= mud;

                _bytes.R.b2 -= mud;
                _bytes.R.b3 -= mud;

                _bytes.B.b2 -= mud;
                _bytes.B.b3 -= mud;

                _bytes.L.b2 -= mud;
                _bytes.L.b3 -= mud;
                tfs = _bytes.F;
                trs = _bytes.R;
                tls = _bytes.L;
                tbs = _bytes.B;

                _bytes.F.b2 &= 0b00001111;
                _bytes.F.b3 &= 0b11110000;
                _bytes.F.b2 |= (byte)(tls.b2 & 0b11110000);
                _bytes.F.b3 |= (byte)(tls.b3 & 0b00001111);

                _bytes.R.b2 &= 0b00001111;
                _bytes.R.b3 &= 0b11110000;
                _bytes.R.b2 |= (byte)(tfs.b2 & 0b11110000);
                _bytes.R.b3 |= (byte)(tfs.b3 & 0b00001111);

                _bytes.B.b2 &= 0b00001111;
                _bytes.B.b3 &= 0b11110000;
                _bytes.B.b2 |= (byte)(trs.b3 << 4 & 0b11110000);
                _bytes.B.b3 |= (byte)(trs.b2 >> 4 & 0b00001111);

                _bytes.L.b2 &= 0b00001111;
                _bytes.L.b3 &= 0b11110000;
                _bytes.L.b2 |= (byte)(tbs.b3 << 4 & 0b11110000);
                _bytes.L.b3 |= (byte)(tbs.b2 >> 4 & 0b00001111);
            }
            else
            {
                tfs = _bytes.F;
                trs = _bytes.R;
                tls = _bytes.L;
                tbs = _bytes.B;
                _bytes.F.b2 &= 0b00001111;
                _bytes.F.b3 &= 0b11110000;
                _bytes.F.b2 |= (byte)(tls.b2 & 0b11110000);
                _bytes.F.b3 |= (byte)(tls.b3 & 0b00001111);
                _bytes.F.b2 += mud;
                _bytes.F.b3 += mud;

                _bytes.R.b2 &= 0b00001111;
                _bytes.R.b3 &= 0b11110000;
                _bytes.R.b2 |= (byte)(tfs.b2 & 0b11110000);
                _bytes.R.b3 |= (byte)(tfs.b3 & 0b00001111);
                _bytes.R.b2 += mud;
                _bytes.R.b3 += mud;

                _bytes.B.b2 &= 0b00001111;
                _bytes.B.b3 &= 0b11110000;
                _bytes.B.b2 |= (byte)(trs.b3 << 4 & 0b11110000);
                _bytes.B.b3 |= (byte)(trs.b2 >> 4 & 0b00001111);
                _bytes.B.b2 += mud;
                _bytes.B.b3 += mud;

                _bytes.L.b2 &= 0b00001111;
                _bytes.L.b3 &= 0b11110000;
                _bytes.L.b2 |= (byte)(tbs.b3 << 4 & 0b11110000);
                _bytes.L.b3 |= (byte)(tbs.b2 >> 4 & 0b00001111);
                _bytes.L.b2 += mud;
                _bytes.L.b3 += mud;
            }
            _numIndex += _offset;
        }

        private void AntiE()
        {
            SideBytes tfs;
            SideBytes trs;
            SideBytes tls;
            SideBytes tbs;
            var mud = (byte)_nums[_numIndex];
            if (_decryptMode)
            {
                _bytes.F.b2 -= mud;
                _bytes.F.b3 -= mud;

                _bytes.R.b2 -= mud;
                _bytes.R.b3 -= mud;

                _bytes.B.b2 -= mud;
                _bytes.B.b3 -= mud;

                _bytes.L.b2 -= mud;
                _bytes.L.b3 -= mud;
                tfs = _bytes.F;
                trs = _bytes.R;
                tls = _bytes.L;
                tbs = _bytes.B;
                _bytes.F.b2 &= 0b00001111;
                _bytes.F.b3 &= 0b11110000;
                _bytes.F.b2 |= (byte)(trs.b2 & 0b11110000);
                _bytes.F.b3 |= (byte)(trs.b3 & 0b00001111);

                _bytes.L.b2 &= 0b00001111;
                _bytes.L.b3 &= 0b11110000;
                _bytes.L.b2 |= (byte)(tfs.b2 & 0b11110000);
                _bytes.L.b3 |= (byte)(tfs.b3 & 0b00001111);

                _bytes.B.b2 &= 0b00001111;
                _bytes.B.b3 &= 0b11110000;
                _bytes.B.b2 |= (byte)(tls.b3 << 4 & 0b11110000);
                _bytes.B.b3 |= (byte)(tls.b2 >> 4 & 0b00001111);

                _bytes.R.b2 &= 0b00001111;
                _bytes.R.b3 &= 0b11110000;
                _bytes.R.b2 |= (byte)(tbs.b3 << 4 & 0b11110000);
                _bytes.R.b3 |= (byte)(tbs.b2 >> 4 & 0b00001111);
            }
            else
            {
                tfs = _bytes.F;
                trs = _bytes.R;
                tls = _bytes.L;
                tbs = _bytes.B;
                _bytes.F.b2 &= 0b00001111;
                _bytes.F.b3 &= 0b11110000;
                _bytes.F.b2 |= (byte)(trs.b2 & 0b11110000);
                _bytes.F.b3 |= (byte)(trs.b3 & 0b00001111);

                _bytes.L.b2 &= 0b00001111;
                _bytes.L.b3 &= 0b11110000;
                _bytes.L.b2 |= (byte)(tfs.b2 & 0b11110000);
                _bytes.L.b3 |= (byte)(tfs.b3 & 0b00001111);

                _bytes.B.b2 &= 0b00001111;
                _bytes.B.b3 &= 0b11110000;
                _bytes.B.b2 |= (byte)(tls.b3 << 4 & 0b11110000);
                _bytes.B.b3 |= (byte)(tls.b2 >> 4 & 0b00001111);

                _bytes.R.b2 &= 0b00001111;
                _bytes.R.b3 &= 0b11110000;
                _bytes.R.b2 |= (byte)(tbs.b3 << 4 & 0b11110000);
                _bytes.R.b3 |= (byte)(tbs.b2 >> 4 & 0b00001111);
                _bytes.F.b2 += mud;
                _bytes.F.b3 += mud;

                _bytes.R.b2 += mud;
                _bytes.R.b3 += mud;

                _bytes.B.b2 += mud;
                _bytes.B.b3 += mud;

                _bytes.L.b2 += mud;
                _bytes.L.b3 += mud;
            }
            _numIndex += _offset;
        }

        private void MoveSideClockwise(ref SideBytes side)
        {
            var ts = side;
            side.b1 = 0;
            side.b2 = 0;
            side.b3 = 0;
            side.b4 = 0;

            side.b1 |= (byte)(ts.b3 >> 4 & 0b00001111);
            side.b1 |= (byte)(ts.b2 & 0b11110000);

            side.b2 |= (byte)(ts.b1 & 0b00001111);
            side.b2 |= (byte)(ts.b4 << 4 & 0b11110000);

            side.b3 |= (byte)(ts.b1 >> 4 & 0b00001111);
            side.b3 |= (byte)(ts.b4 & 0b11110000);

            side.b4 |= (byte)(ts.b3 & 0b00001111);
            side.b4 |= (byte)(ts.b2 << 4 & 0b11110000);
        }

        private void MoveSideCounterClockwise(ref SideBytes side)
        {
            var ts = side;
            side.b1 = 0;
            side.b2 = 0;
            side.b3 = 0;
            side.b4 = 0;

            side.b1 |= (byte)(ts.b2 & 0b00001111);
            side.b1 |= (byte)(ts.b3 << 4 & 0b11110000);

            side.b2 |= (byte)(ts.b4 >> 4 & 0b00001111);
            side.b2 |= (byte)(ts.b1 & 0b11110000);

            side.b3 |= (byte)(ts.b4 & 0b00001111);
            side.b3 |= (byte)(ts.b1 << 4 & 0b11110000);

            side.b4 |= (byte)(ts.b2 >> 4 & 0b00001111);
            side.b4 |= (byte)(ts.b3 & 0b11110000);
        }
    }

    internal struct SideBytes
    {
        public byte b1;
        public byte b2;
        public byte b3;
        public byte b4;
    }

    internal struct CubeBytes
    {
        public SideBytes U;
        public SideBytes L;
        public SideBytes F;
        public SideBytes R;
        public SideBytes B;
        public SideBytes D;
    }
}
