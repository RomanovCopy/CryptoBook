using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CryptoBook.Interfaces
{

    public enum SystemIconSize { Small, Large }

    public interface ISystemIconService:IService
    {
        ImageSource GetFolderIcon(SystemIconSize size = SystemIconSize.Small, bool open = false);
        ImageSource GetIconForPath(string path, SystemIconSize size = SystemIconSize.Small);     // если путь есть
        ImageSource GetIconForExtension(string extension, SystemIconSize size = SystemIconSize.Small); // ".txt" или "txt"
    }
}
