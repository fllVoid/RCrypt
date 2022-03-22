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
            var seed = key.GetHashCode();
            key = HandleKey(key);
            Task? printProgressTask = null;
            var cancelSource = new CancellationTokenSource();
            try
            {
                if (File.Exists(resultPath)) 
                    File.Delete(resultPath);
                using (var fileWriteStream = new BufferedStream(File.OpenWrite(resultPath), 1024 * 4))
                using (var readStream = new BufferedStream(File.OpenRead(sourcePath), 1024 * 4))
                using (var cryptStream = new RCryptStream1Bit(fileWriteStream, false, key, seed))
                {
                    var progress = new Progress(readStream);
                    if (_printProgress != null)
                        printProgressTask = Task.Run(() => PrintProgress(cancelSource.Token, progress, _printProgress));
                    var buffer = new byte[6];
                    var read = 6;
                    while (!cryptStream.LastWrite)
                    {
                        read = readStream.Read(buffer, 0, 6);
                        if (read < 6 || readStream.Position == readStream.Length)
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

        public bool DecryptFile1BitMode(string sourcePath, string resultPath, string key)
        {
            var seed = key.GetHashCode();
            key = HandleKey(key);
            Task? printProgressTask = null;
            var cancelSource = new CancellationTokenSource();
            try
            {
                if (File.Exists(resultPath))
                    File.Delete(resultPath);
                using (var fileReadStream = new BufferedStream(File.OpenRead(sourcePath), 1024 * 4))
                using (var readStream = new RCryptStream1Bit(fileReadStream, true, ReverseScramble(key), seed))
                using (var writeStream = new BufferedStream(File.OpenWrite(resultPath), 1024 * 4))
                {
                    var progress = new Progress(fileReadStream);
                    if (_printProgress != null)
                        printProgressTask = Task.Run(() => PrintProgress(cancelSource.Token, progress, _printProgress));
                    var buffer = new byte[6];
                    var read = 6;
                    while (fileReadStream.Length != fileReadStream.Position)
                    {
                        read = readStream.Read(buffer, 0, 6);
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

        public bool EncryptFile4BitMode(string sourcePath, string resultPath, string key)
        {
            var seed = key.GetHashCode();
            key = HandleKey(key);
            Task? printProgressTask = null;
            var cancelSource = new CancellationTokenSource();
            try
            {
                if (File.Exists(resultPath))
                    File.Delete(resultPath);
                using (var fileWriteStream = new BufferedStream(File.OpenWrite(resultPath), 1024 * 4))
                using (var readStream = new BufferedStream(File.OpenRead(sourcePath), 1024 * 4))
                using (var cryptStream = new RCryptStream4Bit(fileWriteStream, false, key, seed))
                {
                    var progress = new Progress(readStream);
                    if (_printProgress != null)
                        printProgressTask = Task.Run(() => PrintProgress(cancelSource.Token, progress, _printProgress));
                    var buffer = new byte[24];
                    var read = 24;
                    while (!cryptStream.LastWrite)
                    {
                        read = readStream.Read(buffer, 0, 24);
                        if (read < 24 || readStream.Position == readStream.Length)
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

        public bool DecryptFile4BitMode(string sourcePath, string resultPath, string key)
        {
            var seed = key.GetHashCode();
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
                using (var readStream = new RCryptStream4Bit(fileReadStream, true, ReverseScramble(key), seed))
                using (var writeStream = new BufferedStream(File.OpenWrite(resultPath), 1024 * 4))
                {
                    var progress = new Progress(fileReadStream);
                    if (_printProgress != null)
                        printProgressTask = Task.Run(() => PrintProgress(cancelSource.Token, progress, _printProgress));
                    var buffer = new byte[24];
                    var read = 24;
                    while (fileReadStream.Length != fileReadStream.Position)
                    {
                        read = readStream.Read(buffer, 0, 24);
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

        private void PrintProgress(CancellationToken token, Progress progress, Action<double> printFunc)
        {
            while (!token.IsCancellationRequested)
            {
                Thread.Sleep(250);
                printFunc(progress.Get);
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

        private class Progress
        {
            Stream _stream;

            public Progress(Stream stream) => _stream = stream;

            public double Get => _stream.CanRead ? (double)_stream.Position / _stream.Length : 0;
        }
    }
}
