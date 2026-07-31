using CryptoBook.Interfaces;

using Microsoft.Xaml.Behaviors;

using System.Windows;
using System.Windows.Input;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace CryptoBook.Behaviors
{
    public class ImageInteractionBehavior: Behavior<FrameworkElement>
    {
        private System.Windows.Point _lastMousePosition;
        private bool _isDragging;
        private Window? _window;

        // Создаем Dependency Property для привязки нашего IImageService из ViewModel
        public static readonly DependencyProperty ImageServiceProperty =
            DependencyProperty.Register(
                nameof(ImageService),
                typeof(IImageService),
                typeof(ImageInteractionBehavior),
                new PropertyMetadata(null));

        public IImageService? ImageService
        {
            get => (IImageService?)GetValue(ImageServiceProperty);
            set => SetValue(ImageServiceProperty, value);
        }

        public static readonly DependencyProperty PreviousImageCommandProperty =
            DependencyProperty.Register(
                nameof(PreviousImageCommand),
                typeof(ICommand),
                typeof(ImageInteractionBehavior),
                new PropertyMetadata(null));

        public ICommand? PreviousImageCommand
        {
            get => (ICommand?)GetValue(PreviousImageCommandProperty);
            set => SetValue(PreviousImageCommandProperty, value);
        }

        public static readonly DependencyProperty NextImageCommandProperty =
            DependencyProperty.Register(
                nameof(NextImageCommand),
                typeof(ICommand),
                typeof(ImageInteractionBehavior),
                new PropertyMetadata(null));

        public ICommand? NextImageCommand
        {
            get => (ICommand?)GetValue(NextImageCommandProperty);
            set => SetValue(NextImageCommandProperty, value);
        }

        public static readonly DependencyProperty DeleteImageCommandProperty =
            DependencyProperty.Register(
                nameof(DeleteImageCommand),
                typeof(ICommand),
                typeof(ImageInteractionBehavior),
                new PropertyMetadata(null));

        public ICommand? DeleteImageCommand
        {
            get => (ICommand?)GetValue(DeleteImageCommandProperty);
            set => SetValue(DeleteImageCommandProperty, value);
        }

        // Вызывается, когда поведение прикрепляется к элементу в XAML
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += AssociatedObject_Loaded;
            AssociatedObject.Unloaded += AssociatedObject_Unloaded;
            AssociatedObject.MouseWheel += AssociatedObject_MouseWheel;
            AssociatedObject.MouseDown += AssociatedObject_MouseDown;
            AssociatedObject.MouseUp += AssociatedObject_MouseUp;
            AssociatedObject.MouseMove += AssociatedObject_MouseMove;
        }

        // Вызывается при откреплении (предотвращает утечки памяти)
        protected override void OnDetaching()
        {
            DetachWindow();
            AssociatedObject.Loaded -= AssociatedObject_Loaded;
            AssociatedObject.Unloaded -= AssociatedObject_Unloaded;
            AssociatedObject.MouseWheel -= AssociatedObject_MouseWheel;
            AssociatedObject.MouseDown -= AssociatedObject_MouseDown;
            AssociatedObject.MouseUp -= AssociatedObject_MouseUp;
            AssociatedObject.MouseMove -= AssociatedObject_MouseMove;
            base.OnDetaching();
        }

        private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
        {
            DetachWindow();
            _window = Window.GetWindow(AssociatedObject);
            if(_window != null)
                _window.PreviewKeyDown += Window_PreviewKeyDown;
        }

        private void AssociatedObject_Unloaded(object sender, RoutedEventArgs e) =>
            DetachWindow();

        private void DetachWindow()
        {
            if(_window != null)
                _window.PreviewKeyDown -= Window_PreviewKeyDown;
            _window = null;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if(!AssociatedObject.IsVisible ||
                ImageService?.ImageSource == null ||
                Keyboard.Modifiers != ModifierKeys.None)
            {
                return;
            }

            switch(e.Key)
            {
                case Key.Left:
                    e.Handled = ExecuteCommand(PreviousImageCommand);
                    break;
                case Key.Right:
                    e.Handled = ExecuteCommand(NextImageCommand);
                    break;
                case Key.Up:
                    ZoomFromKeyboard(1.1);
                    e.Handled = true;
                    break;
                case Key.Down:
                    ZoomFromKeyboard(1 / 1.1);
                    e.Handled = true;
                    break;
                case Key.Delete:
                    e.Handled = ExecuteCommand(DeleteImageCommand);
                    break;
            }
        }

        private static bool ExecuteCommand(ICommand? command)
        {
            if(command?.CanExecute(null) != true)
                return false;

            command.Execute(null);
            return true;
        }

        private void ZoomFromKeyboard(double zoomFactor)
        {
            if(ImageService == null)
                return;

            var center = new System.Windows.Point(
                AssociatedObject.ActualWidth / 2,
                AssociatedObject.ActualHeight / 2);
            ImageService.Zoom(zoomFactor, center);
        }

        private void AssociatedObject_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if(ImageService == null)
                return;

            // Получаем позицию мыши относительно контейнера, к которому прикреплено поведение
            System.Windows.Point mousePos = e.GetPosition(AssociatedObject);
            double zoomFactor = e.Delta > 0 ? 1.1 : 1 / 1.1;

            ImageService.Zoom(zoomFactor, mousePos);
            e.Handled = true; // Перехватываем событие, чтобы окно не скроллилось
        }

        private void AssociatedObject_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(ImageService == null)
                return;

            if(e.ChangedButton == MouseButton.Left)
            {
                _lastMousePosition = e.GetPosition(AssociatedObject);
                _isDragging = true;
                AssociatedObject.CaptureMouse(); // Захватываем фокус мыши
                e.Handled = true;
            }
        }

        private void AssociatedObject_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if(e.ChangedButton == MouseButton.Left && _isDragging)
            {
                _isDragging = false;
                AssociatedObject.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void AssociatedObject_MouseMove(object sender, MouseEventArgs e)
        {
            if(_isDragging && ImageService != null)
            {
                System.Windows.Point currentMousePos = e.GetPosition(AssociatedObject);
                Vector delta = currentMousePos - _lastMousePosition;
                _lastMousePosition = currentMousePos;

                ImageService.Pan(delta);
                e.Handled = true;
            }
        }
    }
}
