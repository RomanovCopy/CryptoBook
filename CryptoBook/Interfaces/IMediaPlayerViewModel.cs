using FlyleafLib.MediaPlayer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMediaPlayerViewModel:IViewModel,IWindowWithId,IDisposable
    {

        // Подключаем оба специализированных сервиса
        public IMediaPlayerService VideoService { get; }
        public IImageLoaderService ImageService { get; }

        public Visibility VideoVisibility
        {
            get => _videoVisibility;
            set { _videoVisibility = value; OnPropertyChanged(); }
        }

        public Visibility ImageVisibility
        {
            get => _imageVisibility;
            set { _imageVisibility = value; OnPropertyChanged(); }
        }

        public ICommand OpenFileCommand { get; }

        public MainViewModel(IMediaPlayerService videoService, IImageLoaderService imageService)
        {
            VideoService = videoService;
            ImageService = imageService;

            OpenFileCommand = new RelayCommand(async () => await OpenFileAsync());
        }

        private async Task OpenFileAsync()
        {
            var openFileDialog = new OpenFileDialog();
            if(openFileDialog.ShowDialog() != true)
                return;

            string path = openFileDialog.FileName;
            string ext = Path.GetExtension(path).ToLower();

            if(ext == ".jpg" || ext == ".png" || ext == ".jpeg" || ext == ".webp" || ext == ".bmp")
            {
                // Логика ФОТО
                VideoService.Stop();
                VideoVisibility = Visibility.Collapsed;

                await ImageService.LoadImageAsync(path);
                ImageVisibility = Visibility.Visible;
            } else
            {
                // Логика ВИДЕО
                ImageService.Clear();
                ImageVisibility = Visibility.Collapsed;

                VideoVisibility = Visibility.Visible;
                await VideoService.OpenAsync(path, autoPlay: true);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    }
}
