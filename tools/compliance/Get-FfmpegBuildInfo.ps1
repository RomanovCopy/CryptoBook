[CmdletBinding()]
param(
    [string] $NativeDirectory = (Join-Path `
        ([Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)) `
        '.nuget\packages\sdcb.ffmpeg.runtime.windows-x64\7.1.0\runtimes\win-x64\native')
)

$ErrorActionPreference = 'Stop'
$NativeDirectory = [IO.Path]::GetFullPath($NativeDirectory)
if (-not (Test-Path -LiteralPath $NativeDirectory -PathType Container)) {
    throw "FFmpeg native directory was not found: $NativeDirectory"
}

if (-not ('CryptoBook.Compliance.NativeExportReader' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace CryptoBook.Compliance
{
    public static class NativeExportReader
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr StringFunction();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint VersionFunction();

        public static string ReadString(IntPtr library, string exportName)
        {
            IntPtr export = NativeLibrary.GetExport(library, exportName);
            StringFunction function = Marshal.GetDelegateForFunctionPointer<StringFunction>(export);
            return Marshal.PtrToStringUTF8(function()) ?? String.Empty;
        }

        public static uint ReadVersion(IntPtr library, string exportName)
        {
            IntPtr export = NativeLibrary.GetExport(library, exportName);
            VersionFunction function = Marshal.GetDelegateForFunctionPointer<VersionFunction>(export);
            return function();
        }
    }
}
'@
}

$libraries = @(
    @{ File = 'avcodec-61.dll'; Prefix = 'avcodec' },
    @{ File = 'avdevice-61.dll'; Prefix = 'avdevice' },
    @{ File = 'avfilter-10.dll'; Prefix = 'avfilter' },
    @{ File = 'avformat-61.dll'; Prefix = 'avformat' },
    @{ File = 'avutil-59.dll'; Prefix = 'avutil' },
    @{ File = 'postproc-58.dll'; Prefix = 'postproc' },
    @{ File = 'swresample-5.dll'; Prefix = 'swresample' },
    @{ File = 'swscale-8.dll'; Prefix = 'swscale' }
)

$originalPath = $env:PATH
$env:PATH = "$NativeDirectory;$originalPath"
try {
    foreach ($definition in $libraries) {
        $path = Join-Path $NativeDirectory $definition.File
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Expected FFmpeg library was not found: $path"
        }

        $library = [Runtime.InteropServices.NativeLibrary]::Load($path)
        try {
            $prefix = $definition.Prefix
            $version = [CryptoBook.Compliance.NativeExportReader]::ReadVersion(
                $library,
                "${prefix}_version")
            [pscustomobject]@{
                File = $definition.File
                VersionNumber = $version
                Version = '{0}.{1}.{2}' -f (
                    ($version -shr 16) -band 0xff),
                    (($version -shr 8) -band 0xff),
                    ($version -band 0xff)
                Configuration = [CryptoBook.Compliance.NativeExportReader]::ReadString(
                    $library,
                    "${prefix}_configuration")
                License = [CryptoBook.Compliance.NativeExportReader]::ReadString(
                    $library,
                    "${prefix}_license")
            }
        }
        finally {
            [Runtime.InteropServices.NativeLibrary]::Free($library)
        }
    }
}
finally {
    $env:PATH = $originalPath
}
