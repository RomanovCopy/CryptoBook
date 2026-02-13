using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    [Flags]
    public enum SHGSI: uint
    {
        SHGSI_ICON = 0x000000100,
        SHGSI_LARGEICON = 0x000000000,
        SHGSI_SMALLICON = 0x000000001,
        SHGSI_SHELLICONSIZE = 0x000000004,
        SHGSI_SELECTED = 0x000001000,
        SHGSI_LINKOVERLAY = 0x000008000,
        SHGSI_OVERLAYINDEX = 0x000000040
    }
}
