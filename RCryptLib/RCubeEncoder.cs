namespace RCryptLib
{
    public class RCubeEncoder
    {
        TextWriter _errorOutput;
        Action<double>? _printProgress;
        const string _otherLetters = "ACGHIJKNOPQTVWXYZ134567890";
        const string _movesLetters = "BDEFLMRSU";

        public RCubeEncoder(TextWriter errorOutput, Action<double>? printProgress)
        {
            _errorOutput = errorOutput;
            _printProgress = printProgress;
        }

        public bool EncryptFile1BitMode(string sourcePath, string resultPath, string key)
        {
            key = HandleKey(key);
            Task? printProgressTask = null;
            bool finish = false;
            try
            {
                using (var fileReadStream = File.OpenRead(sourcePath))
                using (var fileWriteStream = File.OpenWrite(resultPath))
                using (var readStream = new BufferedStream(fileReadStream, 1024))
                using (var cryptStream = new RCryptStream1Bit(fileWriteStream, false, key))
                {
                    if (_printProgress != null)
                    {
                        printProgressTask = new Task(() =>
                        {
                            while (!finish)
                            {
                                Thread.Sleep(250);
                                _printProgress((double)fileReadStream.Position / fileReadStream.Length);
                            }
                        });
                    }
                    printProgressTask?.Start();
                    var buffer = new byte[6];
                    var read = 6;
                    while (!cryptStream.LastWrite)
                    {
                        read = readStream.Read(buffer, 0, 6);
                        if (read < 6 || readStream.Position == readStream.Length)
                            cryptStream.LastWrite = true;
                        cryptStream.Write(buffer, 0, read);
                    }
                    finish = true;
                    printProgressTask?.Wait();
                    return true;
                }
            }
            catch (Exception ex)
            {
                finish = true;
                printProgressTask?.Wait();
                _errorOutput.WriteLine(ex.Message);
                return false;
            }

        }

        public bool DecryptFile1BitMode(string sourcePath, string resultPath, string key)
        {
            key = HandleKey(key);
            try
            {
                using (var fileReadStream = File.OpenRead(sourcePath))
                using (var fileWriteStream = File.OpenWrite(resultPath))
                using (var readStream = new RCryptStream1Bit(fileReadStream, true, ReverseScramble(key)))
                using (var writeStream = new BufferedStream(fileWriteStream, 1024))
                {
                    var buffer = new byte[6];
                    var read = 6;
                    while (fileReadStream.Length != fileReadStream.Position)
                    {
                        read = readStream.Read(buffer, 0, 6);
                        writeStream.Write(buffer, 0, read);
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                _errorOutput.WriteLine(ex.Message);
                return false;
            }
        }

        public bool EncryptFile4BitMode(string sourcePath, string resultPath, string key)
        {
            key = HandleKey(key);
            Task? printProgressTask = null;
            bool finish = false;
            try
            {
                using (var fileReadStream = File.OpenRead(sourcePath))
                using (var fileWriteStream = File.OpenWrite(resultPath))
                using (var readStream = new BufferedStream(fileReadStream, 4096))
                using (var cryptStream = new RCryptStream4Bit(fileWriteStream, false, key))
                {
                    if (_printProgress != null)
                    {
                        printProgressTask = new Task(() =>
                        {
                            while (!finish)
                            {
                                Thread.Sleep(250);
                                _printProgress((double)fileReadStream.Position / fileReadStream.Length);
                            }
                        });
                    }
                    printProgressTask?.Start();
                    var buffer = new byte[24];
                    var read = 24;
                    while (!cryptStream.LastWrite)
                    {
                        read = readStream.Read(buffer, 0, 24);
                        if (read < 24 || readStream.Position == readStream.Length)
                            cryptStream.LastWrite = true;
                        cryptStream.Write(buffer, 0, read);
                    }
                    finish = true;
                    printProgressTask?.Wait();
                    return true;
                }
            }
            catch (Exception ex)
            {
                finish = true;
                printProgressTask?.Wait();
                _errorOutput.WriteLine(ex.Message);
                return false;
            }

        }

        public bool DecryptFile4BitMode(string sourcePath, string resultPath, string key)
        {
            key = HandleKey(key);
            try
            {
                using (var fileReadStream = File.OpenRead(sourcePath))
                using (var fileWriteStream = File.OpenWrite(resultPath))
                using (var readStream = new RCryptStream4Bit(fileReadStream, true, ReverseScramble(key)))
                using (var writeStream = new BufferedStream(fileWriteStream, 4096))
                {
                    var buffer = new byte[24];
                    var read = 24;
                    while (fileReadStream.Length != fileReadStream.Position)
                    {
                        read = readStream.Read(buffer, 0, 24);
                        writeStream.Write(buffer, 0, read);
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                _errorOutput.WriteLine(ex.Message);
                return false;
            }
        }

        private string ReverseScramble(string s)
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
                else if (result[i] == '2' && i + 1 != result.Length)
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

        private string HandleKey(string key)
        {
            var outKey = key.ToArray();
            for (int i = 0; i < outKey.Length; i++)
            {
                if (!_movesLetters.Contains(outKey[i]))
                {
                    var index = _otherLetters.IndexOf(outKey[i]);
                    if (index != -1)
                        outKey[i] = _movesLetters[index % _movesLetters.Length];
                }
            }
            return new string(outKey);
        }
    }
}
