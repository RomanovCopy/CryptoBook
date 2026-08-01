using CryptoBook.Interfaces;

using CryptoBook.Infrastructure;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CryptoBook.Services
{
    public class ImageService:IImageService
    {
        private ImageSource? _imageSource;
        private bool _isLoading;
        private string? _currentImagePath;

        // Внутреннее состояние матрицы трансформации
        private Matrix _transformMatrix = Matrix.Identity;
        private double _rotationAngle;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ImageSource? ImageSource
        {
            get => _imageSource;
            private set { _imageSource = value; OnPropertyChanged(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); }
        }

        public string? CurrentImagePath
        {
            get => _currentImagePath;
            private set { _currentImagePath = value; OnPropertyChanged(); }
        }

        // Экспортируем данные матрицы в простые типы для UI
        public double Scale => _transformMatrix.M11;
        public System.Windows.Point Offset => new System.Windows.Point(_transformMatrix.OffsetX, _transformMatrix.OffsetY);
        public double RotationAngle => _rotationAngle;

        // Свойство для прямой передачи готовой матрицы в XAML
        public Matrix TransformMatrix => _transformMatrix;

        public async Task LoadImageAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if(!File.Exists(filePath))
                throw new FileNotFoundException(
                    LocalizationManager.GetString("Image.FileNotFound"),
                    filePath);

            IsLoading = true;
            CurrentImagePath = filePath;
            ResetTransform();

            try
            {
                ImageSource = await Task.Run(() =>
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(filePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Замораживаем для межпоточного доступа
                    return bitmap;
                }, cancellationToken);
            } catch(OperationCanceledException)
            {
                Clear();
                throw;
            } catch
            {
                Clear();
                throw;
            } finally
            {
                IsLoading = false;
            }
        }

        public void Zoom(double zoomFactor, System.Windows.Point mousePosition)
        {
            if(ImageSource == null)
                return;

            // Ограничиваем минимальный и максимальный зум (от 0.5x до 20x)
            double currentScale = _transformMatrix.M11;
            if(currentScale * zoomFactor < 0.5 || currentScale * zoomFactor > 20)
                return;

            // Масштабируем матрицу относительно точки курсора мыши
            _transformMatrix.ScaleAt(zoomFactor, zoomFactor, mousePosition.X, mousePosition.Y);
            NotifyTransformChanged();
        }

        public void Pan(Vector dragDelta)
        {
            if(ImageSource == null)
                return;

            // Сдвигаем матрицу на дельту движения мыши
            _transformMatrix.Translate(dragDelta.X, dragDelta.Y);
            NotifyTransformChanged();
        }

        public void RotateRight()
        {
            if(ImageSource == null)
                return;

            _rotationAngle = (_rotationAngle + 90) % 360;
            OnPropertyChanged(nameof(RotationAngle));
        }

        public void ResetTransform()
        {
            _transformMatrix = Matrix.Identity;
            _rotationAngle = 0;
            NotifyTransformChanged();
            OnPropertyChanged(nameof(RotationAngle));
        }

        public void Clear()
        {
            ImageSource = null;
            CurrentImagePath = null;
            IsLoading = false;
            ResetTransform();
        }

        private void NotifyTransformChanged()
        {
            OnPropertyChanged(nameof(Scale));
            OnPropertyChanged(nameof(Offset));
            OnPropertyChanged(nameof(TransformMatrix));
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
