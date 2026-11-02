using Autofac;
using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.Validation;
using CryptoBook.Views;
using System.IO;
using System.Text;
using System.Windows;

internal static partial class Program
{
    private static async Task ValidateStage7Connection(IsolatedServices isolated, WpdStorageProvider provider,
        StorageItemMetadata device, StorageItemMetadata volume, WpdDeviceInfo selected)
    {
        var c = isolated.Container;
        c.Resolve<IThemeManager>().ApplyTheme(ApplicationTheme.Light);
        string folderName = "CryptoBook-Help-Stage7-connection-" + Guid.NewGuid().ToString("N")[..8];
        var folder = provider.GetChild(volume.Location, folderName);
        Check(await provider.GetMetadataAsync(folder) is null, "Connection probe folder does not exist before creation.");
        Check((await provider.CreateContainerAsync(folder)).Success, "New disposable folder created for connection probe.");
        var probe = provider.GetChild(folder, "Reconnect.txt");
        byte[] expected = Encoding.UTF8.GetBytes("Disposable connection probe Ж Є ä.\n");
        await using (var stream = await provider.OpenRawWriteAsync(probe, overwrite: false)) await stream.WriteAsync(expected);
        await isolated.PreparePortableDemoAsync(device, volume, (await provider.GetMetadataAsync(folder))!);
        c.Resolve<IFileExplorerService>().Show(folder.ToString());
        var explorer = Application.Current.Windows.OfType<FileExplorer>().Single();
        var vm = (IFileExplorerViewModel)explorer.DataContext;
        await Until(() => !vm.IsCurrentDirectoryUnavailable && vm.ChildrenView.OfType<IFileItem>().Any());
        void State(string state) => WriteJson(Path.Combine(isolated.Root, "connection-state.json"), new { state, folderName, at = DateTimeOffset.UtcNow });
        async Task WaitFor(Func<IReadOnlyList<WpdDeviceInfo>, bool> condition)
        {
            var deadline = DateTime.UtcNow.AddMinutes(8);
            while (!condition(await new WindowsPortableDeviceBridge().GetDevicesAsync()))
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Physical device action not observed within eight minutes.");
                await Task.Delay(500);
            }
        }
        State("ready-for-physical-disconnect");
        Console.WriteLine($"CONNECTION_READY_DISCONNECT root={isolated.Root}");
        await WaitFor(items => !items.Any(d => d.Id == selected.Id && d.IsAvailable));
        Check(true, "Physical MTP disconnection observed through production device inventory.");
        bool readFailed = false;
        try { await using var stream = await provider.OpenRawReadAsync(probe); _ = stream.ReadByte(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException) { readFailed = true; }
        Check(readFailed, "Opening disposable remote file fails after physical disconnection.");
        vm.RefreshCommand.Execute(vm.SelectedItem);
        await Until(() => vm.IsCurrentDirectoryUnavailable);
        Check(vm.IsCurrentDirectoryUnavailable, "Production explorer marks the disconnected disposable folder unavailable.");
        State("ready-for-physical-reconnect");
        Console.WriteLine("CONNECTION_READY_RECONNECT");
        await WaitFor(items => items.Any(d => d.DisplayName == selected.DisplayName && d.IsAvailable));
        var freshDevice = (await provider.GetRootsAsync()).Single(d => d.Name == selected.DisplayName && d.Capabilities != StorageProviderCapabilities.None);
        var freshVolume = (await provider.GetChildrenAsync(freshDevice.Location)).Single(v => v.Name == volume.Name);
        var freshFolder = provider.GetChild(freshVolume.Location, folderName);
        var freshProbe = provider.GetChild(freshFolder, "Reconnect.txt");
        await using (var stream = await provider.OpenRawReadAsync(freshProbe))
        {
            using var data = new MemoryStream(); await stream.CopyToAsync(data);
            Check(data.ToArray().SequenceEqual(expected), "Actual MTP reconnect retains exact disposable file bytes.");
        }
        if (provider.AreEquivalent(folder, freshFolder))
        {
            vm.RetryNavigationCommand.Execute(null); await Until(() => !vm.IsCurrentDirectoryUnavailable);
            Check(!vm.IsCurrentDirectoryUnavailable, "Production Retry restores the disposable folder after reconnect.");
        }
        State("complete-idle-disconnect-reconnect");
        explorer.Close();
    }

    // Read-only continuation after a disconnected WinRT call ended an earlier host.
    // The source is the exact uniquely created probe folder, never an arbitrary path.
    private static async Task ValidateStage7Reconnect(IsolatedServices isolated, WpdStorageProvider provider,
        StorageItemMetadata device, StorageItemMetadata volume, string folderName)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(folderName, @"^CryptoBook-Help-Stage7-connection-[a-f0-9]{8}$"))
            throw new ArgumentException("Invalid disposable reconnect folder name.");
        var c = isolated.Container;
        c.Resolve<IThemeManager>().ApplyTheme(ApplicationTheme.Light);
        var folder = provider.GetChild(volume.Location, folderName);
        var metadata = await provider.GetMetadataAsync(folder);
        Check(metadata is not null, "Original disposable connection folder is accessible after physical reconnect.");
        var probe = provider.GetChild(folder, "Reconnect.txt");
        byte[] expected = Encoding.UTF8.GetBytes("Disposable connection probe Ж Є ä.\n");
        await using (var stream = await provider.OpenRawReadAsync(probe))
        {
            using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes);
            Check(bytes.ToArray().SequenceEqual(expected), "Original Reconnect.txt retains exact pre-disconnection bytes after physical reconnect.");
        }
        await isolated.PreparePortableDemoAsync(device, volume, metadata!);
        c.Resolve<IFileExplorerService>().Show(folder.ToString());
        var explorer = Application.Current.Windows.OfType<FileExplorer>().Single();
        var vm = (IFileExplorerViewModel)explorer.DataContext;
        await Until(() => !vm.IsCurrentDirectoryUnavailable && vm.ChildrenView.OfType<IFileItem>().Any());
        Check(vm.ChildrenView.OfType<IFileItem>().Any(f => f.Name == "Reconnect"), "Production explorer opens the retained disposable folder and lists its probe after physical reconnect.");
        WriteJson(Path.Combine(isolated.Root, "connection-state.json"), new { state = "resumed-read-only-reconnect-complete", folderName, priorDisconnect = "separate preserved evidence; no repeat disconnect claimed" });
        explorer.Close();
    }
}
