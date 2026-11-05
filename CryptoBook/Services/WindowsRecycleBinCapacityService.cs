using CryptoBook.DTO;

using Microsoft.Win32;

using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace CryptoBook.Services;

internal static class WindowsRecycleBinCapacityService
{
    internal static RecycleBinDeletionPlan Plan(IReadOnlyList<string> paths, CancellationToken token)
    {
        var mounts = new Dictionary<string, (string Path, string Id)>(StringComparer.OrdinalIgnoreCase);
        var volumes = new Dictionary<string, (string Path, long Bytes)>(StringComparer.OrdinalIgnoreCase);
        foreach(string path in paths)
        {
            token.ThrowIfCancellationRequested();
            string fullPath = Path.GetFullPath(path);
            FileSystemInfo item = Directory.Exists(fullPath) ? new DirectoryInfo(fullPath) : new FileInfo(fullPath);
            item.Refresh();
            if(!item.Exists) throw new FileNotFoundException(null, fullPath);
            // Recycle/delete a link itself; do not count or traverse its target.
            bool link = (item.Attributes & FileAttributes.ReparsePoint) != 0;
            string directory = item is DirectoryInfo && !link
                ? fullPath : Path.GetDirectoryName(fullPath)!;
            if(!mounts.TryGetValue(directory, out var mount))
            {
                var volumePath = new StringBuilder(32768);
                if(!GetVolumePathName(fullPath: directory, volumePath, volumePath.Capacity))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var volumeName = new StringBuilder(64);
                string id = GetVolumeNameForVolumeMountPoint(volumePath.ToString(), volumeName, volumeName.Capacity)
                    ? volumeName.ToString() : volumePath.ToString();
                mount = (volumePath.ToString(), id);
                mounts.Add(directory, mount);
            }
            long size = link ? 0 : GetSize(item, token);
            volumes.TryGetValue(mount.Id, out var previous);
            volumes[mount.Id] = (mount.Path, checked(previous.Bytes + size));
        }

        return new(volumes.Select(volume => ReadSpace(volume.Key, volume.Value.Path, volume.Value.Bytes)).ToArray());
    }

    private static long GetSize(FileSystemInfo item, CancellationToken token)
    {
        if(item is FileInfo file) return file.Length;
        long size = 0;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true, IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        foreach(FileSystemInfo child in ((DirectoryInfo)item).EnumerateFileSystemInfos("*", options))
        {
            token.ThrowIfCancellationRequested();
            if(child is FileInfo childFile) size = checked(size + childFile.Length);
        }
        return size;
    }

    private static RecycleBinVolumeSpace ReadSpace(string volumeId, string mount, long required)
    {
        try
        {
            if(new DriveInfo(mount).DriveType == DriveType.Network)
                return new(mount, required, 0, 0);
            using RegistryKey? policy = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer");
            bool disabled = policy?.GetValue("NoRecycleFiles") is int noRecycle && noRecycle != 0;
            int start = volumeId.IndexOf('{'), end = volumeId.IndexOf('}');
            string? guid = start >= 0 && end > start ? volumeId[start..(end + 1)] : null;
            using RegistryKey? settings = guid is null ? null : Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\BitBucket\Volume\" + guid);
            disabled |= settings?.GetValue("NukeOnDelete") is int nuke && nuke != 0;
            long? capacity = GetCapacityBytes(settings?.GetValue("MaxCapacity"),
                policy?.GetValue("RecycleBinSize"), disabled, new DriveInfo(mount).TotalSize);
            if(capacity == 0) return new(mount, required, 0, 0);
            var info = new RecycleBinInfo { Size = (uint)Marshal.SizeOf<RecycleBinInfo>() };
            int result = SHQueryRecycleBin(mount, ref info);
            if(result == unchecked((int)0x8027003A) || result == unchecked((int)0x80070032) ||
               result == unchecked((int)0x80070002) || result == unchecked((int)0x80070003))
                return new(mount, required, 0, 0);
            return result >= 0 && info.Bytes >= 0
                ? new(mount, required, capacity, info.Bytes)
                : new(mount, required, null, 0);
        }
        catch(Exception exception) when(exception is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
        {
            // Unknown is not treated as empty/unlimited: no deletion can start.
            return new(mount, required, null, 0);
        }
    }

    internal static long? GetCapacityBytes(object? maxMegabytes, object? policyPercent, bool disabled, long volumeBytes)
    {
        if(disabled) return 0;
        if(policyPercent is int percent && percent is >= 0 and <= 100)
            return (long)((decimal)volumeBytes * percent / 100);
        // Windows stores this per-volume limit in MiB. Missing/default settings
        // are not guessed; an unknown quota must not authorize deletion.
        return maxMegabytes is int megabytes && megabytes >= 0 ? (long)megabytes * 1024 * 1024 : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RecycleBinInfo { public uint Size; public long Bytes; public long Items; }

    [DllImport("shell32.dll", EntryPoint = "SHQueryRecycleBinW", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string root, ref RecycleBinInfo info);
    [DllImport("kernel32.dll", EntryPoint = "GetVolumePathNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumePathName(string fullPath, StringBuilder volumePath, int length);
    [DllImport("kernel32.dll", EntryPoint = "GetVolumeNameForVolumeMountPointW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(string mount, StringBuilder volumeName, int length);
}
