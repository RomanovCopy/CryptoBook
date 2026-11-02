using System.IO;
using System.Runtime.InteropServices;

namespace CryptoBook.Validation;

/// <summary>Restores only the exact disposable item returned by the recycling test.</summary>
public static class RecycleBinValidation
{
    public static async Task RestoreAsync(string recycledPath, string originalPath)
    {
        if(Path.Exists(originalPath)) throw new IOException("Restore target must be vacant.");
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            object? shell = null, bin = null, items = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application", throwOnError: true)!);
                bin = ((dynamic)shell!).NameSpace(10);
                items = ((dynamic)bin!).Items();
                bool found = false;
                int count = ((dynamic)items).Count;
                for(int index = 0; index < count; index++)
                {
                    object item = ((dynamic)items).Item(index);
                    try
                    {
                        if(!string.Equals((string)((dynamic)item).Path, recycledPath, StringComparison.OrdinalIgnoreCase)) continue;
                        ((dynamic)item).InvokeVerb("undelete");
                        found = true;
                        break;
                    }
                    finally { Marshal.FinalReleaseComObject(item); }
                }
                if(!found) throw new IOException("Disposable item was not found in the Windows Recycle Bin.");
                completion.SetResult();
            }
            catch(Exception e) { completion.SetException(e); }
            finally
            {
                if(items is not null) Marshal.FinalReleaseComObject(items);
                if(bin is not null) Marshal.FinalReleaseComObject(bin);
                if(shell is not null) Marshal.FinalReleaseComObject(shell);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while(!Path.Exists(originalPath)) await Task.Delay(50, timeout.Token);
    }
}
