using CryptoBook.Interfaces;

using System.Windows.Controls;
using ContextMenu = System.Windows.Controls.ContextMenu;

namespace CryptoBook.MyControls
{
    public partial class RichTextEditorContextMenu: ContextMenu, IService
    {
        public RichTextEditorContextMenu()
        {
            InitializeComponent();
        }
    }
}
