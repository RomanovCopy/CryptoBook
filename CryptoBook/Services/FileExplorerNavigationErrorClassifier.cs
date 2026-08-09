using CryptoBook.DTO;

using System;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Security;

namespace CryptoBook.Services
{
    public static class FileExplorerNavigationErrorClassifier
    {
        private const int ErrorAccessDenied = 5;
        private const int ErrorNotReady = 21;
        private static readonly int[] NetworkErrorCodes =
        [
            53,   // ERROR_BAD_NETPATH
            59,   // ERROR_UNEXP_NET_ERR
            64,   // ERROR_NETNAME_DELETED
            67,   // ERROR_BAD_NET_NAME
            121,  // ERROR_SEM_TIMEOUT
            1201, // ERROR_CONNECTION_UNAVAIL
            1222, // ERROR_NO_NETWORK
            1231, // ERROR_NETWORK_UNREACHABLE
            1232  // ERROR_HOST_UNREACHABLE
        ];

        public static FileExplorerNavigationErrorKind Classify(
            Exception exception,
            string? path = null)
        {
            ArgumentNullException.ThrowIfNull(exception);

            foreach(Exception current in EnumerateExceptionChain(exception))
            {
                if(current is OperationCanceledException)
                    return FileExplorerNavigationErrorKind.OperationCanceled;
                if(current is UnauthorizedAccessException or SecurityException ||
                   HasNativeError(current, ErrorAccessDenied))
                    return FileExplorerNavigationErrorKind.AccessDenied;
                if(current is DriveNotFoundException || HasNativeError(current, ErrorNotReady))
                    return FileExplorerNavigationErrorKind.DriveNotReady;
                if(current is SocketException || IsNetworkError(current))
                    return FileExplorerNavigationErrorKind.NetworkResourceUnavailable;
                if(current is DirectoryNotFoundException)
                    return string.IsNullOrWhiteSpace(path)
                        ? FileExplorerNavigationErrorKind.DirectoryNotFound
                        : ClassifyUnavailablePath(path);
            }

            return IsUncPath(path)
                ? FileExplorerNavigationErrorKind.NetworkResourceUnavailable
                : FileExplorerNavigationErrorKind.DirectoryNotFound;
        }

        public static FileExplorerNavigationErrorKind ClassifyUnavailablePath(
            string path)
        {
            if(IsUncPath(path))
                return FileExplorerNavigationErrorKind.NetworkResourceUnavailable;

            try
            {
                string? root = Path.GetPathRoot(path);
                if(!string.IsNullOrWhiteSpace(root) && !new DriveInfo(root).IsReady)
                    return FileExplorerNavigationErrorKind.DriveNotReady;
            }
            catch(Exception exception) when(
                exception is IOException or UnauthorizedAccessException)
            {
                return Classify(exception, path);
            }

            return FileExplorerNavigationErrorKind.DirectoryNotFound;
        }

        private static bool IsNetworkError(Exception exception) =>
            Array.IndexOf(NetworkErrorCodes, GetNativeErrorCode(exception)) >= 0;

        private static bool HasNativeError(Exception exception, int expected) =>
            GetNativeErrorCode(exception) == expected;

        private static int GetNativeErrorCode(Exception exception) =>
            exception is Win32Exception win32
                ? win32.NativeErrorCode
                : exception.HResult & 0xFFFF;

        private static bool IsUncPath(string? path) =>
            !string.IsNullOrWhiteSpace(path) &&
            path.StartsWith(@"\\", StringComparison.Ordinal);

        private static System.Collections.Generic.IEnumerable<Exception>
            EnumerateExceptionChain(Exception exception)
        {
            for(Exception? current = exception;
                current is not null;
                current = current.InnerException)
            {
                yield return current;
            }
        }
    }
}
