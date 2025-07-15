using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class MyRichtextboxViewModel: ViewModelBase, IRichtextboxViewModel
    {
        private readonly MyRichtextboxModel myRichtextboxModel;
        public FlowDocument Document { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool IsBold { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool IsItalic { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool IsUnderlined { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double FontSize { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }


        public MyRichtextboxViewModel(ILifetimeScope scope)
        {
            myRichtextboxModel = new(scope);
        }


        public ICommand BoldCommand => throw new NotImplementedException();

        public ICommand ItalicCommand => throw new NotImplementedException();

        public ICommand UnderlineCommand => throw new NotImplementedException();

        public ICommand ClearFormattingCommand => throw new NotImplementedException();

        public ICommand InsertTextCommand => throw new NotImplementedException();

        public ICommand ClearTextCommand => throw new NotImplementedException();

        public ICommand InsertImageCommand => throw new NotImplementedException();

        public ICommand CopyCommand => throw new NotImplementedException();

        public ICommand CutCommand => throw new NotImplementedException();

        public ICommand PasteCommand => throw new NotImplementedException();

        public ICommand ChangeFontSizeCommand => throw new NotImplementedException();

        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Close => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();

        public ICommand Closed => throw new NotImplementedException();
    }
}
