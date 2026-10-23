using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Vortice.DXGI;

namespace CryptoBook.AI;

public sealed record AiMemoryPlan(int GpuLayers, int TotalLayers, long ModelBytes,
    long EstimatedGpuWeightBytes, long EstimatedRamWeightBytes, long AvailableGpuBytes,
    long AvailableRamBytes, string GpuName);

/// <summary>Plans layer offload; CPU weights remain memory-mapped and pageable by Windows.</summary>
public static class AiMemoryPlanner
{
    private const long MiB = 1024L * 1024, GiB = 1024 * MiB;

    public static AiMemoryPlan Create(AiModelSettings settings, IReadOnlyDictionary<string, string> metadata)
    {
        var (gpuBytes, gpuName) = ReadGpuBudget();
        metadata.TryGetValue("general.architecture", out string? architecture);
        string prefix = architecture ?? "llama";
        int layers = ReadInt(metadata, prefix + ".block_count", 80);
        int embedding = ReadInt(metadata, prefix + ".embedding_length", 4096);
        int heads = ReadInt(metadata, prefix + ".attention.head_count", 32);
        int kvHeads = ReadInt(metadata, prefix + ".attention.head_count_kv", 8);
        int keyLength = ReadInt(metadata, prefix + ".attention.key_length", Math.Max(1, embedding / heads));
        int valueLength = ReadInt(metadata, prefix + ".attention.value_length", keyLength);
        long cachePerLayer = (long)settings.ContextLength * kvHeads * (keyLength + valueLength) * 2;
        return Calculate(settings.MemoryMode, settings.GpuLayers, new FileInfo(settings.ModelPath).Length,
            layers, cachePerLayer, gpuBytes, ReadAvailableRam(), gpuName);
    }

    public static AiMemoryPlan Calculate(AiMemoryMode mode, int manualLayers, long modelBytes,
        int layers, long cacheBytesPerLayer, long gpuBudget, long ramAvailable, string gpuName = "")
    {
        layers = Math.Max(1, layers);
        int offload;
        if(mode == AiMemoryMode.CpuOnly) offload = 0;
        else if(mode == AiMemoryMode.Manual) offload = manualLayers;
        else
        {
            // Keep space for context, graph buffers, the desktop and other applications.
            double reserve = Math.Max(GiB, gpuBudget * 0.20);
            double perLayer = (double)modelBytes / (layers + 1) * 1.10 + cacheBytesPerLayer;
            offload = perLayer > 0 ? (int)Math.Clamp(Math.Floor((gpuBudget - reserve) / perLayer), 0, layers) : 0;
        }
        double fraction = offload < 0 ? 1 : Math.Clamp((double)offload / (layers + 1), 0, 1);
        long gpuWeights = (long)(modelBytes * fraction);
        return new(offload, layers, modelBytes, gpuWeights, modelBytes - gpuWeights,
            gpuBudget, ramAvailable, gpuName);
    }

    private static int ReadInt(IReadOnlyDictionary<string, string> metadata, string key, int fallback) =>
        metadata.TryGetValue(key, out string? value) && int.TryParse(value, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out int result) && result > 0 ? result : fallback;

    private static (long Bytes, string Name) ReadGpuBudget()
    {
        long largest = 0;
        string name = string.Empty;
        try
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            for(uint index = 0; factory.EnumAdapters1(index, out var adapter).Success; index++)
            {
                using(adapter)
                {
                    var description = adapter.Description1;
                    if((description.Flags & AdapterFlags.Software) != 0 || (ulong)description.DedicatedVideoMemory < (ulong)GiB) continue;
                    using var memoryAdapter = adapter.QueryInterfaceOrNull<IDXGIAdapter3>();
                    if(memoryAdapter is null) continue;
                    var memory = memoryAdapter.QueryVideoMemoryInfo(0, MemorySegmentGroup.Local);
                    long available = memory.Budget > memory.CurrentUsage ? (long)(memory.Budget - memory.CurrentUsage) : 0;
                    if(available > largest) { largest = available; name = description.Description; }
                }
            }
        }
        catch(Exception e) when(e is SharpGen.Runtime.SharpGenException or DllNotFoundException or EntryPointNotFoundException) { }
        return (largest, name);
    }

    private static long ReadAvailableRam()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        return GlobalMemoryStatusEx(ref status) ? (long)status.AvailablePhysical : 0;
    }
    // DXGI reports this process's usage and budget. Query inside the AI worker, not the UI process.
    internal static (long Used, long Available) ReadGpuMemory()
    {
        long used = 0, available = long.MaxValue;
        try
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            for(uint index = 0; factory.EnumAdapters1(index, out var adapter).Success; index++)
            {
                using(adapter)
                using(var memoryAdapter = adapter.QueryInterfaceOrNull<IDXGIAdapter3>())
                {
                    if(memoryAdapter is null || (adapter.Description1.Flags & AdapterFlags.Software) != 0) continue;
                    var memory = memoryAdapter.QueryVideoMemoryInfo(0, MemorySegmentGroup.Local);
                    if(memory.CurrentUsage == 0) continue;
                    used += (long)memory.CurrentUsage;
                    available = Math.Min(available, memory.Budget > memory.CurrentUsage ? (long)(memory.Budget - memory.CurrentUsage) : 0);
                }
            }
        }
        catch(Exception error) when(error is SharpGen.Runtime.SharpGenException or DllNotFoundException or EntryPointNotFoundException) { }
        return (used, available);
    }
    internal static ulong ReadTotalRam()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        return GlobalMemoryStatusEx(ref status) ? status.TotalPhysical : 0;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
            TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
