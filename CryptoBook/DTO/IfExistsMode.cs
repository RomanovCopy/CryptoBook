using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public enum IfExistsMode
    {
        FailIfExists,
        Overwrite,
        AutoRename // "MyFile (2).txt"
    }
}
