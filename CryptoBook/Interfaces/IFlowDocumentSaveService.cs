using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFlowDocumentSaveService: IService
    {
        Task SaveToFileAsync( IRichTextBoxService richTextBoxService, string filePath, IFileTemplate template, 
        CancellationToken cancellationToken = default);

        Task SaveToStreamAsync( IRichTextBoxService richTextBoxService, Stream destination, IFileTemplate template, 
        CancellationToken cancellationToken = default);
    }
}
