using Autofac;

using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

using Controls=System.Windows.Controls;

namespace CryptoBook.Services
{
    public class RichTextBoxService: Controls.RichTextBox, IRichTextBoxService
    {
        private readonly ILifetimeScope scope;
        private readonly IFlowDocumentService flowDocumentService;

        private TextRange last_Selection;

        Controls.RichTextBox IRichTextBoxService.Service=> this;
        TextSelection IRichTextBoxService.Selection => this.Selection;
        TextPointer IRichTextBoxService.CaretPosition 
        { 
            get => this.CaretPosition;
            set => this.CaretPosition = value;
        }
        bool IRichTextBoxService.IsReadOnly 
        { 
            get => this.IsReadOnly;
            set => this.IsReadOnly = value;
        }
        bool IRichTextBoxService.SpellCheckEnabled 
        { 
            get => this.SpellCheck.IsEnabled;
            set => this.SpellCheck.IsEnabled = value;
        }
        bool IRichTextBoxService.CanUndo => this.CanUndo;
        bool IRichTextBoxService.CanRedo => this.CanRedo;

        public RichTextBoxService(ILifetimeScope scope)
        {
            this.scope = scope;
            flowDocumentService = scope.Resolve<IFlowDocumentService>();
            this.LostFocus += RichTextBoxService_LostFocus;
        }

        private void RichTextBoxService_LostFocus(object sender, RoutedEventArgs e)
        {
            last_Selection = new TextRange(Selection?.Start, Selection?.End);
        }
        void IRichTextBoxService.Focus()=> this.Focus();   
        void IRichTextBoxService.ScrollToCaret() => this.ScrollToVerticalOffset(this.VerticalOffset);
        void IRichTextBoxService.ScrollToStart()=>this.ScrollToHome();
        void IRichTextBoxService.ScrollToEnd()=> this.ScrollToEnd();
        void IRichTextBoxService.Copy()=>this.Copy();
        void IRichTextBoxService.Cut()=>this.Cut();
        void IRichTextBoxService.Paste()=>this.Paste();
        void IRichTextBoxService.SelectAll()=>this.SelectAll();
        void IRichTextBoxService.ClearSelection()=>this.Selection.Select(this.CaretPosition, this.CaretPosition);
        void IRichTextBoxService.RestoreSelection()
        {
            if(last_Selection != null)
            {
                this.CaretPosition = last_Selection.End;
                this.Selection.Select(last_Selection.Start, last_Selection.End);

            } else
            {
                this.CaretPosition = this.Selection.End;
                this.Selection.Select(this.Selection.End, this.Selection.End);
            }
            this.Focus();
        }
        void IRichTextBoxService.InsertTextAtCaret(string text)=> this.CaretPosition.InsertTextInRun(text);
        void IRichTextBoxService.Undo()=>this.Undo();
        void IRichTextBoxService.Redo()=>this.Redo();
        void IRichTextBoxService.ApplyVerticalScrollBarVisibility(ScrollBarVisibility visibility)
        {
            this.VerticalScrollBarVisibility = visibility;
        }
        void IRichTextBoxService.ApplyHorizontalScrollBarVisibility(ScrollBarVisibility visibility)
        {
            this.HorizontalScrollBarVisibility = visibility;
        }
        void IRichTextBoxService.ApplyContextMenu(ContextMenu menu)=>this.ContextMenu = menu;
        void IRichTextBoxService.ApplyAcceptsTab(bool accept)=>this.AcceptsTab = accept;
        void IRichTextBoxService.ApplyAcceptsReturn(bool accept)=>this.AcceptsReturn = accept;


        private object GetTextPropertiesInCaretPosition(DependencyProperty property)
        {
            TextPointer caret = this.CaretPosition.GetInsertionPosition(LogicalDirection.Backward);
            if(caret == null)
                return DependencyProperty.UnsetValue;

            TextRange range = new TextRange(caret, caret);
            return range.GetPropertyValue(property);
        }

    }
}
