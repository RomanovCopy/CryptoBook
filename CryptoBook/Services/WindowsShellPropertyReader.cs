using CryptoBook.DTO;
using CryptoBook.Infrastructure;

using System.Runtime.InteropServices;

namespace CryptoBook.Services;

/// <summary>Enumerates the read-only Windows property store, including format handlers.</summary>
internal static class WindowsShellPropertyReader
{
    public static void Read(string path, List<FilePropertyEntry> rows, CancellationToken token)
    {
        int initialized = CoInitializeEx(IntPtr.Zero, 0);
        IPropertyStore? store = null;
        try
        {
            if(initialized < 0)
                Marshal.ThrowExceptionForHR(initialized);
            Guid id = typeof(IPropertyStore).GUID;
            // BESTEFFORT retains filesystem properties if a format handler fails.
            // Do not hydrate offline/cloud files just to inspect their properties.
            Marshal.ThrowExceptionForHR(SHGetPropertyStoreFromParsingName(
                path, IntPtr.Zero, 0x40, ref id, out store));
            store.GetCount(out uint count);
            for(uint index = 0; index < count; index++)
            {
                token.ThrowIfCancellationRequested();
                store.GetAt(index, out PropertyKey key);
                string canonical = GetCanonicalName(ref key);
                // PROPVARIANT is 24 bytes on x64, 16 on x86. Native allocation
                // avoids truncating vectors and other values containing pointers.
                IntPtr value = Marshal.AllocCoTaskMem(IntPtr.Size == 8 ? 24 : 16);
                for(int offset = 0; offset < (IntPtr.Size == 8 ? 24 : 16); offset++)
                    Marshal.WriteByte(value, offset, 0);
                try
                {
                    store.GetValue(ref key, value);
                    if(Marshal.ReadInt16(value) is 0 or 1) // VT_EMPTY / VT_NULL
                        continue;
                    string display;
                    int result = PSFormatForDisplayAlloc(ref key, value, 0, out IntPtr formatted);
                    try { display = result >= 0 ? Marshal.PtrToStringUni(formatted) ?? "" : ""; }
                    finally { Marshal.FreeCoTaskMem(formatted); }
                    result = PropVariantToStringAlloc(value, out IntPtr raw);
                    string rawText;
                    try { rawText = result >= 0 ? Marshal.PtrToStringUni(raw) ?? "" : ""; }
                    finally { Marshal.FreeCoTaskMem(raw); }
                    if(string.IsNullOrEmpty(display)) display = rawText;
                    if(string.IsNullOrEmpty(display))
                        display = LocalizationManager.Format("FileDetails.VariantType", Marshal.ReadInt16(value));
                    rows.Add(new FilePropertyEntry(
                        LocalizationManager.GetString("FileDetails.Metadata"),
                        GetDisplayName(ref key, canonical), display, canonical));
                    if(rawText.Length > 0 && !rawText.Equals(display, StringComparison.Ordinal))
                        rows.Add(new(LocalizationManager.GetString("FileDetails.Metadata"),
                            LocalizationManager.GetString("FileDetails.RawValue"), rawText, canonical + ".Raw"));
                }
                catch(COMException exception)
                {
                    rows.Add(new FilePropertyEntry(
                        LocalizationManager.GetString("FileDetails.Metadata"),
                        canonical, exception.Message, canonical));
                }
                finally
                {
                    PropVariantClear(value);
                    Marshal.FreeCoTaskMem(value);
                }
            }
        }
        finally
        {
            if(store is not null)
                Marshal.ReleaseComObject(store);
            if(initialized >= 0)
                CoUninitialize();
        }
    }

    private static string GetCanonicalName(ref PropertyKey key)
    {
        int result = PSGetNameFromPropertyKey(ref key, out IntPtr name);
        try
        {
            return result >= 0 ? Marshal.PtrToStringUni(name) ?? "" :
                $"{key.FormatId:B} {key.Id}";
        }
        finally { Marshal.FreeCoTaskMem(name); }
    }

    private static string GetDisplayName(ref PropertyKey key, string fallback)
    {
        IPropertyDescription? description = null;
        try
        {
            Guid id = typeof(IPropertyDescription).GUID;
            if(PSGetPropertyDescription(ref key, ref id, out description) < 0)
                return fallback;
            description.GetDisplayName(out IntPtr name);
            try { return Marshal.PtrToStringUni(name) ?? fallback; }
            finally { Marshal.FreeCoTaskMem(name); }
        }
        catch(COMException) { return fallback; }
        finally
        {
            if(description is not null)
                Marshal.ReleaseComObject(description);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public uint Id;
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),
        InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint count);
        void GetAt(uint index, out PropertyKey key);
        void GetValue(ref PropertyKey key, IntPtr value);
        void SetValue(ref PropertyKey key, IntPtr value);
        void Commit();
    }

    // Only the first four methods are called; their order matches propsys.h.
    [ComImport, Guid("6F79D558-3E96-4549-A1D1-7D75D2288814"),
        InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyDescription
    {
        void GetPropertyKey(out PropertyKey key);
        void GetCanonicalName(out IntPtr name);
        void GetPropertyType(out ushort type);
        void GetDisplayName(out IntPtr name);
    }

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr reserved, uint mode);
    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();
    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(IntPtr value);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHGetPropertyStoreFromParsingName(
        string path, IntPtr bindContext, uint flags, ref Guid id,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
    [DllImport("propsys.dll")]
    private static extern int PSGetNameFromPropertyKey(ref PropertyKey key, out IntPtr name);
    [DllImport("propsys.dll")]
    private static extern int PSGetPropertyDescription(ref PropertyKey key, ref Guid id,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyDescription description);
    [DllImport("propsys.dll")]
    private static extern int PSFormatForDisplayAlloc(
        ref PropertyKey key, IntPtr value, uint flags, out IntPtr formatted);
    [DllImport("propsys.dll")]
    private static extern int PropVariantToStringAlloc(IntPtr value, out IntPtr result);
}
