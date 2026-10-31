using Autofac;
using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Help;
using CryptoBook.Security;
using CryptoBook.Services;
using CryptoBook.Validation;
using CryptoBook.ViewModels;
using CryptoBook.Views;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

internal static partial class Program
{
    private static async Task ValidateStage7Resources(IsolatedServices isolated)
    {
        var c = isolated.Container;
        c.Resolve<IThemeManager>().ApplyTheme(ApplicationTheme.Light);
        c.Resolve<IHelpWindowService>().Open("devices.android");
        var help = Application.Current.Windows.OfType<HelpWindow>().Single();
        var vm = (HelpWindowViewModel)help.DataContext;
        foreach (string id in Stage7Topics)
        {
            vm.Navigate.Execute(id); await Settle(help);
            var article = c.Resolve<HelpContentService>().Find(culture, id)!;
            Check(vm.CurrentTopicId == id && Text(vm.Article).Contains(article.Title), "Embedded localized article: " + id);
            foreach (string path in article.Images)
                Check(c.Resolve<HelpContentService>().LoadImage(culture, article, path) is not null, "Embedded localized image: " + id + "/" + path);
        }
        help.Close();
    }

    private static async Task ValidateStage7Device(IsolatedServices isolated, bool connectionOnly = false)
    {
        var c = isolated.Container;
        await isolated.PrepareDemoDriveAsync();
        var bridge = new WindowsPortableDeviceBridge();
        var devices = await bridge.GetDevicesAsync();
        string? requestedName = Environment.GetEnvironmentVariable("CRYPTOBOOK_HELP_DEVICE_NAME");
        var phones = devices.Where(d => d.IsAvailable && (requestedName is not null
            ? d.DisplayName.Equals(requestedName, StringComparison.OrdinalIgnoreCase)
            : !Regex.IsMatch(d.DisplayName, @"^[A-Za-z]:\\?$"))).ToArray();
        if (phones.Length != 1) Console.WriteLine("MTP_SELECTION " + System.Text.Json.JsonSerializer.Serialize(devices.Select(d => new { d.DisplayName, d.IsAvailable })));
        Check(phones.Length == 1, "Exactly one available non-drive MTP device selected; no ambiguous device choice.");
        var provider = new WpdStorageProvider(bridge);
        var device = (await provider.GetRootsAsync()).Single(r => r.Name == phones[0].DisplayName);
        // The device root contains storage volumes, not personal directory contents.
        var volumes = (await provider.GetChildrenAsync(device.Location)).Where(m => m.IsContainer && !m.IsReadOnly).ToArray();
        var volume = volumes.Length == 1 ? volumes[0] : volumes.Single(m =>
            m.Name.Contains("Internal", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Внутрен", StringComparison.OrdinalIgnoreCase));
        if (connectionOnly)
        {
            string? reconnectFolder = Environment.GetEnvironmentVariable("CRYPTOBOOK_HELP_RECONNECT_FOLDER");
            if (reconnectFolder is not null) await ValidateStage7Reconnect(isolated, provider, device, volume, reconnectFolder);
            else await ValidateStage7Connection(isolated, provider, device, volume, phones[0]);
            return;
        }
        string folderName = "CryptoBook-Help-Stage7-" + culture + "-" + Guid.NewGuid().ToString("N")[..8];
        var folder = provider.GetChild(volume.Location, folderName);
        Check(await provider.GetMetadataAsync(folder) is null, "Disposable remote folder does not already exist.");
        var created = await provider.CreateContainerAsync(folder);
        Check(created.Success, "A new uniquely named disposable MTP folder is created: " + created.ErrorMessage);
        await isolated.PreparePortableDemoAsync(device, volume, (await provider.GetMetadataAsync(folder))!);
        // Every mutable remote locator is created from this new folder. No parent is deleted.
        StorageLocation Child(string name) => provider.GetChild(folder, name);
        async Task<byte[]> Bytes(StorageLocation location)
        {
            if (!provider.IsDescendant(folder, location)) throw new InvalidOperationException("Remote read escaped disposable folder.");
            await using var source = await provider.OpenRawReadAsync(location);
            using var data = new MemoryStream(); await source.CopyToAsync(data); return data.ToArray();
        }
        async Task Put(StorageLocation location, byte[] data)
        {
            if (!provider.IsDescendant(folder, location)) throw new InvalidOperationException("Remote write escaped disposable folder.");
            await using var output = await provider.OpenRawWriteAsync(location, overwrite: true); await output.WriteAsync(data);
        }
        var coordinator = c.Resolve<IFileOperationCoordinator>();
        string local = Path.Combine(isolated.DemoDirectory, "Transfer.txt");
        byte[] content = Encoding.UTF8.GetBytes("Disposable MTP acceptance. Ж Є ä\n");
        File.WriteAllBytes(local, content);
        string image = Path.Combine(isolated.DemoDirectory, "Landscape.png"); MakeStage7Image(image, Colors.SteelBlue);
        var upload = await coordinator.TransferAsync([local, image], folder.ToString(), FileTransferKind.Copy);
        Check(upload.Success && upload.CompletedCount == 2, "Production coordinator copies TXT and PNG from local storage to actual MTP: " + upload.Failure?.ErrorMessage);
        Check((await Bytes(Child("Transfer.txt"))).SequenceEqual(content), "Uploaded TXT has exact original bytes.");
        Check((await Bytes(Child("Landscape.png"))).SequenceEqual(File.ReadAllBytes(image)), "Uploaded PNG has exact original bytes.");

        var main = Application.Current.Windows.OfType<MainWindow>().Single();
        c.Resolve<IThemeManager>().ApplyTheme(ApplicationTheme.Light);
        c.Resolve<IFileExplorerService>().Show(folder.ToString()); await Settle(main);
        var explorer = Application.Current.Windows.OfType<FileExplorer>().Single(); explorer.Width = 1360; explorer.Height = 860;
        var vm = (IFileExplorerViewModel)explorer.DataContext;
        await Until(() => vm.ChildrenView.OfType<IFileItem>().Count() == 2);
        var list = Visuals<ListView>(explorer).Single();
        async Task Select(string extension, FilePreviewKind expected)
        {
            var file = vm.ChildrenView.OfType<IFileItem>().Single(i => i.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase));
            list.SelectedItem = file; vm.SelectedListItem = file; vm.SelectedItemsSnapshot = [file];
            await vm.Preview.SelectAsync(file);
            await Until(() => vm.Preview.PreviewKind == expected); await Settle(explorer);
        }
        await Select(".txt", FilePreviewKind.Text);
        Shot(explorer, "S28-a", "devices.android", "actual disposable MTP folder and text preview", []);
        Check(vm.Preview.PreviewKind == FilePreviewKind.Text, "Actual remote TXT preview loads in production FileExplorer.");
        await Select(".png", FilePreviewKind.Image);
        Shot(explorer, "S28-b", "devices.limits", "actual remote PNG preview", []);
        Check(vm.Preview.PreviewKind == FilePreviewKind.Image, "Actual remote PNG preview loads in production FileExplorer.");
        string returned = Path.Combine(isolated.DemoDirectory, "Returned"); Directory.CreateDirectory(returned);
        var download = await coordinator.TransferAsync([Child("Transfer.txt").ToString()], returned, FileTransferKind.Copy);
        Check(download.Success && File.ReadAllBytes(Path.Combine(returned, "Transfer.txt")).SequenceEqual(content), "Production coordinator copies MTP to local storage with exact bytes.");

        foreach (var action in new[] { FileConflictAction.Skip, FileConflictAction.KeepBoth, FileConflictAction.Cancel, FileConflictAction.Replace })
        {
            await Put(Child("Transfer.txt"), "Old disposable destination"u8.ToArray());
            using var choose = OnVisible<FileConflictDialog>(async w =>
            {
                var conflict = (FileConflictDialogViewModel)w.DataContext;
                conflict.ApplyToAll = true; await Settle(w);
                if (action == FileConflictAction.KeepBoth) Shot(w, "S29-a", "devices.transfers", "actual MTP conflict", []);
                (action switch { FileConflictAction.Replace => conflict.ReplaceCommand, FileConflictAction.Skip => conflict.SkipCommand, FileConflictAction.KeepBoth => conflict.KeepBothCommand, _ => conflict.CancelCommand }).Execute(null);
            });
            var result = await coordinator.TransferAsync([local], folder.ToString(), FileTransferKind.Copy);
            bool replaced = (await Bytes(Child("Transfer.txt"))).SequenceEqual(content);
            Check(action == FileConflictAction.Cancel ? result.Canceled && !replaced : result.Success && replaced == (action == FileConflictAction.Replace), "Actual MTP conflict outcome: " + action);
            Check(File.ReadAllBytes(local).SequenceEqual(content), "Local copy source is preserved after MTP conflict " + action);
        }
        var movedFolder = Child("Moved"); Check((await provider.CreateContainerAsync(movedFolder)).Success, "Disposable move target is created.");
        var move = await coordinator.TransferAsync([Child("Transfer.txt").ToString()], movedFolder.ToString(), FileTransferKind.Move);
        var moved = provider.GetChild(movedFolder, "Transfer.txt");
        Check(move.Success && await provider.GetMetadataAsync(Child("Transfer.txt")) is null && (await Bytes(moved)).SequenceEqual(content), "Move within actual MTP changes location and preserves exact bytes.");
        string movedBack = Path.Combine(isolated.DemoDirectory, "MovedBack"); Directory.CreateDirectory(movedBack);
        var moveBack = await coordinator.TransferAsync([moved.ToString()], movedBack, FileTransferKind.Move);
        Check(moveBack.Success && await provider.GetMetadataAsync(moved) is null && File.ReadAllBytes(Path.Combine(movedBack, "Transfer.txt")).SequenceEqual(content), "Actual MTP-to-local move verifies content and removes only its disposable remote source.");
        bool cancelObserved = false;
        string cancelFile = Path.Combine(isolated.DemoDirectory, "Cancel-transfer.bin");
        File.WriteAllBytes(cancelFile, new byte[4 * 1024 * 1024]);
        using (var cancel = OnVisible<ProgressWindow>(w =>
        {
            w.UpdateLayout();
            ((IProgressViewModel)w.DataContext).Canceled.Execute(null); cancelObserved = true;
            Shot(w, "S29-b", "devices.transfers", "actual MTP progress cancellation at startup", []);
            return Task.CompletedTask;
        }, 1))
        {
            var canceled = await coordinator.TransferAsync([cancelFile], movedFolder.ToString(), FileTransferKind.Copy);
            Check(cancelObserved && canceled.Canceled && File.Exists(cancelFile), "Cancel in actual progress window stops MTP copy and preserves local source.");
        }
        var delete = Child("Delete-only-this.txt"); await Put(delete, content);
        Check((await provider.DeleteAsync(delete)).Success && await provider.GetMetadataAsync(delete) is null, "Actual MTP provider permanently deletes only the newly created disposable file.");
        string protectedLocal = Path.Combine(isolated.DemoDirectory, "Protected.cbook");
        char[] password = "CryptoBook-Help-Stage7!".ToCharArray();
        try { c.Resolve<IKeyProvider>().SetKey(password); } finally { Array.Clear(password); }
        await c.Resolve<ISecureFileProcessor>().EncryptFileAsync(local, protectedLocal);
        var protectedRemote = Child("Protected.cbook");
        Check((await coordinator.TransferAsync([protectedLocal], folder.ToString(), FileTransferKind.Copy)).Success, "Protected disposable file uploads to actual MTP.");
        var parent = vm.SelectedItem as IContainerSystemItem ?? throw new InvalidOperationException("Remote parent is unavailable.");
        var protectedItem = c.Resolve<ISystemItemCreateService>().CreateFile((await provider.GetMetadataAsync(protectedRemote))!, parent);
        var decrypted = await c.Resolve<IFileSecurityService>().DecryptAsync(protectedItem, protectedRemote.ToString(), EncryptionTargetMode.ReplaceSource);
        Check(decrypted.Success && decrypted.ProcessedFileCount == 1, "Production file-security service decrypts actual remote file with source replacement: " + decrypted.ErrorMessage);
        Check(await provider.GetMetadataAsync(protectedRemote) is null && (await Bytes(Child("Protected.txt"))).SequenceEqual(content), "MTP decryption leaves verified plaintext on the device and removes the disposable protected source.");
        vm.RefreshCommand.Execute(vm.SelectedItem); await Settle(explorer);
        Shot(explorer, "S29-c", "devices.limits", "actual MTP after device-side decryption", []);
        WriteJson(Path.Combine(isolated.Root, "device-acceptance.json"), new
        {
            culture, commit, folderName, checks, checkMessages,
            transport = "production WindowsPortableDeviceBridge and WpdStorageProvider",
            remoteData = "Only uniquely created disposable folder; no personal directory listing or device ID retained",
            originalSha256 = Convert.ToHexString(SHA256.HashData(content)),
            limitations = "One device; no cross-device test, physical interruption or ADB. Progress cancellation at startup only. Disposable folder deliberately retained for inspection."
        });
        explorer.Close();
    }
}
