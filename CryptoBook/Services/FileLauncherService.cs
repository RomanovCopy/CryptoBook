using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public sealed class FileLauncherService: IFileLauncherService
    {
        public LaunchResult Open(string target)
            => ShellExecute(new ShellLaunchOptions { Target = target, Verb = "open" });

        public LaunchResult Open(string target, string verb)
            => ShellExecute(new ShellLaunchOptions { Target = target, Verb = verb });

        public LaunchResult Print(string path)
            => ShellExecute(new ShellLaunchOptions { Target = path, Verb = "print" });

        public LaunchResult Edit(string path)
            => ShellExecute(new ShellLaunchOptions { Target = path, Verb = "edit" });

        public LaunchResult RunAsAdmin(string path, string? arguments = null)
            => ShellExecute(new ShellLaunchOptions
            {
                Target = path,
                Verb = "runas",
                Arguments = arguments,
                // важно: runas требует Shell
                UseShellExecute = true
            });

        public LaunchResult OpenWith(string applicationPath, string target, string? arguments = null, string? workingDirectory = null)
        {
            // applicationPath: путь к exe. target: файл/папка/URL (обычно файл)
            // arguments: дополнительные аргументы, если нужны (например: "-n")
            // Пример: notepad.exe "file.txt"
            try
            {
                if(string.IsNullOrWhiteSpace(applicationPath))
                    return LaunchResult.Fail("open-with", target, "Application path is empty.");

                if(!File.Exists(applicationPath))
                    return LaunchResult.Fail("open-with", target, $"Application not found: {applicationPath}");

                var arg = BuildArgs(arguments, QuoteIfNeeded(target));

                var psi = new ProcessStartInfo
                {
                    FileName = applicationPath,
                    Arguments = arg,
                    WorkingDirectory = workingDirectory ?? SafeGetDirectory(target),
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                var p = Process.Start(psi);
                return p is null
                    ? LaunchResult.Fail("open-with", target, "Process.Start returned null.")
                    : LaunchResult.Ok("open-with", target, p.Id);
            } catch(Exception ex)
            {
                return LaunchResult.Fail("open-with", target, ex.Message, ex);
            }
        }

        public LaunchResult ShellExecute(ShellLaunchOptions options)
        {
            try
            {
                if(options is null)
                    return LaunchResult.Fail("shell", "", "Options is null.");

                var target = options.Target?.Trim();
                if(string.IsNullOrWhiteSpace(target))
                    return LaunchResult.Fail("shell", "", "Target is empty.");

                // Для файлов: если это похоже на путь и файл/папка не существует — вернём fail (URL пропустим)
                if(LooksLikePath(target) && !File.Exists(target) && !Directory.Exists(target))
                    return LaunchResult.Fail("shell", target, $"Path not found: {target}");

                var psi = new ProcessStartInfo
                {
                    FileName = target,
                    UseShellExecute = options.UseShellExecute, // обычно true
                    Verb = string.IsNullOrWhiteSpace(options.Verb) ? "open" : options.Verb!,
                    Arguments = options.Arguments ?? string.Empty,
                    WorkingDirectory = options.WorkingDirectory ?? SafeGetDirectory(target),
                    WindowStyle = options.WindowStyle,
                    ErrorDialog = options.ErrorDialog
                };

                var p = Process.Start(psi);

                // Для Shell (например URL) процесс может быть null/не иметь Id в привычном смысле.
                // Поэтому считаем успехом отсутствие исключения.
                return p is null
                    ? LaunchResult.Ok($"shell:{psi.Verb}", target, null)
                    : LaunchResult.Ok($"shell:{psi.Verb}", target, p.Id);
            } catch(Exception ex)
            {
                return LaunchResult.Fail($"shell:{options?.Verb ?? "open"}", options?.Target ?? "", ex.Message, ex);
            }
        }

        public LaunchResult StartProcess(ProcessLaunchOptions options)
        {
            try
            {
                if(options is null)
                    return LaunchResult.Fail("process", "", "Options is null.");

                var fileName = options.FileName?.Trim();
                if(string.IsNullOrWhiteSpace(fileName))
                    return LaunchResult.Fail("process", "", "FileName is empty.");

                var useShell = options.UseShellExecute;

                // runas требует UseShellExecute = true
                if(options.RunAsAdmin)
                    useShell = true;

                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = options.Arguments ?? string.Empty,
                    WorkingDirectory = options.WorkingDirectory ?? SafeGetDirectory(fileName),
                    UseShellExecute = useShell,
                    CreateNoWindow = options.CreateNoWindow,
                    WindowStyle = options.WindowStyle
                };

                if(options.RunAsAdmin)
                    psi.Verb = "runas";

                // редиректы работают только при UseShellExecute=false
                if(!psi.UseShellExecute)
                {
                    psi.RedirectStandardOutput = options.RedirectStdOut;
                    psi.RedirectStandardError = options.RedirectStdErr;
                    psi.RedirectStandardInput = options.RedirectStdIn;
                }

                var p = Process.Start(psi);
                return p is null
                    ? LaunchResult.Fail("process", fileName, "Process.Start returned null.")
                    : LaunchResult.Ok("process", fileName, p.Id);
            } catch(Exception ex)
            {
                return LaunchResult.Fail("process", options?.FileName ?? "", ex.Message, ex);
            }
        }

        public LaunchResult RevealInExplorer(string path, bool select = true)
        {
            try
            {
                if(string.IsNullOrWhiteSpace(path))
                    return LaunchResult.Fail("explorer", "", "Path is empty.");

                path = path.Trim();

                if(File.Exists(path))
                {
                    // explorer.exe /select,"C:\file.txt"
                    var args = select ? $"/select,{QuoteIfNeeded(path)}" : QuoteIfNeeded(Path.GetDirectoryName(path) ?? path);
                    var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = args,
                        UseShellExecute = true
                    });

                    return p is null
                        ? LaunchResult.Ok("explorer:reveal", path, null)
                        : LaunchResult.Ok("explorer:reveal", path, p.Id);
                }

                if(Directory.Exists(path))
                {
                    var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = QuoteIfNeeded(path),
                        UseShellExecute = true
                    });

                    return p is null
                        ? LaunchResult.Ok("explorer:open", path, null)
                        : LaunchResult.Ok("explorer:open", path, p.Id);
                }

                return LaunchResult.Fail("explorer", path, $"Path not found: {path}");
            } catch(Exception ex)
            {
                return LaunchResult.Fail("explorer", path, ex.Message, ex);
            }
        }

        public LaunchResult RunCmd(string command, string? workingDirectory = null, bool runAsAdmin = false)
        {
            if(string.IsNullOrWhiteSpace(command))
                return LaunchResult.Fail("cmd", "", "Command is empty.");

            // cmd.exe /c "..."
            return StartProcess(new ProcessLaunchOptions
            {
                FileName = "cmd.exe",
                Arguments = $"/c {QuoteCmd(command)}",
                WorkingDirectory = workingDirectory,
                UseShellExecute = runAsAdmin, // если админ => true (иначе runas не сработает)
                RunAsAdmin = runAsAdmin,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            });
        }

        public LaunchResult RunPowerShell(string command, string? workingDirectory = null, bool runAsAdmin = false)
        {
            if(string.IsNullOrWhiteSpace(command))
                return LaunchResult.Fail("powershell", "", "Command is empty.");

            // Windows PowerShell (powershell.exe). Если нужно pwsh — можно заменить на "pwsh".
            var args = $"-NoProfile -ExecutionPolicy Bypass -Command {QuotePowerShell(command)}";

            return StartProcess(new ProcessLaunchOptions
            {
                FileName = "powershell.exe",
                Arguments = args,
                WorkingDirectory = workingDirectory,
                UseShellExecute = runAsAdmin,
                RunAsAdmin = runAsAdmin,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            });
        }

        // ---------------- helpers ----------------

        private static string SafeGetDirectory(string target)
        {
            try
            {
                if(!LooksLikePath(target))
                    return Environment.CurrentDirectory;

                var dir = Directory.Exists(target) ? target : Path.GetDirectoryName(target);
                return string.IsNullOrWhiteSpace(dir) ? Environment.CurrentDirectory : dir!;
            } catch
            {
                return Environment.CurrentDirectory;
            }
        }

        private static bool LooksLikePath(string s)
            => s.Contains(':') || s.StartsWith(@"\\") || s.StartsWith(@"/") || s.Contains(@"\") || s.Contains(@"/");

        private static string QuoteIfNeeded(string s)
            => s.Contains(' ') || s.Contains('\t') || s.Contains('"')
                ? $"\"{s.Replace("\"", "\\\"")}\""
                : s;

        private static string BuildArgs(string? prefixArgs, string tailArg)
            => string.IsNullOrWhiteSpace(prefixArgs) ? tailArg : $"{prefixArgs} {tailArg}";

        private static string QuoteCmd(string cmd)
        {
            // cmd.exe любит кавычки вокруг всей команды
            // Пример: /c "dir & pause"
            var escaped = cmd.Replace("\"", "\\\"");
            return $"\"{escaped}\"";
        }

        private static string QuotePowerShell(string ps)
        {
            // Безопаснее передать как строку в кавычках
            // PowerShell: -Command "<...>"
            // Экранируем двойные кавычки `"
            var escaped = ps.Replace("\"", "`\"");
            return $"\"{escaped}\"";
        }
    }
}
