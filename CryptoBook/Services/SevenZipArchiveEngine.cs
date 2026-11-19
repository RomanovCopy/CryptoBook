using CryptoBook.Interfaces;

using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CryptoBook.Services;

internal sealed class SevenZipArchiveEngine
{
    private static readonly UTF8Encoding Utf8 = new(false);
    private static readonly object RuntimeLock = new();
    private const string ExeHash = "DC0493429FE28136FCB177A0E77C9875CBB3B0BFF5F2F8EEBA4DF302E98CEF76";
    private const string DllHash = "500DB9EB17F3C21B729BA41DD02ECFE1823DA05028C7E4BA129084CC74CC364E";
    private readonly string runtimeDirectory;

    internal SevenZipArchiveEngine(string? runtimeDirectory = null) => this.runtimeDirectory = runtimeDirectory ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CryptoBook", "ArchiveEngine", "26.06");

    public async Task<string> RunAsync(IEnumerable<string> arguments, string? password,
        IProgressReporter? progress, CancellationToken cancellationToken,
        string? workingDirectory = null, Func<Stream, CancellationToken, Task>? readOutput = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(password?.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new ArgumentException(ArchivePathPolicy.Text("InvalidPassword"));
        string executable = PrepareRuntime();
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = Utf8, StandardOutputEncoding = Utf8, StandardErrorEncoding = Utf8,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executable)!
        };
        foreach(string argument in arguments) start.ArgumentList.Add(argument);
        start.ArgumentList.Add("-sccUTF-8");
        start.ArgumentList.Add("-bse2");
        start.ArgumentList.Add(progress is null ? "-bsp0" : "-bsp2");
        using var process = new Process { StartInfo = start };
        process.Start();
        using var registration = cancellationToken.Register(() => Kill(process));
        Task<string> errors = ReadErrorsAsync(process.StandardError, progress);
        string output = string.Empty;
        try
        {
            // A redirected input pipe keeps passwords out of process arguments and logs.
            // EOF also prevents an unattended password/confirmation prompt from hanging.
            await process.StandardInput.WriteAsync(((password ?? string.Empty) + "\n").AsMemory(), cancellationToken);
            process.StandardInput.Close();
            if(readOutput is null)
                output = await ReadBoundedAsync(process.StandardOutput, 32 * 1024 * 1024, cancellationToken);
            else
                await readOutput(process.StandardOutput.BaseStream, cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            string error = await errors;
            cancellationToken.ThrowIfCancellationRequested();
            if(process.ExitCode != 0)
                throw new InvalidDataException(ArchivePathPolicy.Text("EngineError") +
                    Environment.NewLine + error.Trim());
            return output;
        }
        finally
        {
            Kill(process);
            await process.WaitForExitAsync(CancellationToken.None);
            await errors;
        }
    }

    private string PrepareRuntime()
    {
        lock(RuntimeLock)
        {
            ArchivePathPolicy.EnsureNoLinks(runtimeDirectory);
            Directory.CreateDirectory(runtimeDirectory);
            Extract("7z.exe", ExeHash);
            Extract("7z.dll", DllHash);
            Extract("License.txt", null);
            Extract("Copying.txt", null);
            Extract("UnRAR-License.txt", null);
        }
        return Path.Combine(runtimeDirectory, "7z.exe");

        void Extract(string name, string? hash)
        {
            string target = Path.Combine(runtimeDirectory, name);
            if(!File.Exists(target))
            {
                string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using(var source = typeof(SevenZipArchiveEngine).Assembly.GetManifestResourceStream(
                        "CryptoBook.ArchiveEngine." + name) ??
                        throw new FileNotFoundException(ArchivePathPolicy.Text("EngineMissing")))
                    using(var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        source.CopyTo(output);
                    if(hash is not null) Verify(temporary, hash);
                    try { File.Move(temporary, target); }
                    catch(IOException) when(File.Exists(target)) { }
                }
                finally { if(File.Exists(temporary)) File.Delete(temporary); }
            }
            ArchivePathPolicy.EnsureNoLinks(target);
            if(hash is not null) Verify(target, hash);
        }

        static void Verify(string path, string expected)
        {
            if(!File.Exists(path)) throw new FileNotFoundException(ArchivePathPolicy.Text("EngineMissing"));
            using var stream = File.OpenRead(path);
            if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(ArchivePathPolicy.Text("EngineInvalid"));
        }
    }

    private static void Kill(Process process)
    {
        try { if(!process.HasExited) process.Kill(entireProcessTree: true); }
        catch(InvalidOperationException) { }
        catch(System.ComponentModel.Win32Exception) { }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int maximum,
        CancellationToken token)
    {
        var text = new StringBuilder();
        int bytes = 0;
        char[] buffer = new char[4096];
        int count;
        while((count = await reader.ReadAsync(buffer.AsMemory(), token)) != 0)
        {
            bytes += Utf8.GetByteCount(buffer, 0, count);
            if(bytes > maximum)
                throw new InvalidDataException(ArchivePathPolicy.Text("TooManyEntries"));
            text.Append(buffer, 0, count);
        }
        return text.ToString();
    }

    private static async Task<string> ReadErrorsAsync(StreamReader reader, IProgressReporter? progress)
    {
        var errors = new StringBuilder();
        var line = new StringBuilder();
        char[] buffer = new char[1024];
        int count;
        while((count = await reader.ReadAsync(buffer)) != 0)
        {
            foreach(char value in buffer.AsSpan(0, count))
            {
                if(value is '\r' or '\n')
                {
                    string text = line.ToString();
                    Match percent = Regex.Match(text, @"^\s*(\d{1,3})%", RegexOptions.CultureInvariant);
                    if(percent.Success)
                    {
                        if(int.TryParse(percent.Groups[1].Value, out int number))
                            progress?.Report(Math.Clamp(number / 100d, 0, 1));
                    }
                    else if(errors.Length < 64 * 1024) errors.AppendLine(text);
                    line.Clear();
                }
                else if(line.Length < 4096) line.Append(value);
            }
        }
        if(line.Length > 0 && errors.Length < 64 * 1024) errors.Append(line);
        return errors.ToString();
    }
}
