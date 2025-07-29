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

using Media = System.Windows.Media;
using Draving = System.Drawing;

using Controls = System.Windows.Controls;
using FontStyle = System.Windows.FontStyle;
using System.Collections.ObjectModel;

namespace CryptoBook.Services
{
    public class RichTextBoxService: Controls.RichTextBox, IRichTextBoxService
    {
        private readonly ILifetimeScope scope;

        private TextRange last_Selection;

        FlowDocument IRichTextBoxService.Document => this.Document;

        private bool userSelectionText { get; set; }
        private bool userSelectionTextFinished { get; set; }
        bool IRichTextBoxService.IsBold => GetTextPropertiesInCaretPosition(Controls.RichTextBox.FontWeightProperty) is FontWeight weight && weight == FontWeights.Bold;

        bool IRichTextBoxService.IsItalic => GetTextPropertiesInCaretPosition(Controls.RichTextBox.FontStyleProperty) is FontStyle style && style == FontStyles.Italic;

        bool IRichTextBoxService.IsUnderline => GetTextPropertiesInCaretPosition(Inline.TextDecorationsProperty) is TextDecorationCollection decorations && decorations.Contains(TextDecorations.Underline.FirstOrDefault());

        double IRichTextBoxService.FontSize { get => this.FontSize; set => this.FontSize = value; }

        string IRichTextBoxService.FontFamily => GetTextPropertiesInCaretPosition(Controls.RichTextBox.FontFamilyProperty) is Media.FontFamily family ? family.Source : "Segoe UI"; // Default font family if not set

        string IRichTextBoxService.FontColor
        {
            get
            {
                var color = GetTextPropertiesInCaretPosition(Controls.RichTextBox.ForegroundProperty) as Media.Brush;
                return color is Media.SolidColorBrush solidColor ? solidColor.Color.ToString() : Draving.Color.Black.ToString();
            }
        }

        string IRichTextBoxService.FontStile
        {
            get
            {
                var style = GetTextPropertiesInCaretPosition(Controls.RichTextBox.FontStyleProperty) as FontStyle?;
                return style.HasValue ? style.Value.ToString() : FontStyles.Normal.ToString();
            }
        }


        Controls.RichTextBox IRichTextBoxService.Service => this;
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


        public ObservableCollection<double> FontSizes => new ObservableCollection<double>
        {
            8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40,
            44, 48, 52, 56, 60, 64, 68, 72
        };

        public ObservableCollection<string> FontFamilies { get; } // Коллекция доступных семейств шрифтов
        public ObservableCollection<Color> FontColors { get; } // Коллекция доступных цветов шрифта
        public ObservableCollection<Brush> BackgrondColor { get; } // Коллекция доступных цветов фона


        public RichTextBoxService(ILifetimeScope scope)
        {
            this.scope = scope;
            this.LostFocus += RichTextBoxService_LostFocus;
            this.SelectionChanged += RichTextBoxService_SelectionChanged;
            this.PreviewMouseLeftButtonUp += RichTextBoxService_PreviewMouseLeftButtonUp;
        }

        private void RichTextBoxService_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if(userSelectionText && this.IsMouseCaptureWithin )
            {
                userSelectionText = false;
                userSelectionTextFinished = true;
                //var font = scope.Resolve<IFontFormatBar_ViewModel>();
                //var fontsize = GetFontSizeInSelection();
                //font.FontSize = fontsize;
            }
        }

        void IRichTextBoxService.Focus() => this.Focus();
        void IRichTextBoxService.ScrollToCaret() => this.ScrollToVerticalOffset(this.VerticalOffset);
        void IRichTextBoxService.ScrollToStart() => this.ScrollToHome();
        void IRichTextBoxService.ScrollToEnd() => this.ScrollToEnd();
        void IRichTextBoxService.Copy() => this.Copy();
        void IRichTextBoxService.Cut() => this.Cut();
        void IRichTextBoxService.Paste() => this.Paste();
        void IRichTextBoxService.SelectAll() => this.SelectAll();
        void IRichTextBoxService.ClearSelection() => this.Selection.Select(this.CaretPosition, this.CaretPosition);
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
        void IRichTextBoxService.InsertTextAtCaret(string text) => this.CaretPosition.InsertTextInRun(text);
        void IRichTextBoxService.Undo() => this.Undo();
        void IRichTextBoxService.Redo() => this.Redo();
        void IRichTextBoxService.ApplyVerticalScrollBarVisibility(ScrollBarVisibility visibility)
        {
            this.VerticalScrollBarVisibility = visibility;
        }
        void IRichTextBoxService.ApplyHorizontalScrollBarVisibility(ScrollBarVisibility visibility)
        {
            this.HorizontalScrollBarVisibility = visibility;
        }
        void IRichTextBoxService.ApplyContextMenu(ContextMenu menu) => this.ContextMenu = menu;
        void IRichTextBoxService.ApplyAcceptsTab(bool accept) => this.AcceptsTab = accept;
        void IRichTextBoxService.ApplyAcceptsReturn(bool accept) => this.AcceptsReturn = accept;

        private void RichTextBoxService_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if(!Selection.IsEmpty)
                userSelectionText = true;
        }

        public double GetFontSizeInSelection()
        {
            if(Selection.IsEmpty)
                return (double)(Selection.GetPropertyValue(TextElement.FontSizeProperty) ?? 12.0);

            TextPointer start = Selection.Start;
            TextPointer end = Selection.End;

            var sizes = new List<double>();
            var position = start;

            while(position != null && position.CompareTo(end) < 0)
            {
                if(position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text ||
                    position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementStart)
                {
                    var element = position.Parent as TextElement;
                    if(element != null)
                    {
                        var sizeObj = element.GetValue(TextElement.FontSizeProperty);
                        if(sizeObj is double size)
                            sizes.Add(size);
                    }
                }
                position = position.GetNextContextPosition(LogicalDirection.Forward);
            }
            if(sizes.Count == 0)
                return 12.0;
            return sizes.Count > 1 ? 0 : sizes[0];
        }



        private object GetTextPropertiesInCaretPosition(DependencyProperty property)
        {
            TextPointer caret = this.CaretPosition.GetInsertionPosition(LogicalDirection.Backward);
            if(caret == null)
                return DependencyProperty.UnsetValue;

            TextRange range = new TextRange(caret, caret);
            return range.GetPropertyValue(property);
        }

        private void RichTextBoxService_LostFocus(object sender, RoutedEventArgs e)
        {
            last_Selection = new TextRange(Selection?.Start, Selection?.End);
        }


    }
}
