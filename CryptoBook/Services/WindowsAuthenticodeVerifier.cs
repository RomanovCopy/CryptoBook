using System.Runtime.InteropServices;

namespace CryptoBook.Services
{
    internal enum AuthenticodeStatus
    {
        Valid,
        NotSigned,
        Invalid
    }

    internal interface IAuthenticodeVerifier
    {
        AuthenticodeStatus Verify(string filePath);
    }

    internal sealed class WindowsAuthenticodeVerifier: IAuthenticodeVerifier
    {
        private const int ErrorSuccess = 0;
        private const int TrustENoSignature = unchecked((int)0x800B0100);
        private const uint WtdUiNone = 2;
        private const uint WtdRevokeNone = 0;
        private const uint WtdChoiceFile = 1;
        private const uint WtdStateActionIgnore = 0;
        private const uint WtdRevocationCheckChainExcludeRoot = 0x00000080;
        private static readonly Guid VerifyAction = new(
            "00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        public AuthenticodeStatus Verify(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            var fileInfo = new WinTrustFileInfo
            {
                StructureSize = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
                FilePath = filePath
            };
            IntPtr fileInfoPointer = Marshal.AllocHGlobal(
                Marshal.SizeOf<WinTrustFileInfo>());

            try
            {
                Marshal.StructureToPtr(fileInfo, fileInfoPointer, false);
                var trustData = new WinTrustData
                {
                    StructureSize = (uint)Marshal.SizeOf<WinTrustData>(),
                    UiChoice = WtdUiNone,
                    RevocationChecks = WtdRevokeNone,
                    UnionChoice = WtdChoiceFile,
                    FileInfoPointer = fileInfoPointer,
                    StateAction = WtdStateActionIgnore,
                    ProviderFlags = WtdRevocationCheckChainExcludeRoot
                };
                IntPtr trustDataPointer = Marshal.AllocHGlobal(
                    Marshal.SizeOf<WinTrustData>());

                try
                {
                    Marshal.StructureToPtr(trustData, trustDataPointer, false);
                    Guid action = VerifyAction;
                    int result = WinVerifyTrust(
                        new IntPtr(-1),
                        ref action,
                        trustDataPointer);
                    return result switch
                    {
                        ErrorSuccess => AuthenticodeStatus.Valid,
                        TrustENoSignature => AuthenticodeStatus.NotSigned,
                        _ => AuthenticodeStatus.Invalid
                    };
                }
                finally
                {
                    Marshal.DestroyStructure<WinTrustData>(trustDataPointer);
                    Marshal.FreeHGlobal(trustDataPointer);
                }
            }
            finally
            {
                Marshal.DestroyStructure<WinTrustFileInfo>(fileInfoPointer);
                Marshal.FreeHGlobal(fileInfoPointer);
            }
        }

        [DllImport(
            "wintrust.dll",
            ExactSpelling = true,
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern int WinVerifyTrust(
            IntPtr windowHandle,
            [In] ref Guid actionId,
            IntPtr trustData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustFileInfo
        {
            public uint StructureSize;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string FilePath;

            public IntPtr FileHandle;
            public IntPtr KnownSubject;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustData
        {
            public uint StructureSize;
            public IntPtr PolicyCallbackData;
            public IntPtr SipClientData;
            public uint UiChoice;
            public uint RevocationChecks;
            public uint UnionChoice;
            public IntPtr FileInfoPointer;
            public uint StateAction;
            public IntPtr StateData;
            public IntPtr UrlReference;
            public uint ProviderFlags;
            public uint UiContext;
            public IntPtr SignatureSettings;
        }
    }
}
