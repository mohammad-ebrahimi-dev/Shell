using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace MainProgram
{
    public class Program
    {
        public static List<string> history = new List<string>();

        private static void Main(string[] args)
        {
            Console.BackgroundColor = ConsoleColor.Gray;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Clear();

            Console.WriteLine("Welcome to MiniShell (.NET). Type 'help' for commands.");

            while (true)
            {
                try
                {
                    var cwd = Directory.GetCurrentDirectory();
                    Console.Write($"{Path.GetFileName(cwd)}> ");
                    var line = ReadCommandLine();

                    if (line is null) break;

                    line = line.Trim();
                    if (line is "") continue;

                    var parts = line.Split(" ");
                    var cmd = parts[0];
                    var cmdArgs = parts.Skip(1).ToArray();

                    if (!cmd.Equals("history", StringComparison.OrdinalIgnoreCase))
                        history.Add(line);

                    var result = Commands(cmd, cmdArgs);

                    if (result == CommandResult.Exit)
                        break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        public static CommandResult Commands(string cmd, string[] cmdArgs)
        {
            if (cmd.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Good Bye!");
                return CommandResult.Exit;
            }

            if (cmd.Equals("cls", StringComparison.OrdinalIgnoreCase))
            {
                Console.Clear();
                return CommandResult.Continue;
            }

            if (cmd.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Built-ins: help, exit, cd, history, cls, pwd, ls, cat");
                Console.WriteLine("External commands: run like in terminal (e.g. dotnet --info)");
                return CommandResult.Continue;
            }

            if (cmd.Equals("history", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in history)
                    Console.WriteLine(item);
                return CommandResult.Continue;
            }

            if (cmd.Equals("pwd", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(Directory.GetCurrentDirectory());
                return CommandResult.Continue;
            }

            if (cmd.Equals("ls", StringComparison.OrdinalIgnoreCase))
            {
                var path = cmdArgs.Length > 0 ? cmdArgs[0] : ".";
                var entries = Directory.GetFileSystemEntries(path);
                foreach (var e in entries)
                {
                    Console.WriteLine(Path.GetFileName(e));
                }
                return CommandResult.Continue;
            }

            if (cmd.Equals("cat", StringComparison.OrdinalIgnoreCase))
            {
                if (cmdArgs.Length == 0)
                {
                    Console.WriteLine("Usage: cat <filename>");
                    return CommandResult.Continue;
                }

                foreach (var file in cmdArgs)
                {
                    if (File.Exists(file))
                        Console.WriteLine(File.ReadAllText(file));
                    else
                        Console.WriteLine($"cat: {file}: No such file");
                }

                return CommandResult.Continue;
            }

            if (cmd.Equals("cd", StringComparison.OrdinalIgnoreCase))
            {
                if (cmdArgs.Length == 0)
                {
                    Console.WriteLine("Usage: cd <directory-path>");
                    return CommandResult.Continue;
                }

                var path = cmdArgs[0];
                string newPath;

                if (path == "..")
                {
                    newPath = Directory.GetParent(Directory.GetCurrentDirectory())?.FullName
                        ?? Directory.GetCurrentDirectory();
                }
                else
                {
                    newPath = Path.GetFullPath(path, Directory.GetCurrentDirectory());
                }

                try
                {
                    Directory.SetCurrentDirectory(newPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"cd: {ex.Message}");
                }

                return CommandResult.Continue;
            }

            try
            {
                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = cmd;
                process.StartInfo.Arguments = string.Join(" ", cmdArgs);
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (!string.IsNullOrEmpty(output))
                    Console.WriteLine(output);
                if (!string.IsNullOrEmpty(error))
                    Console.WriteLine(error);
            }
            catch
            {
                Console.WriteLine($"Command not found: {cmd}");
            }

            return CommandResult.Continue;
        }

        static string ReadCommandLine()
        {
            var input = new StringBuilder();

            while (true)
            {
                var key = Console.ReadKey(intercept: true);

                // اگر Enter زده شد، خروجی رو برگردون
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return input.ToString();
                }

                // اگر Backspace زد
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (input.Length > 0)
                    {
                        input.Remove(input.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                    continue;
                }

                // اگر Tab زد → autocomplete پوشه‌ها
                if (key.Key == ConsoleKey.Tab)
                {
                    HandleTabCompletion(input);
                    continue;
                }

                // حرف عادی
                input.Append(key.KeyChar);
                Console.Write(key.KeyChar);
            }
        }
        static void HandleTabCompletion(StringBuilder input)
        {
            var text = input.ToString().Trim();

            // بررسی کن آیا دستور cd هست یا نه
            if (!text.StartsWith("cd ", StringComparison.OrdinalIgnoreCase))
                return;

            var currentArg = text.Substring(3).Trim();

            string baseDir;
            string searchPattern;

            if (string.IsNullOrEmpty(currentArg))
            {
                baseDir = Directory.GetCurrentDirectory();
                searchPattern = "";
            }
            else
            {
                // اگه کاربر چیزی تایپ کرده، مسیر رو جدا کن
                if (Directory.Exists(currentArg))
                {
                    baseDir = Path.GetFullPath(currentArg);
                    searchPattern = "";
                }
                else
                {
                    baseDir = Path.GetDirectoryName(Path.GetFullPath(currentArg, Directory.GetCurrentDirectory()))
                              ?? Directory.GetCurrentDirectory();
                    searchPattern = Path.GetFileName(currentArg);
                }
            }

            // همه‌ی دایرکتوری‌هایی که با searchPattern شروع میشن
            var dirs = Directory.GetDirectories(baseDir)
                .Select(Path.GetFileName)
                .Where(d => d != null && d.StartsWith(searchPattern, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (dirs.Count == 0)
                return;

            if (dirs.Count == 1)
            {
                // فقط یک گزینه پیدا شد → خودش رو auto-complete کن
                var remaining = dirs[0].Substring(searchPattern.Length);
                Console.Write(remaining);
                input.Append(remaining);
            }
            else
            {
                // چند گزینه پیدا شد → نمایش همه
                Console.WriteLine();
                foreach (var d in dirs)
                    Console.WriteLine("  " + d);

                Console.Write($"{Path.GetFileName(Directory.GetCurrentDirectory())}> cd {currentArg}");
            }
        }

        public enum CommandResult
        {
            Continue,
            Exit
        }
    }
}
