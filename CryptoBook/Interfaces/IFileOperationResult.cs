using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileOperationResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
