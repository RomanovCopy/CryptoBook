using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public class FileOperationResult
    {
        public bool Success { get => succes; init => succes=value; }
        bool succes;
        public string? ErrorMessage { get => errorMessage; init => errorMessage=value; }
        string? errorMessage;
        public string? AffectedPath { get; init; }

        public static FileOperationResult Ok(string? affectedPath = null) =>
            new() { Success = true, AffectedPath = affectedPath };
        public static FileOperationResult Fail(string message) => new() { Success = false, ErrorMessage = message };

    }
}
