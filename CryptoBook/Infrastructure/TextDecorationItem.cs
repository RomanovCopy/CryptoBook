using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CryptoBook.Infrastructure
{
    public class TextDecorationItem:ITextDecorationItem
    {
        public string Name { get; set ; }
        public TextDecorationCollection Decorations { get ; set; }
    }
}
