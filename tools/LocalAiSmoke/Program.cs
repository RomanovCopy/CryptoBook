using CryptoBook.AI;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

if(args.Length > 0 && args[0] == "--worker-owner")
{
    using var worker = new AiWorkerProcess();
    await worker.ExecuteAsync(new("inspect", new() { ModelPath = args[1] }, new(4096, 0, CpuBackend: true), []), _ => { }, null, CancellationToken.None);
    Console.WriteLine(worker.ProcessId);
    await Task.Delay(Timeout.Infinite);
    return;
}
if(args.Length > 0 && args[0] is "--adaptive" or "--adaptive-cpu")
{
    await AdaptiveProbe.RunAsync(args[1], args[2], args[0] == "--adaptive-cpu");
    return;
}
if(args.Length > 0 && args[0] == "--resources")
{
    await ResourceProbe.RunAsync(args[1], args[2]);
    return;
}
if(args.Length > 0 && args[0] == "--image-profile")
{
    var uiThread = new Thread(() => ImageProfileSmoke.Run(args[1], args[2], args[3], args.Length > 4 ? int.Parse(args[4]) : null));
    uiThread.SetApartmentState(ApartmentState.STA);
    uiThread.Start(); uiThread.Join();
    return;
}
if(args.Length > 0 && args[0] == "--probe")
{
    await ModelProbe.RunAsync(args[1], args[2], args.Length > 3 && args[3] == "cpu" ? AiMemoryMode.CpuOnly : AiMemoryMode.Automatic);
    return;
}
if(args.Length > 0 && args[0] == "--reasoning-probe")
{
    await ReasoningProbe.RunAsync(args[1], args[2]);
    return;
}
if(args.Length > 0 && args[0] == "--chat-appearance")
{
    var uiThread = new Thread(() => ChatAppearanceSmoke.Run(args[1]));
    uiThread.SetApartmentState(ApartmentState.STA);
    uiThread.Start(); uiThread.Join();
    return;
}
if(args.Length > 0 && args[0] == "--ui")
{
    var uiThread = new Thread(() => UiPreview.Run(args[1], args[2]));
    uiThread.SetApartmentState(ApartmentState.STA);
    uiThread.Start(); uiThread.Join();
    return;
}
if(args.Length > 0 && args[0] == "--settings-regression")
{
    var uiThread = new Thread(() => SettingsRegression.Run(args[1], args[2], args[3]));
    uiThread.SetApartmentState(ApartmentState.STA);
    uiThread.Start(); uiThread.Join();
    return;
}
if(args.Length > 0 && args[0] is "--send-button" or "--reasoning-ui")
{
    var uiThread = new Thread(() => SendButtonSmoke.Run(args[1], args[2], args[0] == "--reasoning-ui"));
    uiThread.SetApartmentState(ApartmentState.STA);
    uiThread.Start(); uiThread.Join();
    return;
}
if(args.Length > 0 && args[0] == "--attachments")
{
    var uiThread = new Thread(() => AttachmentSmoke.Run(args[1], args[2]));
    uiThread.SetApartmentState(ApartmentState.STA);
    uiThread.Start(); uiThread.Join();
    return;
}
if(args.Length < 1) throw new ArgumentException("Usage: LocalAiSmoke <model.gguf> [report.json] [gpu-layers]");
var settings = new AiModelSettings
{
    ModelPath = Path.GetFullPath(args[0]), ContextLength = 4096, MaxTokens = 128,
    Temperature = 0, GpuLayers = args.Length > 2 && args[2] != "auto" ? int.Parse(args[2]) : 20,
    MemoryMode = args.Length > 2 && args[2] != "auto" ? AiMemoryMode.Manual : AiMemoryMode.Automatic,
    SystemPrompt = "Отвечай по-русски. Сохраняй числа и условия исходного текста."
};
using var engine = new LocalAiInferenceService();
var report = new Dictionary<string, object?> { ["model_path"] = settings.ModelPath, ["gpu_layers"] = settings.GpuLayers };
var answer = new StringBuilder();
var watch = Stopwatch.StartNew();
double? firstText = null;
report["gpu_before"] = ReadGpuMemory();
report["working_set_before"] = Environment.WorkingSet;
await engine.GenerateAsync(settings,
    [new("user", "Сократи до одного предложения: Бюджет проекта — 120 000 рублей. Проверка занимает три рабочих дня. Публикацию отложат, если найдут ошибки шифрования.")],
    fragment => { firstText ??= watch.Elapsed.TotalSeconds; answer.Append(fragment); }, null, CancellationToken.None);
report["elapsed_seconds"] = watch.Elapsed.TotalSeconds;
report["first_text_seconds"] = firstText;
report["answer"] = answer.ToString();
report["loaded_after_generation"] = engine.IsLoaded;
report["memory_plan"] = engine.LastMemoryPlan;
report["gpu_loaded"] = ReadGpuMemory();
report["working_set_loaded"] = Environment.WorkingSet;
Console.WriteLine("ANSWER: " + answer);
if(answer.Length == 0 || !answer.ToString().Contains("120")) throw new Exception("Unexpected summary answer.");

using var stop = new CancellationTokenSource();
bool cancelled = false;
try
{
    await engine.GenerateAsync(settings, [new("user", "Напиши подробный рассказ на тысячу слов.")],
        _ => stop.Cancel(), null, stop.Token);
}
catch(OperationCanceledException) { cancelled = true; }
report["cancellation_passed"] = cancelled;
if(!cancelled) throw new Exception("Cancellation was not observed.");
Task? unloading = null;
bool unloadCancelled = false;
try
{
    await engine.GenerateAsync(settings, [new("user", "Напиши длинную статью о книгах.")],
        _ => unloading ??= engine.UnloadAsync(), null, CancellationToken.None);
}
catch(OperationCanceledException) { unloadCancelled = true; }
if(unloading is not null) await unloading;
report["unload_cancelled_generation"] = unloadCancelled;
if(!unloadCancelled) throw new Exception("Unload did not stop active generation.");
report["unloaded"] = !engine.IsLoaded;
report["gpu_unloaded"] = ReadGpuMemory();
report["working_set_unloaded"] = Environment.WorkingSet;

var reloadAnswer = new StringBuilder();
await engine.GenerateAsync(settings with { MaxTokens = 32 }, [new("user", "Ответь одним словом: готово")],
    text => reloadAnswer.Append(text), null, CancellationToken.None);
report["reload_answer"] = reloadAnswer.ToString();
report["reload_passed"] = engine.IsLoaded && reloadAnswer.Length > 0;
await engine.UnloadAsync();
report["final_unloaded"] = !engine.IsLoaded;
report["gpu_final"] = ReadGpuMemory();
string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
if(args.Length > 1) File.WriteAllText(args[1], json);
Console.WriteLine(json);

static string ReadGpuMemory()
{
    try
    {
        var info = new ProcessStartInfo("nvidia-smi") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach(string argument in new[] { "--query-gpu=memory.used,memory.free", "--format=csv,noheader,nounits" }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Trim();
    }
    catch { return "unavailable"; }
}
