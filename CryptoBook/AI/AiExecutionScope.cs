using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CryptoBook.AI;

/// <summary>Temporary QoS for this worker only. Normal scheduling priority and system power plan stay intact.</summary>
internal sealed class AiExecutionScope : IDisposable
{
    private readonly Process process = Process.GetCurrentProcess();
    private PowerThrottling previous = new() { Version = 1 };
    private readonly bool changed;
    internal AiExecutionScope(bool enabled, bool powerSaving = false)
    {
        if(!enabled && !powerSaving) return;
        // Older Windows versions may not support querying the policy. Restore OS heuristics there.
        if(!GetProcessInformation(process.Handle, 4, ref previous, Marshal.SizeOf<PowerThrottling>()))
            previous = new() { Version = 1 };
        var active = new PowerThrottling { Version = 1, ControlMask = 1, StateMask = powerSaving ? 1u : 0u };
        changed = SetProcessInformation(process.Handle, 4, ref active, Marshal.SizeOf<PowerThrottling>());
    }
    public void Dispose()
    {
        if(changed) SetProcessInformation(process.Handle, 4, ref previous, Marshal.SizeOf<PowerThrottling>());
        process.Dispose();
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct PowerThrottling { public uint Version, ControlMask, StateMask; }
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessInformation(IntPtr process, int informationClass, ref PowerThrottling information, int size);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessInformation(IntPtr process, int informationClass, ref PowerThrottling information, int size);
}
