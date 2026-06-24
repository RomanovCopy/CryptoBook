using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileActionHandler
    {
        FileActionKind ActionKind { get; }

        string DisplayName { get; }

        int Order { get; }

        bool CanHandle( string filePath, IFileTemplate template);

        Task ExecuteAsync( string filePath, IFileTemplate template, CancellationToken cancellationToken = default);
    }
}
