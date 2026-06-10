using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface ITreeNodeVm
    {
        string Name { get; }
        bool IsExpanded { get; set; }
        bool IsSelected { get; set; }
    }
}
