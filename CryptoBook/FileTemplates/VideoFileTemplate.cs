using CryptoBook.Interfaces;

namespace CryptoBook.FileTemplates
{
    /// <summary>
    /// Шаблон видеофайла для контейнеров, открываемых установленным Flyleaf/FFmpeg.
    /// Конкретный кодек внутри контейнера проверяется проигрывателем при открытии.
    /// </summary>
    public sealed class VideoFileTemplate: IFileTemplate
    {
        public string Id => "Video";
        public string DisplayName =>
            CryptoBook.Infrastructure.LocalizationManager.GetString(
                "FileTemplate.Video");
        public string DefaultExtension => ".mp4";
        public string SuggestedBaseName =>
            CryptoBook.Infrastructure.LocalizationManager.GetString(
                "FileTemplate.NewVideo");
        public bool CanCreate => false;
        public FileOpenMode OpenMode => FileOpenMode.Media;

        public IReadOnlyCollection<string> Extensions =>
        [
            ".3g2",
            ".3gp",
            ".asf",
            ".avi",
            ".divx",
            ".dv",
            ".f4v",
            ".flv",
            ".m2t",
            ".m2ts",
            ".m4v",
            ".mkv",
            ".mov",
            ".mp4",
            ".mpe",
            ".mpeg",
            ".mpg",
            ".mts",
            ".mxf",
            ".ogm",
            ".ogv",
            ".rm",
            ".rmvb",
            ".ts",
            ".vob",
            ".webm",
            ".wm",
            ".wmv",
            ".wtv"
        ];

        public Task<byte[]> GetInitialContentAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Array.Empty<byte>());
        }
    }
}
