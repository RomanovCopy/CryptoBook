using CryptoBook.Adorners;
using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using WpfImage = System.Windows.Controls.Image;
using WpfCursor = System.Windows.Input.Cursor;
using WpfCursors = System.Windows.Input.Cursors;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
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
            private WpfImage? dragImage;
            private WpfPoint dragStartPoint;
            private TextPointer? dropPosition;
            private WpfCursor? originalCursor;
            private double originalImageOpacity;
            private bool isDraggingImage;
            private readonly CommandBinding setLayoutCommandBinding;
            private bool disposed;

            public BehaviorState(WpfRichTextBox richTextBox)
            {
                this.richTextBox = richTextBox;
                richTextBox.PreviewMouseLeftButtonDown +=
                    OnPreviewMouseLeftButtonDown;
                richTextBox.PreviewMouseRightButtonDown +=
                    OnPreviewMouseRightButtonDown;
                richTextBox.PreviewMouseMove += OnPreviewMouseMove;
                richTextBox.PreviewMouseLeftButtonUp +=
                    OnPreviewMouseLeftButtonUp;
                richTextBox.LostMouseCapture += OnLostMouseCapture;
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
                richTextBox.PreviewMouseMove -= OnPreviewMouseMove;
                richTextBox.PreviewMouseLeftButtonUp -=
                    OnPreviewMouseLeftButtonUp;
                richTextBox.LostMouseCapture -= OnLostMouseCapture;
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
                bool insideAdorner =
                    IsInsideActiveAdorner(args.OriginalSource);
                if(insideAdorner &&
                   IsInsideResizeThumb(args.OriginalSource))
                {
                    return;
                }

                WpfImage? image =
                    FindImage(args.OriginalSource) ??
                    (insideAdorner ? selectedImage : null);
                if(image is null)
                {
                    ClearSelection();
                    return;
                }

                Select(image);
                if(!richTextBox.IsReadOnly)
                {
                    dragImage = image;
                    dragStartPoint = args.GetPosition(richTextBox);
                    dropPosition = null;
                }
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

            private void OnPreviewMouseMove(
                object sender,
                WpfMouseEventArgs args)
            {
                if(dragImage is null ||
                   args.LeftButton != MouseButtonState.Pressed)
                {
                    return;
                }

                WpfPoint point = args.GetPosition(richTextBox);
                if(!isDraggingImage)
                {
                    Vector distance = point - dragStartPoint;
                    if(Math.Abs(distance.X) <
                           SystemParameters.MinimumHorizontalDragDistance &&
                       Math.Abs(distance.Y) <
                           SystemParameters.MinimumVerticalDragDistance)
                    {
                        return;
                    }

                    BeginImageDrag();
                }

                UpdateDropPosition(point);
                args.Handled = true;
            }

            private void OnPreviewMouseLeftButtonUp(
                object sender,
                MouseButtonEventArgs args)
            {
                if(!isDraggingImage)
                {
                    dragImage = null;
                    return;
                }

                UpdateDropPosition(args.GetPosition(richTextBox));
                CompleteImageDrag();
                args.Handled = true;
            }

            private void OnLostMouseCapture(
                object sender,
                WpfMouseEventArgs args)
            {
                if(isDraggingImage)
                    CancelImageDrag();
            }

            private void OnPreviewKeyDown(
                object sender,
                WpfKeyEventArgs args)
            {
                if(selectedImage is null)
                    return;

                if(args.Key == Key.Escape)
                {
                    if(isDraggingImage)
                        CancelImageDrag();
                    else
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
                    richTextBox.CaretPosition =
                        layoutService.GetTextInsertionPosition(
                            image,
                            mode);
                } finally
                {
                    richTextBox.EndChange();
                }
                args.Handled = true;
                richTextBox.Focus();

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
                RoutedEventArgs args)
            {
                CancelImageDrag();
                ClearSelection();
            }

            private void BeginImageDrag()
            {
                if(dragImage is null)
                    return;

                isDraggingImage = true;
                originalImageOpacity = dragImage.Opacity;
                dragImage.Opacity = 0.65;
                originalCursor = richTextBox.Cursor;
                richTextBox.Cursor = WpfCursors.SizeAll;
                richTextBox.CaptureMouse();
            }

            private void UpdateDropPosition(WpfPoint point)
            {
                TextPointer? position = richTextBox.GetPositionFromPoint(
                    point,
                    snapToText: true);
                if(position?.Paragraph is null)
                    return;

                dropPosition = position;
                richTextBox.CaretPosition = position;
                richTextBox.Selection.Select(position, position);
            }

            private void CompleteImageDrag()
            {
                WpfImage? image = dragImage;
                TextPointer? destination = dropPosition;
                EndImageDragVisuals();

                if(image is null ||
                   destination is null ||
                   GetImageLayoutService(richTextBox) is not
                       { } layoutService)
                {
                    return;
                }

                ImageLayoutMode mode = layoutService.GetLayout(image);
                ClearSelection();
                bool moved;
                richTextBox.BeginChange();
                try
                {
                    moved = layoutService.Move(image, destination);
                    if(moved)
                    {
                        richTextBox.CaretPosition =
                            layoutService.GetTextInsertionPosition(
                                image,
                                mode);
                    }
                } finally
                {
                    richTextBox.EndChange();
                }

                richTextBox.Focus();
                if(!moved)
                {
                    Select(image);
                    return;
                }

                richTextBox.Dispatcher.BeginInvoke(
                    () =>
                    {
                        if(IsInDocument(image, richTextBox.Document))
                            Select(image);
                    },
                    DispatcherPriority.Loaded);
            }

            private void CancelImageDrag()
            {
                EndImageDragVisuals();
            }

            private void EndImageDragVisuals()
            {
                WpfImage? image = dragImage;
                isDraggingImage = false;
                dragImage = null;
                dropPosition = null;

                if(image is not null)
                    image.Opacity = originalImageOpacity;
                richTextBox.Cursor = originalCursor;
                originalCursor = null;

                if(richTextBox.IsMouseCaptured)
                    richTextBox.ReleaseMouseCapture();
            }

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
                    () => GetAvailableWidth(richTextBox, image));
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

            private static bool IsInsideResizeThumb(object? source)
            {
                DependencyObject? current = source as DependencyObject;
                while(current is not null)
                {
                    if(current is System.Windows.Controls.Primitives.Thumb)
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
                WpfRichTextBox richTextBox,
                WpfImage image)
            {
                DocumentPageLayout.Apply(richTextBox.Document);
                return double.PositiveInfinity;
            }
        }
    }
}
