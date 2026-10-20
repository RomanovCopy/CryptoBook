using LLama;
using LLama.Common;
using LLama.Native;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace CryptoBook.AI;

internal static class AiProgram
{
    [STAThread]
    public static int Main(string[] args)
    {
        if(args.Length == 1 && args[0] == "--ai-worker")
        {
            SetErrorMode(0x0001 | 0x0002); // No native crash dialog blocking cancellation/retry.
            try { return RunWorkerAsync().GetAwaiter().GetResult(); }
            catch { return 2; } // No exception details containing request data on stderr or disk.
        }
        App.Main();
        return Environment.ExitCode;
    }

    private static async Task<int> RunWorkerAsync()
    {
        using Stream input = Console.OpenStandardInput(), output = Console.OpenStandardOutput();
        LocalAiInferenceService? engine = null;
        bool configured = false;
        try
        {
            while(true)
            {
                var request = await AiWorkerProtocol.ReadAsync<AiWorkerRequest>(input, CancellationToken.None).ConfigureAwait(false);
                if(request is null) return 0;
                if(!request.Options.IsValid) return 2;
                if(!configured)
                {
                    NativeLibraryConfig.All.WithCuda(false).WithVulkan(request.Command != "inspect" && !request.Options.CpuBackend)
                        .WithAutoFallback(request.Command == "inspect" || request.Options.CpuBackend)
                        .WithLogCallback((_, _) => { });
                    configured = true;
                }
                var messages = Channel.CreateUnbounded<AiWorkerEvent>(new() { SingleReader = true });
                var pump = Task.Run(async () =>
                {
                    await foreach(var message in messages.Reader.ReadAllAsync().ConfigureAwait(false))
                        await AiWorkerProtocol.WriteAsync(output, message, CancellationToken.None).ConfigureAwait(false);
                });
                try
                {
                    request.Settings.Validate();
                    if(request.Command == "inspect")
                    {
                        using var vocabulary = await LLamaWeights.LoadFromFileAsync(new ModelParams(request.Settings.ModelPath)
                            { VocabOnly = true, GpuLayerCount = 0, LoadMode = LLamaLoadMode.MemoryMap }).ConfigureAwait(false);
                        var metadata = vocabulary.Metadata.Where(pair => pair.Key == "general.architecture" ||
                            pair.Key.EndsWith(".embedding_length_per_layer_input", StringComparison.Ordinal)).ToDictionary();
                        var plan = AiMemoryPlanner.Create(request.Settings, vocabulary.Metadata);
                        messages.Writer.TryWrite(new("inspection", Inspection: new(plan, metadata, HardwareKey(plan.GpuName))));
                    }
                    else if(request.Command is "probe" or "generate")
                    {
                        engine ??= new(request.Options);
                        var settings = request.Settings with { ContextLength = request.Options.ContextLength,
                            MemoryMode = AiMemoryMode.Manual, GpuLayers = request.Options.GpuLayers };
                        IReadOnlyList<AiTurn> turns;
                        if(request.Command == "probe")
                        {
                            settings = settings with { MaxTokens = 8, Temperature = 0, SystemPrompt = "", UseReasoning = false };
                            turns = [new("user", "Say OK.")];
                        }
                        else turns = request.Turns.Select(turn => turn.ToTurn()).ToArray();
                        void ReportUsage(AiTokenUsage usage)
                        {
                            if(request.Command == "generate") messages.Writer.TryWrite(new("usage", Usage: usage));
                        }
                        engine.TokenUsageChanged += ReportUsage;
                        try
                        {
                            await engine.GenerateAsync(settings, turns,
                                text => { if(request.Command == "generate") messages.Writer.TryWrite(new("text", Text: text)); },
                                new AiInlineProgress<float>(value => messages.Writer.TryWrite(new("progress", Progress: value))), CancellationToken.None,
                                text => { if(request.Command == "generate") messages.Writer.TryWrite(new("reasoning", Text: text)); }).ConfigureAwait(false);
                        }
                        finally { engine.TokenUsageChanged -= ReportUsage; }
                    }
                    else return 2;
                    messages.Writer.TryWrite(new("done"));
                }
                catch(AiRepetitionException error)
                {
                    messages.Writer.TryWrite(new("repetition", Repetition: error.Stop));
                }
                catch(Exception error)
                {
                    messages.Writer.TryWrite(new("error", Text: error is AiException ai ? ai.ResourceKey
                        : request.Command == "inspect" ? "AI.LoadFailed" : "AI.GenerationFailed"));
                }
                finally { messages.Writer.TryComplete(); await pump.ConfigureAwait(false); }
                request = null;
            }
        }
        finally { engine?.Dispose(); }
    }

    private static string HardwareKey(string gpuName)
    {
        string video = "";
        try
        {
            using var search = new ManagementObjectSearcher("SELECT Name,PNPDeviceID,DriverVersion FROM Win32_VideoController");
            search.Options.Timeout = TimeSpan.FromSeconds(5);
            using var results = search.Get();
            var devices = new List<string>();
            foreach(ManagementObject device in results)
                using(device) devices.Add($"{device["Name"]}|{device["PNPDeviceID"]}|{device["DriverVersion"]}");
            video = string.Join(';', devices.Order(StringComparer.Ordinal));
        }
        catch(Exception error) when(error is ManagementException or COMException or UnauthorizedAccessException) { }
        return $"{Environment.OSVersion}|{RuntimeInformation.ProcessArchitecture}|{Environment.ProcessorCount}|{AiMemoryPlanner.ReadTotalRam()}|{gpuName}|{video}";
    }
    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);
}

internal sealed class AiInlineProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
