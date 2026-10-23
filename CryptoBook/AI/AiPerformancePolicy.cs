using System.Runtime.InteropServices;

namespace CryptoBook.AI;

public sealed record AiPerformanceMeasurement(int InputTokens, int OutputTokens, double PromptSeconds,
    double GenerationSeconds, long PeakRamBytes = 0, long PeakGpuBytes = 0)
{
    public double PromptTokensPerSecond => InputTokens / PromptSeconds;
    public double GenerationTokensPerSecond => (OutputTokens - 1) / GenerationSeconds;
    // Compare equal amounts of work even if a model ends one trial earlier.
    internal double WorkSeconds => 128 / PromptTokensPerSecond + 64 / GenerationTokensPerSecond;
    internal bool IsValid => InputTokens > 0 && OutputTokens >= 16 && PromptSeconds > 0 && GenerationSeconds > 0 &&
        double.IsFinite(WorkSeconds) && WorkSeconds > 0 && PeakRamBytes >= 0 && PeakGpuBytes >= 0;
}

internal sealed record AiGenerationTiming(int InputTokens, int OutputTokens, double PromptSeconds, double GenerationSeconds);
public sealed record AiPerformanceResult(AiPerformanceMeasurement Baseline, AiPerformanceMeasurement Selected,
    bool BoostSelected, DateTime TestedUtc)
{
    public double Speedup => Baseline.WorkSeconds / Selected.WorkSeconds;
    internal bool IsValid => Baseline is { IsValid: true } && Selected is { IsValid: true } && double.IsFinite(Speedup);
}

public interface IAiPerformanceService
{
    AiPerformanceResult? GetLastPerformance(AiModelSettings settings);
    Task<AiPerformanceResult> TestPerformanceAsync(AiModelSettings settings, CancellationToken cancellationToken);
}

internal readonly record struct AiResourceSnapshot(long TotalRam, long AvailableRam, long AvailableCommit, bool OnAc)
{
    internal long Reserve => Math.Clamp(TotalRam / 8, 1024L * 1024 * 1024, 4L * 1024 * 1024 * 1024);
    internal bool HasHeadroom => TotalRam > 0 && AvailableRam > Reserve && AvailableCommit > Reserve;
    internal long CommitLimit => HasHeadroom ? Math.Min(AvailableRam - Reserve, AvailableCommit - Reserve) : 0;
    internal long ProcessCommitLimit(long existingCommit) => !HasHeadroom || existingCommit < 0 ? 0
        : existingCommit + Math.Min(CommitLimit, long.MaxValue - existingCommit);
    internal static AiResourceSnapshot Read()
    {
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        bool known = GlobalMemoryStatusEx(ref memory);
        bool onAc = GetSystemPowerStatus(out var power) && power.AcLineStatus == 1;
        return known ? new((long)memory.TotalPhysical, (long)memory.AvailablePhysical, (long)memory.AvailablePageFile, onAc) : new(0, 0, 0, onAc);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
            TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus
    {
        public byte AcLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public uint BatteryLifeTime, BatteryFullLifeTime;
    }
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out PowerStatus status);
}

internal static class AiPerformancePolicy
{
    internal static bool IsAllowed(AiModelSettings settings, AiResourceSnapshot resources) =>
        settings.AllowPerformanceBoost && (!settings.BoostOnlyOnAc || resources.OnAc);

    internal static IReadOnlyList<AiRuntimeOptions> Candidates(AiRuntimeOptions baseline, int processors)
    {
        int normal = Math.Max(1, processors / 2);
        int upper = Math.Max(1, processors - 1);
        return new[] { baseline with { Threads = normal, BatchThreads = normal, PerformanceBoost = false },
            baseline with { Threads = normal, BatchThreads = normal, PerformanceBoost = true },
            baseline with { Threads = Math.Min(upper, Math.Max(1, processors * 3 / 4)), BatchThreads = upper, PerformanceBoost = true },
            baseline with { Threads = upper, BatchThreads = upper, PerformanceBoost = true } }.Distinct().ToArray();
    }
}
