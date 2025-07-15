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
    public class RichtextboxViewModel: ViewModelBase, IRichtextboxViewModel
    {
        private readonly RichtextboxModel richtextboxModel;
        public FlowDocument Document { get => richtextboxModel.Document; set => richtextboxModel.Document=value; }
        public bool IsBold { get => richtextboxModel.IsBold; set => richtextboxModel.IsBold=value; }
        public bool IsItalic { get => richtextboxModel.IsItalic; set => richtextboxModel.IsItalic=value; }
        public bool IsUnderlined { get => richtextboxModel.IsUnderlined; set => richtextboxModel.IsUnderlined=value; }
        public double FontSize { get => richtextboxModel.FontSize; set => richtextboxModel.FontSize=value; }

        /// <summary>
        /// Конструктор ViewModel.
        /// </summary>
        /// <param name="scope">Область жизни для внедрения зависимостей.</param>
        public RichtextboxViewModel(ILifetimeScope scope)
        {
            richtextboxModel = new(scope);
            richtextboxModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }


        public ICommand BoldCommand => boldCommand ??= new RelayCommand(richtextboxModel.Execute_BoldCommand, richtextboxModel.CanExecute_BoldCommand);
        RelayCommand boldCommand;

        public ICommand ItalicCommand => italicCommand??=new RelayCommand(richtextboxModel.Execute_ItalicCommand, richtextboxModel.CanExecute_ItalicCommand);
        RelayCommand italicCommand;

        public ICommand UnderlineCommand => underlineCommand??=new RelayCommand(richtextboxModel.Execute_UnderlineCommand, richtextboxModel.CanExecute_UnderlineCommand);
        RelayCommand underlineCommand;

        public ICommand ClearFormattingCommand => clearFormattingCommand??=new RelayCommand(richtextboxModel.Execute_ClearFormattingCommand, richtextboxModel.CanExecute_ClearFormattingCommand);
        RelayCommand clearFormattingCommand;

        public ICommand InsertTextCommand => insertTextCommand??=new RelayCommand(richtextboxModel.Execute_InsertTextCommand, richtextboxModel.CanExecute_InsertTextCommand);
        RelayCommand insertTextCommand;

        public ICommand ClearTextCommand => clearTextCommand??=new RelayCommand(richtextboxModel.Execute_ClearTextCommand, richtextboxModel.CanExecute_ClearTextCommand);
        RelayCommand clearTextCommand;

        public ICommand InsertImageCommand => insertImageCommand ??= new RelayCommand(richtextboxModel.Execute_InsertImageCommand, richtextboxModel.CanExecute_InsertImageCommand);
        RelayCommand insertImageCommand;

        public ICommand CopyCommand => copyCommand ??= new RelayCommand(richtextboxModel.Execute_CopyCommand, richtextboxModel.CanExecute_CopyCommand);
        RelayCommand copyCommand;

        public ICommand CutCommand => cutCommand ??= new RelayCommand(richtextboxModel.Execute_CutCommand, richtextboxModel.CanExecute_CutCommand);
        RelayCommand cutCommand;

        public ICommand PasteCommand => pasteCommand??=new RelayCommand(richtextboxModel.Execute_PasteCommand, richtextboxModel.CanExecute_PasteCommand);
        RelayCommand pasteCommand;


        public ICommand ChangeFontSizeCommand => changeFontSizeCommand??=new RelayCommand(richtextboxModel.Execute_ChangeFontSizeCommand, richtextboxModel.CanExecute_ChangeFontSizeCommand);
        RelayCommand changeFontSizeCommand;


        public ICommand Loaded => loaded ??= new RelayCommand(richtextboxModel.Execute_Loaded, richtextboxModel.CanExecute_Loaded);
        RelayCommand loaded;


        public ICommand Close => close ??= new RelayCommand(richtextboxModel.Execute_Close, richtextboxModel.CanExecute_Close);
        RelayCommand close;


        public ICommand Closing => closing ??= new RelayCommand(richtextboxModel.Execute_Closing, richtextboxModel.CanExecute_Closing);
        RelayCommand closing;


        public ICommand Closed => closed ??= new RelayCommand(richtextboxModel.Execute_Closed, richtextboxModel.CanExecute_Closed);
        RelayCommand closed;
    }
}
