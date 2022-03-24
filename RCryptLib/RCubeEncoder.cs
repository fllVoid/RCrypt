using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace RCryptLib
{
    public class RCubeEncoder
    {
        TextWriter _errorOutput;
        Action<double>? _printProgress;
        readonly string _otherLetters;
        readonly string _movesLetters = "BCDEFLMRSU";
        const int keySize = 24;

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
                (writeStream, decrypt, tupleKeySeed) => new RCryptStream1Bit(writeStream, decrypt, tupleKeySeed.Item1, tupleKeySeed.Item2));
        }

        public bool DecryptFile1BitMode(string sourcePath, string resultPath, string key)
        {
            return DecryptFile(sourcePath, resultPath, key,
                (writeStream, decrypt, tupleKeySeed) => new RCryptStream1Bit(writeStream, decrypt, tupleKeySeed.Item1, tupleKeySeed.Item2));
        }

        public bool EncryptFile4BitMode(string sourcePath, string resultPath, string key)
        {
            return EncryptFile(sourcePath, resultPath, key,
                (writeStream, decrypt, tupleKeySeed) => new RCryptStream4Bit(writeStream, decrypt, tupleKeySeed.Item1, tupleKeySeed.Item2));
        }

        public bool DecryptFile4BitMode(string sourcePath, string resultPath, string key)
        {
            return DecryptFile(sourcePath, resultPath, key,
                (writeStream, decrypt, tupleKeySeed) => new RCryptStream4Bit(writeStream, decrypt, tupleKeySeed.Item1, tupleKeySeed.Item2));
        }

        public bool EncryptFile(
            string sourcePath,
            string resultPath,
            string key,
            Func<Stream, bool, (string, ulong), RCryptStream> generateStream)
        {
            var seed = key.GetCustomHashCode();
            var tupleKeySeed = HandleKey(key);
            Task? printProgressTask = null;
            var cancelSource = new CancellationTokenSource();
            try
            {
                if (File.Exists(resultPath))
                    File.Delete(resultPath);
                using (var fileWriteStream = new BufferedStream(File.OpenWrite(resultPath), 1024 * 4))
                using (var readStream = new BufferedStream(File.OpenRead(sourcePath), 1024 * 4))
                using (var cryptStream = generateStream(fileWriteStream, false, tupleKeySeed))
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
            Func<Stream, bool, (string, ulong), RCryptStream> generateStream)
        {
            var seed = key.GetCustomHashCode();
            if (File.Exists(resultPath))
                File.Delete(resultPath);
            var tupleKeySeed = HandleKey(key);
            Task? printProgressTask = null;
            var cancelSource = new CancellationTokenSource();
            try
            {
                if (File.Exists(resultPath))
                    File.Delete(resultPath);
                using (var fileReadStream = new BufferedStream(File.OpenRead(sourcePath), 1024 * 4))
                using (var cryptStream = generateStream(fileReadStream, true, tupleKeySeed))
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

        private (string, ulong) HandleKey(string key)
        {
            var ba = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(key));
            var hex = $"0{BitConverter.ToString(ba).Replace("-", "")}";
            var hash = BigInteger.Parse(hex, System.Globalization.NumberStyles.AllowHexSpecifier).ToString();
            var result = new List<char>();
            for (int i = 0; i < keySize; ++i)
            {
                result.Add(_movesLetters[hash[i] - '0']);
                if (hash[hash.Length - 1 - i] > '4')
                    result.Add('\'');
            }
            var seedSource = hash.Substring(keySize, hash.Length - keySize * 2);
            var seed = UInt64.Parse(seedSource[..19]) * UInt64.Parse(seedSource[19..]);
            return (new string(result.ToArray()), seed);
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
