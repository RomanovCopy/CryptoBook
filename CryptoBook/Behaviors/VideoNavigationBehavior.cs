using CryptoBook.Interfaces;

using FlyleafLib.Controls.WPF;

using Microsoft.Xaml.Behaviors;

using System.Windows;
using System.Windows.Input;

using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace CryptoBook.Behaviors
{
    /// <summary>
    /// Routes clip navigation shortcuts from Flyleaf's native render windows.
    /// </summary>
    public sealed class VideoNavigationBehavior: Behavior<FlyleafHost>
    {
        private Window? _surface;
        private Window? _overlay;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += OnLoaded;
            AssociatedObject.Unloaded += OnUnloaded;
            AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
            AssociatedObject.SurfaceCreated += OnFlyleafWindowCreated;
            AssociatedObject.OverlayCreated += OnFlyleafWindowCreated;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Loaded -= OnLoaded;
            AssociatedObject.Unloaded -= OnUnloaded;
            AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
            AssociatedObject.SurfaceCreated -= OnFlyleafWindowCreated;
            AssociatedObject.OverlayCreated -= OnFlyleafWindowCreated;
            DetachFlyleafWindows();
            base.OnDetaching();
        }

        private void OnLoaded(object sender, RoutedEventArgs args) =>
            AttachFlyleafWindows();

        private void OnUnloaded(object sender, RoutedEventArgs args) =>
            DetachFlyleafWindows();

        private void OnFlyleafWindowCreated(object? sender, EventArgs args) =>
            AttachFlyleafWindows();

        private void AttachFlyleafWindows()
        {
            AttachWindow(ref _surface, AssociatedObject.Surface);

            var overlay = ReferenceEquals(
                AssociatedObject.Overlay,
                AssociatedObject.Surface)
                ? null
                : AssociatedObject.Overlay;
            AttachWindow(ref _overlay, overlay);
        }

        private void DetachFlyleafWindows()
        {
            AttachWindow(ref _surface, null);
            AttachWindow(ref _overlay, null);
        }

        private void AttachWindow(ref Window? current, Window? replacement)
        {
            if(ReferenceEquals(current, replacement))
                return;

            if(current is not null)
                current.PreviewKeyDown -= OnPreviewKeyDown;

            current = replacement;
            if(current is not null)
                current.PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs args)
        {
            if(args.KeyboardDevice.Modifiers != ModifierKeys.Alt)
                return;

            var key = args.Key == Key.System ? args.SystemKey : args.Key;
            args.Handled = key switch
            {
                Key.Left => ExecuteNavigation(previous: true),
                Key.Right => ExecuteNavigation(previous: false),
                _ => false
            };
        }

        private bool ExecuteNavigation(bool previous)
        {
            if(Window.GetWindow(AssociatedObject)?.DataContext is not
                IMediaPlayerViewModel viewModel)
            {
                return false;
            }

            var command = previous
                ? viewModel.PreviousVideoCommand
                : viewModel.NextVideoCommand;
            if(!command.CanExecute(null))
                return false;

            command.Execute(null);
            return true;
        }
    }
}
