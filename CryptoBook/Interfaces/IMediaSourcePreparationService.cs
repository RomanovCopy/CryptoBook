using System.IO;

namespace CryptoBook.Interfaces
{
    public interface IMediaSourcePreparationService: IService
    {
        Task<IPreparedMediaSource> PrepareAsync(
            string sourcePath,
            CancellationToken cancellationToken = default);
    }

    public interface IPreparedMediaSource: IDisposable
    {
        string OriginalPath { get; }
        string PlaybackPath { get; }
        string OriginalExtension { get; }
        Stream? PlaybackStream { get; }
        bool IsEncrypted { get; }
        bool IsTemporary { get; }
    }
}
