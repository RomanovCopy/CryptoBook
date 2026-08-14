using CryptoBook.Interfaces;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

using WpfCursor = System.Windows.Input.Cursor;
using WpfCursors = System.Windows.Input.Cursors;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfImage = System.Windows.Controls.Image;
using WpfPen = System.Windows.Media.Pen;
using WpfSize = System.Windows.Size;

namespace CryptoBook.Adorners
{
    /// <summary>
    /// Показывает четыре угловых маркера, предварительно отображает новый
    /// размер рамкой и фиксирует Width/Height изображения по окончании жеста.
    /// </summary>
    public sealed class ImageResizeAdorner: Adorner
    {
        private const double ThumbSize = 10;
        private const double MoveButtonWidth = 30;
        private const double MoveButtonHeight = 26;
        private const double MoveButtonGap = 4;
        private const double MoveButtonOffset = 6;
        private const double MinimumImageWidth = 48;

        private readonly IEmbeddedImageEditor imageEditor;
        private readonly Func<double> getMaximumWidth;
        private readonly VisualCollection visuals;
        private readonly Thumb topLeft;
        private readonly Thumb topRight;
        private readonly Thumb bottomLeft;
        private readonly Thumb bottomRight;
        private readonly WpfButton moveUpButton;
        private readonly WpfButton moveDownButton;

        private bool isDragging;
        private double initialWidth;
        private double initialHeight;
        private double aspectRatio;
        private double cumulativeHorizontalChange;
        private double cumulativeVerticalChange;
        private double previewWidth;
        private double previewHeight;
        private int activeHorizontalDirection;
        private int activeVerticalDirection;

        public ImageResizeAdorner(
            WpfImage image,
            IEmbeddedImageEditor imageEditor,
            Func<double> getMaximumWidth,
            ICommand moveUpCommand,
            ICommand moveDownCommand,
            IInputElement commandTarget,
            string moveUpToolTip,
            string moveDownToolTip)
            : base(image)
        {
            this.imageEditor = imageEditor
                ?? throw new ArgumentNullException(nameof(imageEditor));
            this.getMaximumWidth = getMaximumWidth
                ?? throw new ArgumentNullException(nameof(getMaximumWidth));
            ArgumentNullException.ThrowIfNull(moveUpCommand);
            ArgumentNullException.ThrowIfNull(moveDownCommand);
            ArgumentNullException.ThrowIfNull(commandTarget);

            IsHitTestVisible = true;
            visuals = new VisualCollection(this);

            topLeft = CreateThumb(
                WpfCursors.SizeNWSE,
                horizontalDirection: -1,
                verticalDirection: -1);
            topRight = CreateThumb(
                WpfCursors.SizeNESW,
                horizontalDirection: 1,
                verticalDirection: -1);
            bottomLeft = CreateThumb(
                WpfCursors.SizeNESW,
                horizontalDirection: -1,
                verticalDirection: 1);
            bottomRight = CreateThumb(
                WpfCursors.SizeNWSE,
                horizontalDirection: 1,
                verticalDirection: 1);
            moveUpButton = CreateMoveButton(
                "↑",
                moveUpToolTip,
                moveUpCommand,
                commandTarget);
            moveDownButton = CreateMoveButton(
                "↓",
                moveDownToolTip,
                moveDownCommand,
                commandTarget);

            visuals.Add(topLeft);
            visuals.Add(topRight);
            visuals.Add(bottomLeft);
            visuals.Add(bottomRight);
            visuals.Add(moveUpButton);
            visuals.Add(moveDownButton);
        }

        private WpfImage Image => (WpfImage)AdornedElement;

        protected override int VisualChildrenCount => visuals.Count;

        protected override Visual GetVisualChild(int index) => visuals[index];

        protected override WpfSize ArrangeOverride(WpfSize finalSize)
        {
            double half = ThumbSize / 2;
            double width = GetDisplayWidth();
            double height = GetDisplayHeight();

            topLeft.Arrange(new Rect(
                -half,
                -half,
                ThumbSize,
                ThumbSize));
            topRight.Arrange(new Rect(
                width - half,
                -half,
                ThumbSize,
                ThumbSize));
            bottomLeft.Arrange(new Rect(
                -half,
                height - half,
                ThumbSize,
                ThumbSize));
            bottomRight.Arrange(new Rect(
                width - half,
                height - half,
                ThumbSize,
                ThumbSize));

            double toolbarWidth =
                MoveButtonWidth * 2 + MoveButtonGap;
            double toolbarLeft = (width - toolbarWidth) / 2;
            double toolbarTop =
                -MoveButtonHeight - MoveButtonOffset;
            moveUpButton.Arrange(new Rect(
                toolbarLeft,
                toolbarTop,
                MoveButtonWidth,
                MoveButtonHeight));
            moveDownButton.Arrange(new Rect(
                toolbarLeft + MoveButtonWidth + MoveButtonGap,
                toolbarTop,
                MoveButtonWidth,
                MoveButtonHeight));

            return finalSize;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var pen = new WpfPen(WpfBrushes.DodgerBlue, 1)
            {
                DashStyle = DashStyles.Dash
            };
            drawingContext.DrawRectangle(
                null,
                pen,
                new Rect(
                    0,
                    0,
                    GetDisplayWidth(),
                    GetDisplayHeight()));
        }

