using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public sealed class ProcessLaunchOptions
    {
        public required string FileName { get; init; }         // exe/cmd/powershell/...
        public string? Arguments { get; init; }
        public string? WorkingDirectory { get; init; }
        public bool UseShellExecute { get; init; } = false;    // false => ProcessStartInfo с редиректами возможно
        public bool CreateNoWindow { get; init; } = false;
        public ProcessWindowStyle WindowStyle { get; init; } = ProcessWindowStyle.Normal;

        // Если нужно "runas" (админ), то UseShellExecute должен быть true
        public bool RunAsAdmin { get; init; } = false;

        // Опционально: редиректы/кодировка
        public bool RedirectStdOut { get; init; } = false;
        public bool RedirectStdErr { get; init; } = false;
        public bool RedirectStdIn { get; init; } = false;
    }
}
