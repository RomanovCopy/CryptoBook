using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFlowDocumentLoadService
    {
        Task LoadAsync(IRichTextBoxService richTextBoxService, Stream source, IFileTemplate template, CancellationToken cancellationToken = default);
    }
}
