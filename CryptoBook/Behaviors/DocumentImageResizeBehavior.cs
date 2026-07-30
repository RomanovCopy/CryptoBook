using CryptoBook.Adorners;
using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using WpfImage = System.Windows.Controls.Image;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;

namespace CryptoBook.Behaviors
{
    /// <summary>
    /// Добавляет RichTextBox выбор встроенных изображений и управление
    /// ImageResizeAdorner без code-behind редактора.
    /// </summary>
    public static class DocumentImageResizeBehavior
    {
        public static RoutedUICommand SetImageLayoutCommand { get; } =
            new(
                "Изменить размещение изображения",
                nameof(SetImageLayoutCommand),
                typeof(DocumentImageResizeBehavior));

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(DocumentImageResizeBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static readonly DependencyProperty ImageEditorProperty =
            DependencyProperty.RegisterAttached(
                "ImageEditor",
                typeof(IEmbeddedImageEditor),
                typeof(DocumentImageResizeBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ImageLayoutServiceProperty =
            DependencyProperty.RegisterAttached(
                "ImageLayoutService",
                typeof(IEmbeddedImageLayoutService),
                typeof(DocumentImageResizeBehavior),
                new PropertyMetadata(null));

        private static readonly DependencyProperty StateProperty =
            DependencyProperty.RegisterAttached(
                "State",
                typeof(BehaviorState),
                typeof(DocumentImageResizeBehavior),
                new PropertyMetadata(null));

        public static void SetIsEnabled(
            DependencyObject element,
            bool value) =>
            element.SetValue(IsEnabledProperty, value);

        public static bool GetIsEnabled(DependencyObject element) =>
            (bool)element.GetValue(IsEnabledProperty);

        public static void SetImageEditor(
            DependencyObject element,
            IEmbeddedImageEditor? value) =>
            element.SetValue(ImageEditorProperty, value);

        public static IEmbeddedImageEditor? GetImageEditor(
            DependencyObject element) =>
            (IEmbeddedImageEditor?)element.GetValue(ImageEditorProperty);

        public static void SetImageLayoutService(
            DependencyObject element,
            IEmbeddedImageLayoutService? value) =>
            element.SetValue(ImageLayoutServiceProperty, value);

        public static IEmbeddedImageLayoutService? GetImageLayoutService(
            DependencyObject element) =>
            (IEmbeddedImageLayoutService?)element.GetValue(
                ImageLayoutServiceProperty);

        private static void OnIsEnabledChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            if(dependencyObject is not WpfRichTextBox richTextBox)
                return;

            if(args.NewValue is true)
            {
                if(richTextBox.GetValue(StateProperty) is null)
                {
                    richTextBox.SetValue(
                        StateProperty,
                        new BehaviorState(richTextBox));
                }
                return;
            }

            if(richTextBox.GetValue(StateProperty) is BehaviorState state)
            {
                state.Dispose();
                richTextBox.ClearValue(StateProperty);
            }
        }

        private sealed class BehaviorState: IDisposable
        {
            private readonly WpfRichTextBox richTextBox;
            private ImageResizeAdorner? adorner;
            private AdornerLayer? adornerLayer;
            private WpfImage? selectedImage;
            private readonly CommandBinding setLayoutCommandBinding;
            private bool disposed;

            public BehaviorState(WpfRichTextBox richTextBox)
            {
                this.richTextBox = richTextBox;
                richTextBox.PreviewMouseLeftButtonDown +=
                    OnPreviewMouseLeftButtonDown;
                richTextBox.PreviewMouseRightButtonDown +=
                    OnPreviewMouseRightButtonDown;
                richTextBox.PreviewKeyDown += OnPreviewKeyDown;
                richTextBox.TextChanged += OnTextChanged;
                richTextBox.Unloaded += OnUnloaded;

                setLayoutCommandBinding = new CommandBinding(
                    SetImageLayoutCommand,
                    OnSetImageLayout,
                    OnCanSetImageLayout);
                richTextBox.CommandBindings.Add(setLayoutCommandBinding);
            }

            public void Dispose()
            {
                if(disposed)
                    return;

                disposed = true;
                ClearSelection();
                richTextBox.PreviewMouseLeftButtonDown -=
                    OnPreviewMouseLeftButtonDown;
                richTextBox.PreviewMouseRightButtonDown -=
                    OnPreviewMouseRightButtonDown;
                richTextBox.PreviewKeyDown -= OnPreviewKeyDown;
                richTextBox.TextChanged -= OnTextChanged;
                richTextBox.Unloaded -= OnUnloaded;
                richTextBox.CommandBindings.Remove(
                    setLayoutCommandBinding);
            }

            private void OnPreviewMouseLeftButtonDown(
                object sender,
                MouseButtonEventArgs args)
            {
                if(IsInsideActiveAdorner(args.OriginalSource))
                    return;

                WpfImage? image = FindImage(args.OriginalSource);
                if(image is null)
                {
                    ClearSelection();
                    return;
                }

                Select(image);
            }

            private void OnPreviewMouseRightButtonDown(
                object sender,
                MouseButtonEventArgs args)
            {
                WpfImage? image = FindImage(args.OriginalSource);
                if(image is null)
                {
                    ClearSelection();
                    return;
                }

                Select(image);
            }

            private void OnPreviewKeyDown(
                object sender,
                WpfKeyEventArgs args)
            {
                if(selectedImage is null)
                    return;

                if(args.Key == Key.Escape)
                {
                    ClearSelection();
                    args.Handled = true;
                    return;
                }

                if(args.Key == Key.Delete &&
                   GetImageLayoutService(richTextBox) is { } layoutService)
                {
                    WpfImage image = selectedImage;
                    ClearSelection();
                    args.Handled = layoutService.Remove(image);
                }
            }

            private void OnCanSetImageLayout(
                object sender,
                CanExecuteRoutedEventArgs args)
            {
                args.CanExecute =
                    !richTextBox.IsReadOnly &&
                    selectedImage is not null &&
                    args.Parameter is ImageLayoutMode &&
                    GetImageLayoutService(richTextBox) is not null;
                args.Handled = true;
            }

            private void OnSetImageLayout(
                object sender,
                ExecutedRoutedEventArgs args)
            {
                if(selectedImage is not { } image ||
                   args.Parameter is not ImageLayoutMode mode ||
                   GetImageLayoutService(richTextBox) is not
                       { } layoutService)
                {
                    return;
                }

                ClearSelection();
                richTextBox.BeginChange();
                try
                {
                    layoutService.SetLayout(image, mode);
                } finally
                {
                    richTextBox.EndChange();
                }
                args.Handled = true;

                richTextBox.Dispatcher.BeginInvoke(
                    () =>
                    {
                        if(IsInDocument(image, richTextBox.Document))
                            Select(image);
                    },
                    DispatcherPriority.Loaded);
            }

            private void OnTextChanged(
                object sender,
                TextChangedEventArgs args)
            {
                if(selectedImage is not null &&
                   !IsInDocument(selectedImage, richTextBox.Document))
                {
                    ClearSelection();
                }
            }

            private void OnUnloaded(
                object sender,
                RoutedEventArgs args) =>
                ClearSelection();

            private void Select(WpfImage image)
            {
                if(ReferenceEquals(selectedImage, image))
                    return;

                ClearSelection();

                IEmbeddedImageEditor? imageEditor =
                    GetImageEditor(richTextBox);
                if(imageEditor is null)
                    return;

                AdornerLayer? layer = AdornerLayer.GetAdornerLayer(image);
                if(layer is null)
                    return;

                selectedImage = image;
                adornerLayer = layer;
                adorner = new ImageResizeAdorner(
                    image,
                    imageEditor,
                    () => GetAvailableWidth(richTextBox));
                adornerLayer.Add(adorner);
            }

            private void ClearSelection()
            {
                if(adorner is not null && adornerLayer is not null)
                    adornerLayer.Remove(adorner);

                adorner = null;
                adornerLayer = null;
                selectedImage = null;
            }

            private bool IsInsideActiveAdorner(object? source)
            {
                if(adorner is null)
                    return false;

                DependencyObject? current = source as DependencyObject;
                while(current is not null)
                {
                    if(ReferenceEquals(current, adorner))
                        return true;

                    current = GetParent(current);
                }

                return false;
            }

            private static WpfImage? FindImage(object? source)
            {
                DependencyObject? current = source as DependencyObject;

                while(current is not null)
                {
                    if(current is WpfImage image)
                        return image;

                    current = GetParent(current);
                }

                return null;
            }

            private static DependencyObject? GetParent(
                DependencyObject current)
            {
                if(current is Visual or
                   System.Windows.Media.Media3D.Visual3D)
                    return VisualTreeHelper.GetParent(current);

                if(current is FrameworkContentElement contentElement)
                    return contentElement.Parent;

                return LogicalTreeHelper.GetParent(current);
            }

            private static bool IsInDocument(
                WpfImage image,
                FlowDocument document)
            {
                DependencyObject? current =
                    image.Parent ?? LogicalTreeHelper.GetParent(image);

                while(current is not null)
                {
                    if(ReferenceEquals(current, document))
                        return true;

                    current = current switch
                    {
                        FrameworkContentElement contentElement =>
                            contentElement.Parent,
                        FrameworkElement element when element.Parent is not null =>
                            element.Parent,
                        _ => LogicalTreeHelper.GetParent(current)
                    };
                }

                return false;
            }

            private static double GetAvailableWidth(
                WpfRichTextBox richTextBox)
            {
                const double reserve = 32;
                double width = richTextBox.ActualWidth
                    - richTextBox.Document.PagePadding.Left
                    - richTextBox.Document.PagePadding.Right
                    - reserve;
                return double.IsFinite(width) && width > 0
                    ? Math.Max(64, width)
                    : 720;
            }
        }
    }
}
