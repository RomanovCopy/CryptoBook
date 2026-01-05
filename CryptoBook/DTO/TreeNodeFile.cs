using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public class TreeNodeFile:FileItem
    {
        public bool IsSelected { get => isSelected; set => SetProperty(ref isSelected, value); }
        bool isSelected;

    }
}
