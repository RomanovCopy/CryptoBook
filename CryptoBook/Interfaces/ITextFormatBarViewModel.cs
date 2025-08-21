using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface ITextFormatBarViewModel:IViewModel
    {
        public ICommand SetTextAlignment { get; }
        public ICommand SetParagraphIndent { get; }
        public ICommand SetLineHeight { get; }
        public ICommand ToggleBulletList { get; }
        public ICommand ToggleNumberedList { get; }
        public ICommand ClearListOnSelection {  get; }
        public ICommand InsertHyperlink { get; }
        public ICommand InsertImage { get; }
        public ICommand ClearAllFormatting { get; }
        public TextRange GetSelectedTextRange { get; }
        public ICommand ReplaceSelectedText { get; }
        public ICommand Undo { get; }
        public ICommand Redo { get; }
        public bool CanUndo { get; }
        public bool CanRedo { get; }
        public ICommand MoveCaretToStart { get; }
        public ICommand MoveCaretToEnd { get; }


    }
}
