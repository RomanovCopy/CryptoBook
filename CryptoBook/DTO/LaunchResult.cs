using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public readonly record struct LaunchResult(
    bool Success,
    string Action,
    string Target,
    int? ProcessId = null,
    string? Error = null,
    Exception? Exception = null
)
    {
        public static LaunchResult Ok(string action, string target, int? pid = null)
            => new(true, action, target, pid);

        public static LaunchResult Fail(string action, string target, string error, Exception? ex = null)
            => new(false, action, target, null, error, ex);
    }
}
