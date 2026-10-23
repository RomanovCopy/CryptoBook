using CryptoBook.AI;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace CryptoBook.Tests;

public sealed class AiWorkerSafetyTests
{
    [Fact]
    public async Task ProtectedInspection_AcceptsResourceLimitsWithoutEnablingCpuBoost()
    {
        using var worker = new AiWorkerProcess(() => new(16L << 30, 12L << 30, 20L << 30, true));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var error = await Assert.ThrowsAsync<AiException>(() => worker.ExecuteAsync(new("inspect", new(),
            new(4096, 0, CpuBackend: true), [], ProtectResources: true), _ => { }, null, timeout.Token));
        // An empty model path is rejected by the child only after the parent's limits were accepted.
        Assert.Equal("AI.ModelMissing", error.ResourceKey);
    }
    [Fact]
    public async Task WindowsAcceptsMemoryAndCpuLimits_ForBoostedWorker()
    {
        string path = Path.Combine(Path.GetTempPath(), "CryptoBook-Safety-" + Guid.NewGuid().ToString("N") + ".gguf");
        File.WriteAllBytes(path, "GGUF"u8.ToArray());
        try
        {
            using var worker = new AiWorkerProcess(() => new(16L << 30, 12L << 30, 20L << 30, true));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var error = await Assert.ThrowsAsync<AiException>(() => worker.ExecuteAsync(new("probe",
                new() { ModelPath = path, AllowPerformanceBoost = true, BoostOnlyOnAc = false },
                new(4096, 0, CpuBackend: true, Threads: 2, BatchThreads: 2, PerformanceBoost: true), []),
                _ => { }, null, timeout.Token));
            // The deliberately incomplete model fails only after Windows accepted both resource limits.
            Assert.Equal("AI.LoadFailed", error.ResourceKey);
            Assert.True(worker.IsAlive);
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public async Task LowMemory_StopsOnlyTheOwnedWorker_AndLeavesIndependentProcessRunning()
    {
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
        foreach(string argument in new[] { "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30" })
            start.ArgumentList.Add(argument);
        using var independent = Process.Start(start)!;
        try
        {
            using var worker = new AiWorkerProcess(() => default);
            var error = await Assert.ThrowsAsync<AiException>(() => worker.ExecuteAsync(new("inspect", new(),
                new(4096, 0, CpuBackend: true), [], ProtectResources: true), _ => { }, null, CancellationToken.None));
            Assert.Equal("AI.ResourcePressure", error.ResourceKey);
            Assert.NotNull(worker.ProcessId);
            Assert.True(SpinWait.SpinUntil(() => !worker.IsAlive, TimeSpan.FromSeconds(5)));
            Assert.False(independent.HasExited);
        }
        finally { if(!independent.HasExited) { independent.Kill(); await independent.WaitForExitAsync(); } }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QosScope_RestoresPreviousProcessPolicy(bool powerSaving)
    {
        using var process = Process.GetCurrentProcess();
        var previousPriority = process.PriorityClass;
        var before = new PowerPolicy { Version = 1 };
        if(!GetProcessInformation(process.Handle, 4, ref before, Marshal.SizeOf<PowerPolicy>())) return;
        using(new AiExecutionScope(!powerSaving, powerSaving))
        {
            var active = new PowerPolicy { Version = 1 };
            Assert.True(GetProcessInformation(process.Handle, 4, ref active, Marshal.SizeOf<PowerPolicy>()));
            Assert.Equal(1u, active.ControlMask & 1);
            Assert.Equal(powerSaving ? 1u : 0u, active.StateMask & 1);
            Assert.Equal(previousPriority, process.PriorityClass);
        }
        var after = new PowerPolicy { Version = 1 };
        Assert.True(GetProcessInformation(process.Handle, 4, ref after, Marshal.SizeOf<PowerPolicy>()));
        Assert.Equal(before.ControlMask, after.ControlMask);
        Assert.Equal(before.StateMask, after.StateMask);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct PowerPolicy { public uint Version, ControlMask, StateMask; }
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessInformation(IntPtr process, int informationClass, ref PowerPolicy information, int size);
}
