using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CryptoBook.AI;

/// <summary>One native engine, owned by this process. Cancellation destroys its entire address space.</summary>
internal sealed class AiWorkerProcess : IAiWorkerSession
{
    private Process? process;
    private SafeFileHandle? job;
    private readonly object sync = new();
    public bool IsAlive { get { lock(sync) return process is { HasExited: false }; } }
    internal int? ProcessId { get { lock(sync) return process?.Id; } }

    public async Task<AiModelInspection?> ExecuteAsync(AiWorkerRequest request, Action<string> onText,
        IProgress<float>? progress, CancellationToken cancellationToken, Action<string>? onReasoning = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Process current;
        lock(sync)
        {
            if(process is null)
            {
                string path = Environment.ProcessPath!;
                if(!string.Equals(Path.GetFileNameWithoutExtension(path), "CryptoBook", StringComparison.OrdinalIgnoreCase))
                    path = Path.Combine(AppContext.BaseDirectory, "CryptoBook.exe");
                var start = new ProcessStartInfo(path)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
                };
                start.ArgumentList.Add("--ai-worker");
                job = CreateJobObject(IntPtr.Zero, null);
                if(job.IsInvalid) throw new AiWorkerException("JobCreate");
                var limits = new JobLimits { Basic = new() { LimitFlags = 0x2000 } }; // KILL_ON_JOB_CLOSE
                if(!SetInformationJobObject(job, 9, ref limits, Marshal.SizeOf<JobLimits>()))
                { job.Dispose(); job = null; throw new AiWorkerException("JobConfigure"); }
                try
                {
                    process = Process.Start(start) ?? throw new AiWorkerException("ProcessStart");
                    if(!AssignProcessToJobObject(job, process.Handle)) throw new AiWorkerException("JobAssign");
                    // Native assertions use stderr. Drain without retaining or persisting model/request data.
                    _ = DrainAsync(process.StandardError);
                }
                catch(Exception error)
                {
                    Stop();
                    if(error is AiWorkerException) throw;
                    throw new AiWorkerException("ProcessStart");
                }
            }
            current = process;
        }
        using var cancel = cancellationToken.Register(Stop);
        try
        {
            await Task.Run(() => AiWorkerProtocol.WriteAsync(current.StandardInput.BaseStream, request, cancellationToken), cancellationToken).ConfigureAwait(false);
            AiModelInspection? inspection = null;
            while(true)
            {
                var message = await AiWorkerProtocol.ReadAsync<AiWorkerEvent>(current.StandardOutput.BaseStream, cancellationToken).ConfigureAwait(false);
                if(message is null)
                {
                    await current.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    throw new AiWorkerException($"0x{unchecked((uint)current.ExitCode):X8}");
                }
                switch(message.Kind)
                {
                    case "text": onText(message.Text ?? ""); break;
                    case "reasoning": onReasoning?.Invoke(message.Text ?? ""); break;
                    case "progress": progress?.Report(message.Progress); break;
                    case "inspection": inspection = message.Inspection; break;
                    case "done": return inspection;
                    case "error": throw new AiException(message.Text is not null && message.Text.StartsWith("AI.", StringComparison.Ordinal)
                        ? message.Text : "AI.GenerationFailed");
                    default: throw new AiWorkerException("Protocol");
                }
            }
        }
        catch(Exception error) when(error is IOException or JsonException or ObjectDisposedException or InvalidOperationException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new AiWorkerException(current.HasExited ? $"0x{unchecked((uint)current.ExitCode):X8}" : "ConnectionLost");
        }
        finally { if(cancellationToken.IsCancellationRequested) Stop(); }
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        char[] buffer = new char[2048];
        try { while(await reader.ReadAsync(buffer).ConfigureAwait(false) > 0) Array.Clear(buffer); }
        catch(Exception error) when(error is IOException or ObjectDisposedException) { }
        finally { Array.Clear(buffer); }
    }
    public void Stop()
    {
        lock(sync)
        {
            // Closing the job also kills descendants, including on abrupt application shutdown.
            job?.Dispose(); job = null;
            if(process is not null)
            {
                try { if(!process.HasExited) process.Kill(entireProcessTree: true); }
                catch(Exception error) when(error is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                // Do not Dispose a Process while ExecuteAsync is reading its pipes/exit code.
            }
        }
    }
    public void Dispose()
    {
        Stop();
        lock(sync) { process?.Dispose(); process = null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits
    {
        public long ProcessUserTime, JobUserTime;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSet, MaximumWorkingSet;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct JobLimits
    {
        public BasicLimits Basic;
        public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes;
        public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeFileHandle job, int type, ref JobLimits limits, int size);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
}
