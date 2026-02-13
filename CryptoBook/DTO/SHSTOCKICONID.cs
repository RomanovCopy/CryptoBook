using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public enum SHSTOCKICONID: uint
    {
        SIID_DOCNOASSOC = 0,
        SIID_DOCASSOC = 1,
        SIID_APPLICATION = 2,
        SIID_FOLDER = 3,
        SIID_FOLDEROPEN = 4,

        SIID_DRIVEFIXED = 8,
        SIID_DRIVECD = 11,

        SIID_WARNING = 78,
        SIID_INFO = 79,
        SIID_ERROR = 80,

        SIID_RENAME = 83,
        SIID_DELETE = 84,
    }
}
