using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileTemplateRegistry
    {
        IReadOnlyList<IFileTemplate> GetAll();
        IFileTemplate? GetById(string id);
    }
}
