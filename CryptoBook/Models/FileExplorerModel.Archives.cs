using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Services;
using CryptoBook.ViewModels;
using CryptoBook.Views;

using System.Runtime.InteropServices;
using System.Security;

namespace CryptoBook.Models;

public partial class FileExplorerModel
{
    public async Task<bool> OpenArchiveAsync(string path, CancellationToken cancellationToken = default)
    {
        if(_archives is null || _archiveOpening || _closed) return false;
        _archiveOpening = true;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);
        CancellationToken token = cancellation.Token;
        try
        {
            StorageLocation? existing = _archives.FindRoot(WindowId, path);
            if(existing is not null)
            {
                await AttachArchiveDirectoryAsync(existing.Value, token);
                return await NavigateAsync(existing.Value.ToString(), FileExplorerNavigationMode.Standard, token);
            }
            bool retry = false;
            string? password = null;
            StorageLocation root;
            while(true)
            {
                try
                {
                    root = await _progressDialogService.RunAsync(ArchivePathPolicy.Text("Open"),
                        async (progress, dialogToken) =>
                        {
                            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, dialogToken);
                            progress.Report(null, ArchivePathPolicy.Text("Reading"));
                            return await _archives.MountAsync(WindowId, path, password, linked.Token);
                        });
                    password = null;
                    break;
                }
                catch(ArchivePasswordRequiredException)
                {
                    password = null;
                    token.ThrowIfCancellationRequested();
                    Guid dialog = _windowManager.CreateWindow<ArchivePasswordWindow>(new Dictionary<string, object?>
                    {
                        ["source"] = path, ["retry"] = retry
                    });
                    _windowManager.ShowWindowDialog(dialog);
                    using SecureString? secure = _windowManager.GetResult<SecureString>(dialog);
                    if(secure is null) return false;
                    IntPtr pointer = Marshal.SecureStringToGlobalAllocUnicode(secure);
                    try { password = Marshal.PtrToStringUni(pointer); }
                    finally { Marshal.ZeroFreeGlobalAllocUnicode(pointer); }
                    retry = true;
                }
            }
            token.ThrowIfCancellationRequested();
            await AttachArchiveDirectoryAsync(root, token);
            return await NavigateAsync(root.ToString(), FileExplorerNavigationMode.Standard, token);
        }
        catch(OperationCanceledException) { return false; }
        catch(Exception exception)
        {
            if(!_closed) await _messageService.ShowMessage(ArchivePathPolicy.Text("Open"), exception.Message);
            return false;
        }
        finally
        {
            if(_closed) _archives.CloseOwner(WindowId);
            _archiveOpening = false;
        }
    }

    private async Task AttachArchiveDirectoryAsync(StorageLocation root, CancellationToken token)
    {
        if(ArchiveDirectories.Any(directory => _storage.AreEquivalent(directory.Location, root))) return;
        var metadata = await _storage.GetMetadataAsync(root, token) ?? throw new InvalidOperationException();
        StorageLocation parentLocation = _storage.GetParent(root) ?? throw new InvalidOperationException();
        var parent = await ResolveDirectoryContainerAsync(parentLocation.ToString(), token);
        var directory = _systemItemCreateService.CreateDirectory(metadata with { Kind = StorageItemKind.Container }, parent);
        parent.IsExpanded = true;
        ArchiveDirectories = [.. ArchiveDirectories, directory];
    }
}