        private Thumb CreateThumb(
            WpfCursor cursor,
            int horizontalDirection,
            int verticalDirection)
        {
            var thumb = new Thumb
            {
                Width = ThumbSize,
                Height = ThumbSize,
                Cursor = cursor,
                Background = WpfBrushes.White,
                BorderBrush = WpfBrushes.DodgerBlue,
                BorderThickness = new Thickness(2)
            };

            thumb.DragStarted += (_, _) =>
                BeginResize(horizontalDirection, verticalDirection);
            thumb.DragDelta += (_, args) =>
                UpdatePreview(
                    args.HorizontalChange,
                    args.VerticalChange);
            thumb.DragCompleted += (_, args) =>
                CompleteResize(args.Canceled);
            return thumb;
        }

        private static WpfButton CreateMoveButton(
            string glyph,
            string toolTip,
            ICommand command,
            IInputElement commandTarget)
        {
            var button = new WpfButton
            {
                Width = MoveButtonWidth,
                Height = MoveButtonHeight,
                Padding = new Thickness(0),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Content = glyph,
                ToolTip = toolTip,
                Command = command,
                CommandTarget = commandTarget,
                Cursor = WpfCursors.Hand,
                Focusable = false
            };
            ToolTipService.SetInitialShowDelay(button, 200);
            ToolTipService.SetShowDuration(button, 10_000);
            ToolTipService.SetBetweenShowDelay(button, 0);
            System.Windows.Automation.AutomationProperties.SetName(
                button,
                toolTip);
            System.Windows.Automation.AutomationProperties.SetHelpText(
                button,
                toolTip);
            return button;
        }

        private void BeginResize(
            int horizontalDirection,
            int verticalDirection)
        {
            initialWidth = GetCurrentWidth();
            initialHeight = GetCurrentHeight();
            if(initialWidth <= 0 || initialHeight <= 0)
                return;

            aspectRatio = initialHeight / initialWidth;
            cumulativeHorizontalChange = 0;
            cumulativeVerticalChange = 0;
            previewWidth = initialWidth;
            previewHeight = initialHeight;
            activeHorizontalDirection = horizontalDirection;
            activeVerticalDirection = verticalDirection;
            isDragging = true;
        }

        private void UpdatePreview(
            double horizontalChange,
            double verticalChange)
        {
            if(!isDragging)
                return;

            cumulativeHorizontalChange += horizontalChange;
            cumulativeVerticalChange += verticalChange;

            double projectedWidthChange =
                (cumulativeHorizontalChange * activeHorizontalDirection +
                 cumulativeVerticalChange *
                 activeVerticalDirection *
                 aspectRatio) /
                (1 + aspectRatio * aspectRatio);
            double maximumWidth = Math.Max(
                MinimumImageWidth,
                getMaximumWidth());

            previewWidth = Math.Clamp(
                initialWidth + projectedWidthChange,
                MinimumImageWidth,
                maximumWidth);
            previewHeight = previewWidth * aspectRatio;
            InvalidateArrange();
            InvalidateVisual();
        }

        private void CompleteResize(bool canceled)
        {
            if(!isDragging)
                return;

            isDragging = false;
            if(!canceled)
            {
                imageEditor.ResizeToWidth(
                    Image,
                    previewWidth,
                    getMaximumWidth());
            }

            InvalidateArrange();
            InvalidateVisual();
        }

        private double GetDisplayWidth()
        {
            return isDragging
                ? previewWidth
                : AdornedElement.RenderSize.Width;
        }

        private double GetDisplayHeight()
        {
            return isDragging
                ? previewHeight
                : AdornedElement.RenderSize.Height;
        }

        private double GetCurrentWidth()
        {
            if(double.IsFinite(Image.Width) && Image.Width > 0)
                return Image.Width;
            if(Image.ActualWidth > 0)
                return Image.ActualWidth;
            return Image.Source?.Width ?? 0;
        }

        private double GetCurrentHeight()
        {
            if(double.IsFinite(Image.Height) && Image.Height > 0)
                return Image.Height;
            if(Image.ActualHeight > 0)
                return Image.ActualHeight;
            return Image.Source?.Height ?? 0;
        }
    }
}
