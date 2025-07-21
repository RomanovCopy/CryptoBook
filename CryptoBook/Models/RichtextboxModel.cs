using Autofac;

using CryptoBook.Infrastructure;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Models
{
    internal class RichtextboxModel: ViewModelBase
    {
        private readonly ILifetimeScope scope;
        internal FlowDocument Document { get=>document; set=>SetProperty(ref document, value); }
        FlowDocument document;
        internal bool IsBold { get=>isBold; set=>SetProperty(ref isBold,value); }
        bool isBold;
        internal bool IsItalic { get=> isItalic; set=>SetProperty(ref isItalic, value); }
        bool isItalic;
        internal bool IsUnderlined { get=> isUnderlined; set=>SetProperty(ref isUnderlined, value); }
        bool isUnderlined;
        internal double FontSize { get=> fontSize; set=>SetProperty(ref fontSize, value); }
        double fontSize;

        internal RichtextboxModel(ILifetimeScope _scope)
        {
            scope = _scope;
            //Document = new FlowDocument();
        }


        internal bool CanExecute_BoldCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_BoldCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_ItalicCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_ItalicCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_UnderlineCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_UnderlineCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_ClearFormattingCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_ClearFormattingCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_InsertTextCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_InsertTextCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_ClearTextCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_ClearTextCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_InsertImageCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_InsertImageCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_CopyCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_CopyCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_CutCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_CutCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_PasteCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_PasteCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_ChangeFontSizeCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_ChangeFontSizeCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_ChangeFontFamilyCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_ChangeFontFamilyCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_ChangeForegroundColor(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_ChangeForegroundColor(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_ChangeBackgroundColor(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_ChangeBackgroundColor(object? obj)
        {
            throw new NotImplementedException();
        }



        internal bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        internal void Execute_Loaded(object? obj)
        {

        }


        internal bool CanExecute_Close(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Close(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }

    }
}
