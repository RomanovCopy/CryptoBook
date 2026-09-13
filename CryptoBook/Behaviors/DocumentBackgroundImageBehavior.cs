using CryptoBook.Interfaces;
using CryptoBook.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using RichTextBox = System.Windows.Controls.RichTextBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Brushes = System.Windows.Media.Brushes;
using Cursor = System.Windows.Input.Cursor;
using Cursors = System.Windows.Input.Cursors;
using Control = System.Windows.Controls.Control;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace CryptoBook.Behaviors;

/// <summary>Clips the paper image and routes Ctrl+left-drag to document appearance editing.</summary>
public static class DocumentBackgroundImageBehavior
{
    static DocumentBackgroundImageBehavior()
    {
        // Handle the gesture before the editor's selection, hyperlink and inline-image handlers.
        EventManager.RegisterClassHandler(typeof(RichTextBox), UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler((sender, args) =>
                (((RichTextBox)sender).GetValue(StateProperty) as State)?.MouseDown(sender, args)));
        EventManager.RegisterClassHandler(typeof(RichTextBox), UIElement.PreviewKeyDownEvent,
            new System.Windows.Input.KeyEventHandler((sender, args) =>
                (((RichTextBox)sender).GetValue(StateProperty) as State)?.KeyDown(sender, args)));
    }

