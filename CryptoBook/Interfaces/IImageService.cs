using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace CryptoBook.Interfaces
{
    public interface IImageService: INotifyPropertyChanged,IService
    {
        ImageSource? ImageSource { get; }
        bool IsLoading { get; }
        string? CurrentImagePath { get; }

        // Интерактивные свойства для биндинга
        double Scale { get; }
        System.Windows.Point Offset { get; }
        double RotationAngle { get; } // Поворот на 90, 180, 270 градусов
        Matrix TransformMatrix { get; }

        // Управление файлом
        Task LoadImageAsync(string filePath, CancellationToken cancellationToken = default);
        Task LoadImageAsync(
            Stream source,
            string sourceName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "The image service does not support stream sources.");
        void Clear();

        // Управление трансформацией
        void Zoom(double zoomFactor, System.Windows.Point mousePosition);
        void Pan(Vector dragDelta);
        void RotateRight(); // Поворот по часовой стрелке
        void ResetTransform(); // Сброс зума и смещения (вписать в экран)
    }
}
