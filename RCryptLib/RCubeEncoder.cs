namespace RCryptLib
{
    public class RCubeEncoder
    {
        TextWriter _errorOutput;
        Action<double>? _printProgress;
        readonly string _otherLetters;
        readonly string _movesLetters = "BDEFLMRSU";

        public RCubeEncoder(TextWriter errorOutput, Action<double>? printProgress)
        {
            var alph = "ACGHIJKNOPQTVWXYZ134567890";
            _errorOutput = errorOutput;
            _printProgress = printProgress;
            _otherLetters = Shuffle(alph, new Random(alph.GetCustomHashCode()));
        }

        public bool EncryptFile1BitMode(string sourcePath, string resultPath, string key)
        {
            return EncryptFile(sourcePath, resultPath, key,
                (writeStream, decrypt, scramble, seed) => new RCryptStream1Bit(writeStream, decrypt, scramble, seed));
        }

        public bool DecryptFile1BitMode(string sourcePath, string resultPath, string key)
        {
            return DecryptFile(sourcePath, resultPath, key,
                (writeStream, decrypt, scramble, seed) => new RCryptStream1Bit(writeStream, decrypt, scramble, seed));
        }

        public bool EncryptFile4BitMode(string sourcePath, string resultPath, string key)
        {
            return EncryptFile(sourcePath, resultPath, key,
                (writeStream, decrypt, scramble, seed) => new RCryptStream4Bit(writeStream, decrypt, scramble, seed));
        }

        public bool DecryptFile4BitMode(string sourcePath, string resultPath, string key)
        {
            return DecryptFile(sourcePath, resultPath, key,
                (writeStream, decrypt, scramble, seed) => new RCryptStream4Bit(writeStream, decrypt, scramble, seed));
        }

        public bool EncryptFile(
            string sourcePath,
            string resultPath,
            string key,
            Func<Stream, bool, string, int, RCryptStream> generateStream)
        {
            var seed = key.GetCustomHashCode();
            key = HandleKey(key);
            Task? printProgressTask = null;
            var cancelSource = new CancellationTokenSource();
            try
            {
                if (File.Exists(resultPath))
                    File.Delete(resultPath);
                using (var fileWriteStream = new BufferedStream(File.OpenWrite(resultPath), 1024 * 4))
                using (var readStream = new BufferedStream(File.OpenRead(sourcePath), 1024 * 4))
                using (var cryptStream = generateStream(fileWriteStream, false, key, seed))
                {
                    var bufferSize = cryptStream.BlockSizeInBytes;
                    var progress = new Progress(readStream);
                    if (_printProgress != null)
                        printProgressTask = PrintProgress(cancelSource.Token, progress, _printProgress);
                    var buffer = new byte[bufferSize];
                    var read = bufferSize;
                    while (!cryptStream.LastWrite)
                    {
                        read = readStream.Read(buffer, 0, bufferSize);
                        if (read < bufferSize || readStream.Position == readStream.Length)
                            cryptStream.LastWrite = true;
                        cryptStream.Write(buffer, 0, read);
                    }
                    cancelSource.Cancel();
                    printProgressTask?.Wait();
                    return true;
                }
            }
            catch (Exception ex)
            {
                cancelSource.Cancel();
                printProgressTask?.Wait();
                _errorOutput.WriteLine(ex.Message);
                return false;
            }
        }

        public bool DecryptFile(
            string sourcePath,
            string resultPath,
            string key,
            Func<Stream, bool, string, int, RCryptStream> generateStream)
        {
            var seed = key.GetCustomHashCode();
            if (File.Exists(resultPath))
                File.Delete(resultPath);
            key = HandleKey(key);
            Task? printProgressTask = null;
            var cancelSource = new CancellationTokenSource();
            try
            {
                if (File.Exists(resultPath))
                    File.Delete(resultPath);
                using (var fileReadStream = new BufferedStream(File.OpenRead(sourcePath), 1024 * 4))
                using (var cryptStream = generateStream(fileReadStream, true, ScrambleHelper.ReverseScramble(key), seed))
                using (var writeStream = new BufferedStream(File.OpenWrite(resultPath), 1024 * 4))
                {
                    var bufferSize = cryptStream.BlockSizeInBytes;
                    var progress = new Progress(fileReadStream);
                    if (_printProgress != null)
                        printProgressTask = PrintProgress(cancelSource.Token, progress, _printProgress);
                    var buffer = new byte[bufferSize];
                    var read = bufferSize;
                    while (fileReadStream.Length != fileReadStream.Position)
                    {
                        read = cryptStream.Read(buffer, 0, bufferSize);
                        writeStream.Write(buffer, 0, read);
                    }
                    cancelSource.Cancel();
                    printProgressTask?.Wait();
                    return true;
                }
            }
            catch (Exception ex)
            {
                cancelSource.Cancel();
                printProgressTask?.Wait();
                _errorOutput.WriteLine(ex.Message);
                return false;
            }
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

        private string Shuffle(string str, Random random)
        {
            var chars = str.ToCharArray();
            for (int i = str.Length - 1; i >= 1; i--)
            {
                int j = random.Next(i + 1);
                var temp = str[j];
                chars[j] = str[i];
                chars[i] = temp;
            }
            return new string(chars);
        }

        private async Task PrintProgress(CancellationToken token, Progress progress, Action<double> printFunc)
        {
            do
            {
                await Task.Delay(100);
                printFunc(progress.Get);
            }
            while (!token.IsCancellationRequested);
        }

        private class Progress
        {
            Stream _stream;

            public Progress(Stream stream) => _stream = stream;

            public double Get => _stream.CanRead ? (double)_stream.Position / _stream.Length : 0;
        }
    }
}
