using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.Security.Cryptography;
using System.Text;

internal static class ArchiveIntegration
{
    internal static async Task RunAsync(WpdStorageProvider portable, StorageLocation remoteFolder, string localRoot)
    {
        var service = new ArchiveService();
        var archives = new ArchiveStorageProvider(service, new UnusedItems());
        var storage = new StorageFacade([new LocalStorageProvider(), portable, archives]);
        var transfer = new TransferEngine(storage);
        Guid owner = Guid.NewGuid();
        List<string> snapshots = [];
        byte[] payload = Encoding.UTF8.GetBytes("CryptoBook: архив ↔ Android, проверка текста 🔐\r\n");
        string content = Directory.CreateDirectory(Path.Combine(localRoot, "Архивные данные")).FullName;
        string text = Path.Combine(content, "Документ.txt");
        await File.WriteAllBytesAsync(text, payload);
        Directory.CreateDirectory(Path.Combine(content, "Пустая папка"));
        string nested = Path.Combine(content, "Вложенный.zip");
        await service.CreateAsync([text], nested, ArchiveFormat.Zip);
        string localArchive = Path.Combine(localRoot, "Архив.7z");
        const string password = "Archive test пароль 🔐";
        await service.CreateAsync([content], localArchive, ArchiveFormat.SevenZip, password);
        var remoteArchive = portable.GetChild(remoteFolder, "Архив.7z");
        Ensure(await transfer.CopyAsync(new("local", localArchive), remoteArchive), "Upload encrypted archive");
        byte[] originalHash = await HashAsync(portable, remoteArchive);
        try
        {
            var root = await archives.MountSourceAsync(owner, remoteArchive, storage, password, default);
            snapshots.Add(archives.SourcePath(root));
            if(archives.CanEdit(root)) throw new IOException("MTP archive incorrectly permits editing.");
            var folder = (await archives.GetChildrenAsync(root)).Single();
            var children = await archives.GetChildrenAsync(folder.Location);
            var document = children.Single(item => item.Name == "Документ.txt");
            await VerifyAsync(archives, document.Location, payload);
            if((await archives.DeleteAsync(document.Location)).Success)
                throw new IOException("MTP archive deletion was not blocked.");
            Console.WriteLine("MTP archive snapshot, password, read-only entries: OK");

            var nestedEntry = children.Single(item => item.Name == "Вложенный.zip");
            var nestedRoot = await archives.MountSourceAsync(owner, nestedEntry.Location, storage, null, default);
            snapshots.Add(archives.SourcePath(nestedRoot));
            await VerifyAsync(archives, (await archives.GetChildrenAsync(nestedRoot)).Single().Location, payload);
            Console.WriteLine("Nested archive inside MTP archive: OK");

            var remoteDocument = portable.GetChild(remoteFolder, "Из архива.txt");
            Ensure(await transfer.CopyAsync(document.Location, remoteDocument), "Archive entry -> MTP");
            await VerifyAsync(portable, remoteDocument, payload);
            var remoteTree = portable.GetChild(remoteFolder, "Из архива папка");
            Ensure(await transfer.CopyAsync(folder.Location, remoteTree), "Archive folder -> MTP");
            await VerifyAsync(portable, portable.GetChild(remoteTree, "Документ.txt"), payload);
            if(await portable.GetMetadataAsync(portable.GetChild(remoteTree, "Пустая папка")) is not { IsContainer: true })
                throw new IOException("Empty archive folder was not transferred to MTP.");

            var localRootLocation = await archives.MountAsync(owner, localArchive, password, default);
            var imported = archives.GetChild(localRootLocation, "С устройства.txt");
            Ensure(await transfer.CopyAsync(remoteDocument, imported), "MTP file -> local encrypted archive");
            await VerifyAsync(archives, imported, payload);
            byte[] remoteHash = await HashAsync(portable, remoteArchive);
            if(!originalHash.AsSpan().SequenceEqual(remoteHash))
                throw new IOException("The source archive on MTP was changed.");
            Console.WriteLine("MTP source archive SHA-256 unchanged: OK");
        }
        finally { archives.CloseOwner(owner); }
        if(snapshots.Any(File.Exists)) throw new IOException("Archive snapshot cleanup failed.");
        Console.WriteLine("Archive snapshot cleanup: OK");
    }

    private static async Task VerifyAsync(IStorageProvider provider, StorageLocation file, byte[] expected)
    {
        await using var input = await provider.OpenRawReadAsync(file);
        using var memory = new MemoryStream();
        await input.CopyToAsync(memory);
        if(!expected.AsSpan().SequenceEqual(memory.ToArray())) throw new IOException("Archive/MTP content mismatch.");
    }
    private static async Task<byte[]> HashAsync(IStorageProvider provider, StorageLocation file)
    {
        await using var input = await provider.OpenRawReadAsync(file);
        return await SHA256.HashDataAsync(input);
    }
    private static void Ensure(FileOperationResult result, string operation)
    {
        if(!result.Success) throw new IOException(operation + ": " + result.ErrorMessage);
        Console.WriteLine(operation + ": OK");
    }
    private sealed class UnusedItems: ISystemItemCreateService
    {
        public IDriveItem CreateRoot(string path) => throw new NotSupportedException();
        public IDirectoryItem CreateDirectory(string path, ISystemItem? parent) => throw new NotSupportedException();
        public IFileItem CreateFile(string path, ISystemItem? parent) => throw new NotSupportedException();
    }
}
