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
        public int ProcessedFileCount { get; init; }
        public int SkippedFileCount { get; init; }

        public static FileOperationResult Ok(
            string? affectedPath = null,
            int processedFileCount = 0,
            int skippedFileCount = 0) =>
            new()
            {
                Success = true,
                AffectedPath = affectedPath,
                ProcessedFileCount = processedFileCount,
                SkippedFileCount = skippedFileCount
            };
        public static FileOperationResult Fail(
            string message,
            int processedFileCount = 0,
            int skippedFileCount = 0) =>
            new()
            {
                Success = false,
                ErrorMessage = message,
                ProcessedFileCount = processedFileCount,
                SkippedFileCount = skippedFileCount
            };

    }
}
