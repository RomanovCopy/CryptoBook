using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IMediaPlayerService: IService, INotifyPropertyChanged, IDisposable
    {
        // События
        event EventHandler? MediaOpened;
        event EventHandler<string>? MediaFailed;
        event EventHandler? MediaEnded;

        // Нативный объект для FlyleafHost
        object PlayerInstance { get; }

        // Базовые свойства
        string? Source { get; }
        TimeSpan Position { get; set; }
        TimeSpan Duration { get; }
        double Volume { get; set; }
        bool IsMuted { get; set; }
        bool IsPlaying { get; }
        bool IsMediaLoaded { get; }

        // Продвинутые свойства (Скорость, Аудиодорожки, Субтитры)
        double PlaybackSpeed { get; set; }
        int CurrentAudioStreamIndex { get; }
        IReadOnlyList<string> AudioStreams { get; }
        int CurrentSubtitleStreamIndex { get; }
        IReadOnlyList<string> SubtitleStreams { get; }

        // Управление
        Task OpenAsync(string source, bool autoPlay = true, CancellationToken cancellationToken = default);
        Task OpenAsync(
            Stream source,
            string sourceName,
            bool autoPlay = true,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "The media player does not support stream sources.");
        void Play();
        void Pause();
        void Stop();
        void Seek(TimeSpan position);

        // Покадровая перемотка (Вперед / Назад)
        void FrameForward();
        void FrameBackward();
    }
}