    public static readonly DependencyProperty FontServiceProperty = DependencyProperty.RegisterAttached(
        "FontService", typeof(IFontService), typeof(DocumentBackgroundImageBehavior),
        new PropertyMetadata(null, OnServiceChanged));
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(State), typeof(DocumentBackgroundImageBehavior));

    public static IFontService? GetFontService(DependencyObject element) => (IFontService?)element.GetValue(FontServiceProperty);
    public static void SetFontService(DependencyObject element, IFontService? value) => element.SetValue(FontServiceProperty, value);

    internal static bool BeginDrag(RichTextBox editor, Point point, ModifierKeys modifiers) =>
        (editor.GetValue(StateProperty) as State)?.BeginDrag(point, modifiers) == true;

    internal static bool MoveDrag(RichTextBox editor, Point point, ModifierKeys modifiers, MouseButtonState button) =>
        (editor.GetValue(StateProperty) as State)?.MoveDrag(point, modifiers, button) == true;

    private static void OnServiceChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if(element is not RichTextBox editor)
            return;
        editor.Loaded -= Attach;
        editor.Unloaded -= Detach;
        Detach(editor, new RoutedEventArgs());
        if(args.NewValue is IFontService)
        {
            editor.Loaded += Attach;
            editor.Unloaded += Detach;
            if(editor.IsLoaded)
                Attach(editor, new RoutedEventArgs());
        }
    }

    private static void Attach(object sender, RoutedEventArgs args)
    {
        var editor = (RichTextBox)sender;
        if(editor.GetValue(StateProperty) is null && GetFontService(editor) is { } fonts)
            editor.SetValue(StateProperty, new State(editor, fonts));
    }

    private static void Detach(object sender, RoutedEventArgs args)
    {
        var editor = (RichTextBox)sender;
        (editor.GetValue(StateProperty) as State)?.Dispose();
        editor.ClearValue(StateProperty);
    }

    private sealed class State : IDisposable
    {
        private readonly RichTextBox editor;
        private readonly IFontService fonts;
        private ImageBrush? source;
        private Rect paper, viewport;
        private FlowDocument? dragDocument;
        private Rect startCrop, originalCrop;
        private Size dragPaperSize;
        private Point dragStart;
        private bool hasMoved;
        private Cursor? originalCursor;
        private FrameworkElement? renderScope;
        private double originalMaxWidth;
        private HorizontalAlignment originalAlignment;

        public State(RichTextBox editor, IFontService fonts)
        {
            this.editor = editor;
            this.fonts = fonts;
            editor.LayoutUpdated += Update;
            fonts.DocumentBackgroundChanged += Update;
            editor.PreviewMouseMove += MouseMove;
            editor.PreviewMouseLeftButtonUp += MouseUp;
            editor.LostMouseCapture += LostCapture;
            Update(null, EventArgs.Empty);
        }

        private void Update(object? sender, EventArgs args)
        {
            if(dragDocument is not null && (editor.Document != dragDocument || editor.IsReadOnly ||
               editor.Document.Background is not ImageBrush current || DocumentBackgroundImageLayout.IsFinalized(current)))
                EndDrag();
            if(editor.Document.Background is not ImageBrush image)
            {
                if(editor.Background == Brushes.Transparent)
                    editor.SetCurrentValue(Control.BackgroundProperty, editor.Document.Background);
                source = null;
                RestoreRenderScope();
                return;
            }
            var presenter = FindPresenter(editor);
            if(presenter is null || presenter.ActualWidth <= 0 || presenter.ActualHeight <= 0 ||
               !double.IsFinite(editor.Document.PageWidth))
                return;
            // WPF also paints FlowDocument.Background on its internal document view.
            // Constrain that view, rather than changing the serialized FlowDocument.
            if(VisualTreeHelper.GetChildrenCount(presenter) > 0 &&
               VisualTreeHelper.GetChild(presenter, 0) is FrameworkElement scope)
            {
                if(renderScope != scope)
                {
                    RestoreRenderScope();
                    renderScope = scope;
                    originalMaxWidth = scope.MaxWidth;
                    originalAlignment = scope.HorizontalAlignment;
                }
                if(scope.MaxWidth != editor.Document.PageWidth || scope.HorizontalAlignment != HorizontalAlignment.Left)
                {
                    scope.SetCurrentValue(FrameworkElement.MaxWidthProperty, editor.Document.PageWidth);
                    scope.SetCurrentValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                    return;
                }
            }
            var bounds = new Rect(presenter.TranslatePoint(new Point(), editor), presenter.RenderSize);
            // ContentEnd is in the scrolled document view's coordinates. Convert back
            // to paper coordinates; neither viewport height nor zoom defines the paper.
            double contentHeight = editor.Document.ContentEnd.GetCharacterRect(LogicalDirection.Backward).Bottom +
                editor.VerticalOffset + editor.Document.PagePadding.Bottom;
            var paperSize = DocumentBackgroundImageLayout.GetPaperSize(editor.Document.PageWidth, contentHeight);
            image = DocumentBackgroundImageLayout.MapToPaper(image, paperSize);
            if(editor.Document.Background != image)
                editor.Document.Background = image;
            var page = new Rect(bounds.Left - editor.HorizontalOffset, bounds.Top - editor.VerticalOffset,
                paperSize.Width, paperSize.Height);
            if(dragDocument is not null && dragPaperSize != paperSize)
                EndDrag();
            source = image;
            paper = page;
            viewport = bounds;
            // The document view paints the image once. Painting it on the control too
            // would fill the workspace and double the opacity of translucent images.
            if(editor.Background != Brushes.Transparent)
                editor.SetCurrentValue(Control.BackgroundProperty, Brushes.Transparent);
        }

        public void MouseDown(object sender, MouseButtonEventArgs args)
        {
            if(!args.Handled && BeginDrag(args.GetPosition(editor), Keyboard.Modifiers))
                args.Handled = true;
        }

        public bool BeginDrag(Point point, ModifierKeys modifiers)
        {
            Update(null, EventArgs.Empty);
            if(dragDocument is not null || modifiers != ModifierKeys.Control || editor.IsReadOnly ||
               source?.ImageSource is null || DocumentBackgroundImageLayout.IsFinalized(source) ||
               !paper.Contains(point) || !viewport.Contains(point))
                return false;
            if(!editor.CaptureMouse())
                return false;
            dragDocument = editor.Document;
            dragStart = point - (Vector)paper.TopLeft;
            hasMoved = false;
            dragPaperSize = paper.Size;
            originalCrop = source.Viewbox;
            startCrop = DocumentBackgroundImageLayout.GetVisibleCrop(source, dragPaperSize);
            originalCursor = editor.Cursor;
            editor.SetCurrentValue(FrameworkElement.CursorProperty, Cursors.SizeAll);
            return true;
        }

        private void MouseMove(object sender, MouseEventArgs args)
        {
            if(MoveDrag(args.GetPosition(editor), Keyboard.Modifiers, args.LeftButton))
                args.Handled = true;
        }

        public bool MoveDrag(Point point, ModifierKeys modifiers, MouseButtonState button)
        {
            Update(null, EventArgs.Empty);
            if(dragDocument is null)
                return false;
            if(editor.Document != dragDocument || editor.IsReadOnly || button != MouseButtonState.Pressed ||
               modifiers != ModifierKeys.Control)
            {
                EndDrag();
                return true;
            }
            var delta = point - (Vector)paper.TopLeft - dragStart;
            if(delta.LengthSquared > 0 || hasMoved)
            {
                fonts.SetDocumentBackgroundImageCrop(delta.LengthSquared == 0 ? originalCrop :
                    DocumentBackgroundImageLayout.MoveCrop(startCrop, dragPaperSize, delta));
                hasMoved = true;
                Update(null, EventArgs.Empty);
            }
            return true;
        }

        private void MouseUp(object sender, MouseButtonEventArgs args)
        {
            if(dragDocument is null)
                return;
            EndDrag();
            args.Handled = true;
        }

        private void LostCapture(object sender, MouseEventArgs args) => EndDrag();

        public void KeyDown(object sender, System.Windows.Input.KeyEventArgs args)
        {
            if(dragDocument is null || args.Key != Key.Escape)
                return;
            if(editor.Document == dragDocument)
                fonts.SetDocumentBackgroundImageCrop(originalCrop);
            EndDrag();
            args.Handled = true;
        }

        private void EndDrag()
        {
            if(dragDocument is null)
                return;
            dragDocument = null;
            editor.SetCurrentValue(FrameworkElement.CursorProperty, originalCursor);
            if(editor.IsMouseCaptured)
                editor.ReleaseMouseCapture();
        }

        public void Dispose()
        {
            EndDrag();
            editor.LayoutUpdated -= Update;
            fonts.DocumentBackgroundChanged -= Update;
            editor.PreviewMouseMove -= MouseMove;
            editor.PreviewMouseLeftButtonUp -= MouseUp;
            editor.LostMouseCapture -= LostCapture;
            if(editor.Background == Brushes.Transparent)
                editor.SetCurrentValue(Control.BackgroundProperty, editor.Document.Background);
            RestoreRenderScope();
        }

        private void RestoreRenderScope()
        {
            if(renderScope is null)
                return;
            renderScope.SetCurrentValue(FrameworkElement.MaxWidthProperty, originalMaxWidth);
            renderScope.SetCurrentValue(FrameworkElement.HorizontalAlignmentProperty, originalAlignment);
            renderScope = null;
        }
    }

    private static ScrollContentPresenter? FindPresenter(DependencyObject parent)
    {
        for(int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if(child is ScrollContentPresenter presenter)
                return presenter;
            if(FindPresenter(child) is { } nested)
                return nested;
        }
        return null;
    }
}
