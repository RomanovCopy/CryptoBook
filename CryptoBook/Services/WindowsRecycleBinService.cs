using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.IO;
using System.Runtime.InteropServices;

namespace CryptoBook.Services;

/// <summary>Recycles original paths through the Windows Shell on a dedicated STA.</summary>
public sealed class WindowsRecycleBinService : IRecycleBinService
{
    internal const uint OperationFlags = 0x00080000 | 0x00100000 | 0x00000400 | 0x00000010 | 0x00000004;
    internal const uint RecycleTransferFlag = 0x80;
    internal const int Cancelled = unchecked((int)0x800704C7);
    internal const int NotSupported = unchecked((int)0x80070032);

    public Task<FileOperationResult> RecycleAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var completion = new TaskCompletionSource<FileOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(Recycle(Path.GetFullPath(path), cancellationToken)); }
            catch(OperationCanceledException) { completion.SetCanceled(cancellationToken); }
            catch(Exception e) { completion.SetResult(Failure(e.Message)); }
        }) { IsBackground = true, Name = "CryptoBook Recycle Bin" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static FileOperationResult Recycle(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        IFileOperation? operation = null;
        IShellItem? item = null;
        var sink = new RecyclingProgressSink(token);
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("3ad05575-8857-4850-9277-11b85bdb8e09"), throwOnError: true)!;
            operation = (IFileOperation)Activator.CreateInstance(type)!;
            operation.SetOperationFlags(OperationFlags);
            Guid itemId = typeof(IShellItem).GUID;
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref itemId, out item);
            operation.DeleteItem(item, sink);
            int performed = operation.PerformOperations();
            operation.GetAnyOperationsAborted(out bool aborted);
            // Cancellation may arrive after the item has already entered the bin.
            // Report that item as completed so the batch counts remain accurate.
            if(sink.Recycled) return FileOperationResult.Ok(sink.RecycledPath);
            if(token.IsCancellationRequested) throw new OperationCanceledException(token);
            if(sink.RefusedPermanentDeletion) return Failure();
            int error = sink.Error < 0 ? sink.Error : performed;
            if(error < 0) Marshal.ThrowExceptionForHR(error);
            return Failure(aborted ? LocalizationManager.GetString("Explorer.DeleteCanceledTitle") : null);
        }
        finally
        {
            if(item is not null) Marshal.FinalReleaseComObject(item);
            if(operation is not null) Marshal.FinalReleaseComObject(operation);
            GC.KeepAlive(sink);
        }
    }

    private static FileOperationResult Failure(string? detail = null) => FileOperationResult.Fail(
        LocalizationManager.GetString("Explorer.RecycleBinFailed") +
        (string.IsNullOrWhiteSpace(detail) ? string.Empty : Environment.NewLine + detail));

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    internal sealed class RecyclingProgressSink(CancellationToken token) : IFileOperationProgressSink
    {
        public bool Recycled { get; private set; }
        public string? RecycledPath { get; private set; }
        public bool RefusedPermanentDeletion { get; private set; }
        public int Error { get; private set; }
        public int StartOperations() => token.IsCancellationRequested ? Cancelled : 0;
        public int FinishOperations(int result) => 0;
        public int PreRenameItem(uint flags, IShellItem item, string name) => 0;
        public int PostRenameItem(uint flags, IShellItem item, string name, int result, IShellItem? created) => 0;
        public int PreMoveItem(uint flags, IShellItem item, IShellItem folder, string name) => 0;
        public int PostMoveItem(uint flags, IShellItem item, IShellItem folder, string name, int result, IShellItem? created) => 0;
        public int PreCopyItem(uint flags, IShellItem item, IShellItem folder, string name) => 0;
        public int PostCopyItem(uint flags, IShellItem item, IShellItem folder, string name, int result, IShellItem? created) => 0;
        public int PreDeleteItem(uint flags, IShellItem item)
        {
            if(token.IsCancellationRequested) return Cancelled;
            if((flags & RecycleTransferFlag) != 0) return 0;
            RefusedPermanentDeletion = true;
            return NotSupported;
        }
        public int PostDeleteItem(uint flags, IShellItem item, int result, IShellItem? created)
        {
            if(result < 0) Error = result;
            Recycled |= result >= 0 && created is not null;
            if(result >= 0 && created is not null)
            {
                created.GetDisplayName(0x80058000, out IntPtr name); // SIGDN_FILESYSPATH
                try { RecycledPath = Marshal.PtrToStringUni(name); }
                finally { Marshal.FreeCoTaskMem(name); }
            }
            return 0;
        }
        public int PreNewItem(uint flags, IShellItem folder, string name) => 0;
        public int PostNewItem(uint flags, IShellItem folder, string name, string template, uint attributes, int result, IShellItem? created) => 0;
        public int UpdateProgress(uint total, uint completed) => token.IsCancellationRequested ? Cancelled : 0;
        public int ResetTimer() => 0;
        public int PauseTimer() => 0;
        public int ResumeTimer() => 0;
    }

    [ComVisible(true), Guid("04b0f1a7-9490-44bc-96e1-4296a31252e2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFileOperationProgressSink
    {
        [PreserveSig] int StartOperations();
        [PreserveSig] int FinishOperations(int result);
        [PreserveSig] int PreRenameItem(uint flags, IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int PostRenameItem(uint flags, IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IShellItem? created);
        [PreserveSig] int PreMoveItem(uint flags, IShellItem item, IShellItem folder, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int PostMoveItem(uint flags, IShellItem item, IShellItem folder, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IShellItem? created);
        [PreserveSig] int PreCopyItem(uint flags, IShellItem item, IShellItem folder, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int PostCopyItem(uint flags, IShellItem item, IShellItem folder, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IShellItem? created);
        [PreserveSig] int PreDeleteItem(uint flags, IShellItem item);
        [PreserveSig] int PostDeleteItem(uint flags, IShellItem item, int result, IShellItem? created);
        [PreserveSig] int PreNewItem(uint flags, IShellItem folder, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int PostNewItem(uint flags, IShellItem folder, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string template, uint attributes, int result, IShellItem? created);
        [PreserveSig] int UpdateProgress(uint total, uint completed);
        [PreserveSig] int ResetTimer();
        [PreserveSig] int PauseTimer();
        [PreserveSig] int ResumeTimer();
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        void BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint kind, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [ComImport, Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        void Advise(IFileOperationProgressSink sink, out uint cookie);
        void Unadvise(uint cookie);
        void SetOperationFlags(uint flags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        void SetProgressDialog(IntPtr dialog);
        void SetProperties(IntPtr properties);
        void SetOwnerWindow(IntPtr owner);
        void ApplyPropertiesToItem(IShellItem item);
        void ApplyPropertiesToItems(IntPtr items);
        void RenameItem(IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string name, IFileOperationProgressSink? sink);
        void RenameItems(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string name);
        void MoveItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name, IFileOperationProgressSink? sink);
        void MoveItems(IntPtr items, IShellItem destination);
        void CopyItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name, IFileOperationProgressSink? sink);
        void CopyItems(IntPtr items, IShellItem destination);
        void DeleteItem(IShellItem item, IFileOperationProgressSink sink);
        void DeleteItems(IntPtr items);
        void NewItem(IShellItem destination, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string template, IFileOperationProgressSink? sink);
        [PreserveSig] int PerformOperations();
        void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, out IShellItem item);
}
