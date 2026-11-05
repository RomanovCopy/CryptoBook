using System.IO;
using System.Text.Json;
using System.Runtime.InteropServices;

// Loaded only through DOTNET_STARTUP_HOOKS during an explicitly configured package check.
// This assembly is a tool and is never included in the application/installer.
public static class StartupHook
{
    public static void Initialize()
    {
        string? configuration = Environment.GetEnvironmentVariable("CRYPTOBOOK_HELP_PACKAGE_CHECK");
        if (string.IsNullOrEmpty(configuration))
            throw new InvalidOperationException("Package validation requires an explicit configuration.");
        string[] args = JsonSerializer.Deserialize<string[]>(File.ReadAllText(configuration))
            ?? throw new InvalidDataException("Empty package-check configuration.");
        if (args.Length != 4 || args[3] != "stage12-package")
            throw new InvalidDataException("Only stage12-package can run through this hook.");

        int result = 1;
        var thread = new Thread(() =>
        {
            try { result = Program.Main(args); }
            catch (Exception error) { Console.Error.WriteLine(error); }
            // App.Run has completed OnExit and all reports are closed. Terminate
            // before this STA thread exits: COM/WPF thread teardown can invoke
            // managed callbacks after the CLR has destroyed its thread state.
            // This owned validation process must never enter the user's app.
            TerminateProcess(GetCurrentProcess(), (uint)result);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        throw new InvalidOperationException("Unable to terminate the package-check process.");
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);
}
