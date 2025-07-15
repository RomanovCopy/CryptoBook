using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IRichtextboxViewModel:IViewModel
    {
        // Свойство для хранения содержимого RichTextBox (FlowDocument)
        FlowDocument Document { get; set; }

        // Свойства для текущего состояния (например, для привязки кнопок)
        bool IsBold { get; set; }
        bool IsItalic { get; set; }
        bool IsUnderlined { get; set; }
        double FontSize { get; set; }


        // Команды для редактирования текста
        ICommand BoldCommand { get; }
        ICommand ItalicCommand { get; }
        ICommand UnderlineCommand { get; }
        ICommand ClearFormattingCommand { get; }

        // Команды для управления содержимым
        ICommand InsertTextCommand { get; }
        ICommand ClearTextCommand { get; }
        ICommand InsertImageCommand { get; }

        // Команды для работы с выделением
        ICommand CopyCommand { get; }
        ICommand CutCommand { get; }
        ICommand PasteCommand { get; }

        // Команда для изменения размера ш font
        ICommand ChangeFontSizeCommand { get; }

    }
}
}
