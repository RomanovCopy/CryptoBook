using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface ITextDecorationItem
    {
        public string Name { get; set; }
        public System.Windows.TextDecorationCollection Decorations { get; set; }
    }
}
