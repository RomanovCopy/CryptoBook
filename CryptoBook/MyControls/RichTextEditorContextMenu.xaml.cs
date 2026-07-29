using CryptoBook.Interfaces;

using System.Windows.Controls;

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
