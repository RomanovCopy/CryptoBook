using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public sealed class ShellLaunchOptions
    {
        public required string Target { get; init; }           // file/folder/url
        public string? Verb { get; init; }                     // open/edit/print/runas/...
        public string? Arguments { get; init; }                // для некоторых verb/target
        public string? WorkingDirectory { get; init; }
        public bool UseShellExecute { get; init; } = true;     // должно быть true для shell verbs
        public ProcessWindowStyle WindowStyle { get; init; } = ProcessWindowStyle.Normal;
        public bool ErrorDialog { get; init; } = false;
    }
}
