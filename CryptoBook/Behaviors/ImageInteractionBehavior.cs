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

namespace CryptoBook.Behaviors
{
    public class ImageInteractionBehavior: Behavior<FrameworkElement>
    {
        private System.Windows.Point _lastMousePosition;
        private bool _isDragging;

        // Создаем Dependency Property для привязки нашего IImageService из ViewModel
        public static readonly DependencyProperty ImageServiceProperty =
            DependencyProperty.Register(
                nameof(ImageService),
                typeof(IImageService),
                typeof(ImageInteractionBehavior),
                new PropertyMetadata(null));

        public IImageService ImageService
        {
            get => (IImageService)GetValue(ImageServiceProperty);
            set => SetValue(ImageServiceProperty, value);
        }

        // Вызывается, когда поведение прикрепляется к элементу в XAML
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.MouseWheel += AssociatedObject_MouseWheel;
            AssociatedObject.MouseDown += AssociatedObject_MouseDown;
            AssociatedObject.MouseUp += AssociatedObject_MouseUp;
            AssociatedObject.MouseMove += AssociatedObject_MouseMove;
        }

        // Вызывается при откреплении (предотвращает утечки памяти)
        protected override void OnDetaching()
        {
            AssociatedObject.MouseWheel -= AssociatedObject_MouseWheel;
            AssociatedObject.MouseDown -= AssociatedObject_MouseDown;
            AssociatedObject.MouseUp -= AssociatedObject_MouseUp;
            AssociatedObject.MouseMove -= AssociatedObject_MouseMove;
            base.OnDetaching();
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
