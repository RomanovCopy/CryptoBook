using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Collections.ObjectModel;
using System.Windows;

namespace CryptoBook.Models
{
    internal class TitleBarRB_Model: ViewModelBase
    {

        private readonly ILifetimeScope scope;
        private readonly IRichTextBoxService service;
        private readonly IRichtextboxViewModel richtextbox;

        public ObservableCollection<double> FontSizes { get=>fontSizes; internal set=>SetProperty(ref fontSizes,value); }
        public bool CanUndo { get; internal set; }
        public bool CanRedo { get; internal set; }
        public ObservableCollection<System.Windows.Media.FontFamily> FontFamilyes { get; internal set; }

        private ObservableCollection<double> fontSizes;

        public TitleBarRB_Model(ILifetimeScope _scope)
        {
            scope = _scope;
            service = scope.Resolve<IRichTextBoxService>();
            richtextbox = scope.Resolve<IRichtextboxViewModel>();
            FontSizes = new ObservableCollection<double>();
        }


        internal bool CanExecute_BoldCommand(object? obj)
        {
            return richtextbox.BoldCommand.CanExecute(null);
        }
        internal void Execute_BoldCommand(object? obj)
        {
            richtextbox.BoldCommand.Execute(obj);
        }


        internal bool CanExecute_ItalicCommand(object? obj)
        {
            return richtextbox.ItalicCommand.CanExecute(obj);
        }
        internal void Execute_ItalicCommand(object? obj)
        {
            richtextbox.ItalicCommand.Execute(obj);
        }


        internal bool CanExecute_UnderlineCommand(object? obj)
        {
            return richtextbox.UnderlineCommand.CanExecute(obj);
        }
        internal void Execute_UnderlineCommand(object? obj)
        {
            richtextbox.UnderlineCommand.Execute(obj);
        }


        internal bool CanExecute_ClearFormattingCommand(object? obj)
        {
            return richtextbox.ClearFormattingCommand.CanExecute(obj);
        }
        internal void Execute_ClearFormattingCommand(object? obj)
        {
            richtextbox.ClearFormattingCommand.Execute(obj);
        }


        internal bool CanExecute_InsertImageCommand(object? obj)
        {
            return richtextbox.InsertImageCommand.CanExecute(obj);
        }
        internal void Execute_InsertImageCommand(object? obj)
        {
            richtextbox.InsertImageCommand.Execute(obj);
        }


        internal bool CanExecute_ApplyFontSize(object? obj)
        {
            return richtextbox.ChangeFontSizeCommand.CanExecute(obj);
        }
        internal void Execute_ApplyFontSize(object? obj)
        {
            richtextbox.ChangeFontSizeCommand.Execute(obj);
        }


        internal bool CanExecute_ApplyFontFamily(object? obj)
        {
            return richtextbox.ChangeFontFamilyCommand.CanExecute(obj);
        }
        internal void Execute_ApplyFontFamily(object? obj)
        {
            richtextbox.ChangeFontFamilyCommand.Execute(obj);
        }

        internal bool CanExecute_ApplyForegroundColor(object? obj)
        {
            return richtextbox.ChangeForegroundColor.CanExecute(obj);
        }
        internal void Execute_ApplyForegroundColor(object? obj)
        {
            richtextbox.ChangeForegroundColor.Execute(obj);
        }


        internal bool CanExecute_ApplyBackgroundColor(object? obj)
        {
            return richtextbox.ChangeBackgroundColor.CanExecute(obj);
        }
        internal void Execute_ApplyBackgroundColor(object? obj)
        {
            richtextbox.ChangeBackgroundColor.Execute(obj);
        }


        internal bool CanExecute_ApplyTextAlignment(object? obj)
        {
            return richtextbox.ApplyTextAlignment.CanExecute(obj);
        }
        internal void Execute_ApplyTextAlignment(object? obj)
        {
            richtextbox.ApplyTextAlignment.Execute(obj);
        }


        internal bool CanExecute_ApplyTextFormattingMode(object? obj)
        {
            return richtextbox.ApplyTextFormattingMode.CanExecute(obj);
        }
        internal void Execute_ApplyTextFormattingMode(object? obj)
        {
            richtextbox.ApplyTextFormattingMode.Execute(obj);
        }


        internal bool CanExecute_ApplyTextRenderingMode(object? obj)
        {
            return richtextbox.ApplyTextRenderingMode.CanExecute (obj);
        }
        internal void Execute_ApplyTextRenderingMode(object? obj)
        {
            richtextbox.ApplyTextRenderingMode.Execute(obj);
        }


        internal bool CanExecute_ApplyAcceptsReturn(object? obj)
        {
            return richtextbox.ApplyAcceptsReturn.CanExecute(obj);
        }
        internal void Execute_ApplyAcceptsReturn(object? obj)
        {
            richtextbox.ApplyAcceptsReturn.Execute(obj);
        }


        internal bool CanExecute_ApplyVerticalScrollBarVisibility(object? obj)
        {
            return richtextbox.ApplyVerticalScrollBarVisibility.CanExecute(obj);
        }
        internal void Execute_ApplyVerticalScrollBarVisibility(object? obj)
        {
            richtextbox.ApplyVerticalScrollBarVisibility.Execute(obj);
        }








        internal bool CanExecute_Loaded(object? obj)
        {
            return service != null;
        }
        internal void Execute_Loaded(object? obj)
        {
        }


        internal bool CanExecute_Close(object? obj)
        {
            return true;
        }
        internal void Execute_Close(object? obj)
        {
        }


        internal bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        internal void Execute_Closing(object? obj)
        {
        }


        internal bool CanExecute_Closed(object? obj)
        {
            return true;
        }
        internal void Execute_Closed(object? obj)
        {
        }

        internal bool CanExecute_ApplyAcceptsTab(object? obj)
        {
            throw new NotImplementedException();
        }

        internal void Execute_ApplyAcceptsTab(object? obj)
        {
            throw new NotImplementedException();
        }

    }
}
