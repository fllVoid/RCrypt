using RCryptLib;

namespace RCryptConsole
{
    internal class ConsoleDialog
    {
        RCubeEncoder encoder;
        readonly TextWriter _out = Console.Out;
        readonly TextReader _in = Console.In;
        const string forgotArgMessage = "You didn't enter an argument";
        const string failedMessage = "Failed to execute command";
        const string successMessage = "Done!";
        const string helpMessage =
            "\t'enc1' command to encrypt file in 1-bit mode: enc <filepath_(source)> <filepath_(result)> <key>" +
            "\n\t'enc4' command to encrypt file in 4-bit mode: enc <filepath_(source)> <filepath_(result)> <key>" +
            "\n\t'dec1' command to decrypt file in 1-bit mode: dec <filepath_(source)> <filepath_(result)> <key>" +
            "\n\t'dec4' command to decrypt file in 4-bit mode: dec <filepath_(source)> <filepath_(result)> <key>";

        public ConsoleDialog()
        {
            encoder = new RCubeEncoder(Console.Out, PrintProgress);
        }

        public void Start()
        {
            _out.WriteLine("Welcome to RCrypt");
            while (true)
            {
                _out.Write(">>");
                string? input = _in.ReadLine();
                if (input == null)
                    continue;
                var command = input.Split(' ');

                switch (command[0])
                {
                    case "enc1":
                        ExecuteCommandWithArguments(encoder.EncryptFile1BitMode, command.Skip(1).ToArray());
                        break;
                    case "dec1":
                        ExecuteCommandWithArguments(encoder.DecryptFile1BitMode, command.Skip(1).ToArray());
                        break;
                    case "enc4":
                        ExecuteCommandWithArguments(encoder.EncryptFile4BitMode, command.Skip(1).ToArray());
                        break;
                    case "dec4":
                        ExecuteCommandWithArguments(encoder.DecryptFile4BitMode, command.Skip(1).ToArray());
                        break;
                    case "help":
                        _out.WriteLine(helpMessage);
                        break;
                    case "exit":
                        return;
                    default:
                        _out.WriteLine("Unknown command");
                        break;
                }
            }
        }

        private void ExecuteCommandWithArguments(Func<string, string, string, bool> function, params string[] args)
        {
            if (args.Length < 3)
                _out.WriteLine(forgotArgMessage);
            else
            {
                if (function(args[0], args[1], args[2].ToUpper()))
                    _out.WriteLine($"\n{successMessage}");
                else
                    _out.WriteLine($"\n{failedMessage}");
            }
        }

        private void PrintProgress(double progress)
        {
            Console.SetCursorPosition(0, Console.GetCursorPosition().Top);
            _out.Write(new string(' ', 10));
            Console.SetCursorPosition(0, Console.GetCursorPosition().Top);
            _out.Write(String.Format("Progress: {0:P2}.\t", progress));
        }
    }
}
