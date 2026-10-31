using Autofac;
using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Help;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;
using CryptoBook.Validation;
using CryptoBook.ViewModels;
using CryptoBook.Views;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaPlayer = CryptoBook.Views.MediaPlayer;

internal static partial class Program
{
    private static readonly string[] Stage7Topics = ["search.name", "search.workspace", "search.document", "devices.android", "devices.transfers", "devices.limits", "view.preview", "view.images", "view.video", "view.pdf", "view.print"];

    private static async Task ValidateStage7(IsolatedServices isolated, bool verify)
    {
        var c = isolated.Container;
        await isolated.PrepareDemoDriveAsync();
        var main = Application.Current.Windows.OfType<MainWindow>().Single();
        c.Resolve<IThemeManager>().ApplyTheme(ApplicationTheme.Light);
        main.WindowState = WindowState.Normal; main.Width = 1440; main.Height = 960;
        var windows = c.Resolve<IWindowManager>();
        var scope = windows.FindHostWindow(((IMainWindowViewModel)main.DataContext).WindowId)!.Scope;
        var nav = scope.Resolve<IPageNavigationService>();
        var session = c.Resolve<IDocumentSession>();
        var open = c.Resolve<IWorkspaceFileOpenService>();
        var workspace = c.Resolve<IWorkspaceService>();
        workspace.SetWorkspaceDirectory(isolated.DemoDirectory);
        string word = Local("поездка", "подорож", "Reise", "journey");
        string body = Local("План: поездка утром. Поездка вечером.", "План: подорож уранці. Подорож увечері.", "Plan: Reise am Morgen. reise am Abend.", "Plan: journey in the morning. Journey in the evening.");
        string name = Local("План", "План", "Plan", "Plan");
        string plain = Path.Combine(isolated.DemoDirectory, name + ".txt");
        string nested = Path.Combine(isolated.DemoDirectory, "Nested"); Directory.CreateDirectory(nested);
        File.WriteAllText(plain, body); File.WriteAllText(Path.Combine(nested, name + ".md"), "# " + name + "\n\n" + body);
        File.WriteAllText(Path.Combine(isolated.Root, name + "-outside.txt"), body);
        demonstrationFile = plain;
        Check((await open.OpenAsync(plain)).Success, "Disposable text opens in the editor.");

        c.Resolve<ISettingsWindowService>().Open(); await Settle(main);
        var settingsWindow = Application.Current.Windows.OfType<SettingsWindow>().Single();
        settingsWindow.Width = 1120; settingsWindow.Height = 820;
        var settings = (ISettingsViewModel)settingsWindow.DataContext;
        settings.SelectedSectionIndex = 3; await Settle(settingsWindow);
        for (int index = 0; index < 6 && !Buttons(settingsWindow).Any(b => b.Command == settings.ChooseWorkspace); index++)
        { settings.SelectedSectionIndex = index; await Settle(settingsWindow); }
        settings.SearchQuery = name.ToUpperInvariant(); settings.Search.Execute(null);
        await Until(() => !settings.IsSearching && settings.SearchResults.Count == 2); await Settle(settingsWindow);
        Check(settings.SearchResults.All(r => r.FullPath.StartsWith(isolated.DemoDirectory + Path.DirectorySeparatorChar)), "Name search is recursive, case-insensitive and confined to the local workspace.");
        if (!verify) Shot(settingsWindow, "S03-c", "search.name", "steps 1-3", []);
        await ((AsyncRelayCommand)settings.OpenSearchResult).ExecuteAsync(settings.SearchResults.Single(r => r.FullPath == plain));
        Check(!settingsWindow.IsVisible && session.FilePath == plain, "Opening a name result closes settings and opens the file.");

        nav.Navigate("WorkspaceSearch"); await Settle(main);
        var search = (IWorkspaceSearchViewModel)nav.CurrentPage!.DataContext;
        search.SearchQuery = word; await ((AsyncRelayCommand)search.Search).ExecuteAsync(); await Settle(main);
        Check(search.SearchResults.Count == 2 && search.SearchResults.All(r => r.MatchCount == 2), "Content search returns one result per matching TXT/Markdown file with both literal matches.");
        if (!verify) Shot(main, "S26-a", "search.workspace", "steps 1-3", []);
        await ((AsyncRelayCommand)search.OpenResult).ExecuteAsync(search.SearchResults.Single(r => r.FullPath == plain)); await Settle(main);
        Check(session.FilePath == plain && nav.CurrentKey == "Home", "Content result opens the document and returns to its editor.");
        nav.Navigate("WorkspaceSearch"); await Settle(main);
        search.SearchQuery = "zzzz-no-match"; await ((AsyncRelayCommand)search.Search).ExecuteAsync();
        Check(search.SearchResults.Count == 0, "Content search reports no results.");
        search.ClearPage.Execute(null); Check(search.SearchQuery == "" && search.SearchResults.Count == 0, "Clear removes the query and results only.");
        search.SearchQuery = word;
        var running = ((AsyncRelayCommand)search.Search).ExecuteAsync(); search.CancelSearch.Execute(null); await running;
        Check(!search.IsSearching && !search.CancelSearch.CanExecute(null), "Cancel stops a live content search and re-enables controls.");

        var key = c.Resolve<IKeyProvider>();
        char[] demoPassword = ("Help-" + Guid.NewGuid().ToString("N") + "!7").ToCharArray();
        try { key.SetKey(demoPassword); } finally { Array.Clear(demoPassword); }
        string encrypted = Path.Combine(isolated.DemoDirectory, name + ".cbook");
        await c.Resolve<ISecureFileProcessor>().EncryptFileAsync(plain, encrypted);
        search.SearchQuery = word; await ((AsyncRelayCommand)search.Search).ExecuteAsync(); await Settle(main);
        Check(search.SearchResults.Count == 3 && search.SearchResults.Single(r => r.IsEncrypted).FullPath == encrypted, "Protected content is found with an available real key.");
        demonstrationFile = encrypted;
        if (!verify) Shot(main, "S26-b", "search.workspace", "protected result", []);
        var encryptedBytes = File.ReadAllBytes(encrypted);
        Check(encryptedBytes.Length > 0 && !Encoding.UTF8.GetString(encryptedBytes).Contains(body), "The disposable protected file does not contain the demonstration plaintext.");
        var protectedResult = search.SearchResults.Single(r => r.IsEncrypted);
        using (var prompt = OnVisible<MessageWindow>(w => { w.Close(); return Task.CompletedTask; }))
            await ((AsyncRelayCommand)search.DeleteResult).ExecuteAsync(protectedResult);
        Check(File.Exists(encrypted), "Cancelling result deletion preserves the file.");
        using (var prompt = OnVisible<MessageWindow>(w => { ((IMessageWindowViewModel)w.DataContext).OkCommand.Execute(null); return Task.CompletedTask; }))
            await ((AsyncRelayCommand)search.DeleteResult).ExecuteAsync(protectedResult);
        Check(!File.Exists(encrypted) && search.SearchResults.All(r => !r.IsEncrypted), "Confirmed deletion from content search removes the disposable file and result through its provider.");
        await c.Resolve<ISecureFileProcessor>().EncryptFileAsync(plain, encrypted);
        key.Clear();
        using (var prompt = OnVisible<KeyInputWindow>(w => { w.Close(); return Task.CompletedTask; }))
            await ((AsyncRelayCommand)search.Search).ExecuteAsync();
        Check(search.SearchResults.Count == 2 && search.SearchResults.All(r => !r.IsEncrypted) && File.Exists(encrypted), "Cancelling the real key dialog skips protected content and preserves its file.");
        File.Delete(encrypted);
        search.ClosePage.Execute(null); await Settle(main);
        demonstrationFile = plain;
        Check(nav.CurrentKey == "Home", "Closing search returns to the active editor.");

        var editor = c.Resolve<IRichTextBoxService>();
        var home = (IHomeViewModel)nav.CurrentPage!.DataContext;
        var documentSearch = home.DocumentView.Search;
        documentSearch.Open.Execute(null); documentSearch.Query = word; await Task.Delay(250); await Settle(main);
        Check(documentSearch.ResultCount == 2 && editor.Selection.Text.Equals(word, StringComparison.OrdinalIgnoreCase), "Ordinary document search highlights and selects the first match.");
        var queryBox = Visuals<TextBox>(main).Single(t => t.IsVisible && t.Text == word);
        queryBox.Focus(); await Settle(main);
        RaiseSearchKey(queryBox, main, Key.Enter); Check(documentSearch.CurrentIndex == 1, "Enter in the focused query advances to the next match.");
        RaiseSearchKey(queryBox, main, Key.F3); Check(documentSearch.CurrentIndex == 0, "F3 wraps to the first match.");
        if (!verify) Shot(main, "S27-a", "search.document", "steps 1-3", []);
        documentSearch.MatchCase = true; await Task.Delay(250); Check(documentSearch.ResultCount == 1, "Aa restricts ordinary search to matching case.");
        documentSearch.Query = "zzzz-no-match"; await Task.Delay(250); Check(documentSearch.ResultCount == 0 && !documentSearch.Next.CanExecute(null), "No-match search disables navigation.");
        await Settle(main);
        if (!verify) Shot(main, "S27-b", "search.document", "no matches and Aa", [], new Rect(0, 28, 1438, 300));
        documentSearch.MatchCase = false; documentSearch.Query = word; home.DocumentView.ToggleView.Execute(null); await Settle(main); await Task.Delay(250);
        Check(home.DocumentView.IsPreviewMode && !documentSearch.IsOpen, "Entering paged reading preview closes ordinary search.");
        if (!verify) Shot(main, "S27-c", "search.document", "reading preview before Find returns to editing", []);
        documentSearch.Open.Execute(null); await Settle(main); await Task.Delay(250);
        Check(!home.DocumentView.IsPreviewMode && documentSearch.ResultCount == 2, "Find from ordinary reading preview returns to editing and searches there.");
        RaiseSearchKey(Visuals<TextBox>(main).Single(t => t.IsVisible && t.Text == word), main, Key.Escape);
        Check(!documentSearch.IsOpen, "Escape routed from the search area closes ordinary document search.");

        string mdPath = Path.Combine(nested, name + ".md");
        await open.OpenAsync(mdPath); nav.Navigate("MarkdownEditor"); await Settle(main);
        var md = (IMarkdownEditorViewModel)nav.CurrentPage!.DataContext;
        demonstrationFile = mdPath;
        md.Search.Open.Execute(null); md.Search.Query = word; await Task.Delay(250); await Settle(main);
        Check(md.Search.ResultCount == 2, "Markdown source search finds the same two matches.");
        if (!verify) Shot(main, "S27-d", "search.document", "Markdown source", []);
        md.ToggleView.Execute(null); await Settle(main); await Task.Delay(250);
        Check(md.Search.ResultCount == 2, "Markdown preview searches rendered text.");
        if (!verify) Shot(main, "S27-e", "search.document", "Markdown preview", []);
        md.Search.Close.Execute(null);

        var menu = scope.Resolve<IMenuFileViewModel>();
        Check(menu.PrintFile.CanExecute(null), "Print is available for nonempty Markdown.");
        await ((AsyncRelayCommand)menu.PrintFile).ExecuteAsync();
        Check(isolated.PrintSink.Texts.Last().Contains(body) && !isolated.PrintSink.Texts.Last().Contains("# "), "Print command prepares rendered Markdown for the isolated sink.");
        await open.OpenAsync(plain); nav.Navigate("Home"); await Settle(main);
        demonstrationFile = plain;
        await ((AsyncRelayCommand)menu.PrintFile).ExecuteAsync();
        Check(isolated.PrintSink.Texts.Last().Contains(body), "Print command passes the current ordinary document to the isolated sink.");
        var mainModel = scope.Resolve<IMainWindowModel>(); mainModel.IsMenuOpen = true; await Settle(main);
        if (!verify) Shot(main, "S32-a", "view.print", "step 1", [new(1, Bounds(main, Buttons(main).Last(b => b.Command == menu.PrintFile)))]);
        mainModel.IsMenuOpen = false;

        string image = Path.Combine(isolated.DemoDirectory, name + "-1.png"), secondImage = Path.Combine(isolated.DemoDirectory, name + "-2.png");
        MakeStage7Image(image, Colors.SteelBlue); MakeStage7Image(secondImage, Colors.DarkOrange);
        File.WriteAllBytes(Path.Combine(isolated.DemoDirectory, "Preview.pdf"), "%PDF-1.7\n% Disposable header for unsupported-preview check only\n"u8.ToArray());
        c.Resolve<IFileExplorerService>().Show(isolated.DemoDirectory); await Settle(main);
        var explorer = Application.Current.Windows.OfType<FileExplorer>().Single(); explorer.Width = 1360; explorer.Height = 860;
        var files = (IFileExplorerViewModel)explorer.DataContext;
        await Until(() => files.ChildrenView.OfType<IFileItem>().Count() >= 4);
        var preview = files.Preview;
        async Task SelectPreview(string path)
        {
            demonstrationFile = path;
            var item = files.ChildrenView.OfType<IFileItem>().Single(i => SameLocalPath(i.FullPath, path));
            Visuals<ListView>(explorer).Single().SelectedItem = item; files.SelectedListItem = item;
            await preview.SelectAsync(item); await Settle(explorer);
        }
        await SelectPreview(plain); Check(preview.PreviewKind == FilePreviewKind.Text && preview.Text.Contains(body), "Production explorer preview displays local text.");
        if (!verify) Shot(explorer, "S30-a", "view.preview", "step 2: text", []);
        await SelectPreview(image); Check(preview.PreviewKind == FilePreviewKind.Image && preview.Image is not null, "Production explorer preview decodes the disposable PNG.");
        if (!verify) Shot(explorer, "S30-b", "view.preview", "step 2: image", []);
        await SelectPreview(Path.Combine(isolated.DemoDirectory, "Preview.pdf"));
        Check(preview.PreviewKind == FilePreviewKind.Unsupported && preview.Message.Contains(LocalizationManager.GetString("Preview.PdfUnsupported")), "PDF preview explicitly reports external-viewer requirement.");
        Check(c.Resolve<IFileTemplateRegistry>().GetAll().OfType<PdfFileTemplate>().Single().OpenMode == FileOpenMode.External, "PDF is registered for external opening only.");
        if (!verify) Shot(explorer, "S32-b", "view.pdf", "unsupported embedded preview", []);
        explorer.Close();

        demonstrationFile = image;
        var mediaId = windows.CreateWindow<MediaPlayer>(new Dictionary<string, object?> { ["path"] = image,
            [MediaCatalogSelection.WindowContextKey] = new MediaCatalogSelection(image, [image, secondImage]) });
        windows.ShowWindow(mediaId); var media = (MediaPlayer)windows.FindHostWindow(mediaId)!.Window;
        media.Width = 1040; media.Height = 680; await Settle(media);
        var player = (IMediaPlayerViewModel)media.DataContext;
        await Until(() => player.IsImageVisible && player.ImageService.ImageSource is not null);
        player.RotateImageCommand.Execute(null); Check(player.ImageService.RotationAngle == 90, "Rotate image changes display rotation.");
        RaiseSearchKey(media, media, Key.Up); Check(player.ImageService.Scale > 1, "Up zooms the image; it does not rotate.");
        player.ResetImageTransformCommand.Execute(null); Check(player.ImageService.Scale == 1 && player.ImageService.RotationAngle == 0, "Fit/reset clears rotation, zoom and offset.");
        Check(player.NextImageCommand.CanExecute(null), "The selected image catalog enables next image.");
        player.NextImageCommand.Execute(null); await Until(() => player.IsImageVisible && !player.ImageService.IsLoading && player.ImageService.CurrentImagePath == secondImage && player.ImageService.ImageSource is not null); await Settle(media);
        Check(player.PreviousImageCommand.CanExecute(null), "The second image enables previous image after loading.");
        player.PreviousImageCommand.Execute(null); await Until(() => player.IsImageVisible && !player.ImageService.IsLoading && player.ImageService.CurrentImagePath == image && player.ImageService.ImageSource is not null); await Settle(media);
        await Settle(media); if (!verify) Shot(media, "S30-c", "view.images", "image toolbar", []);
        using (var prompt = OnVisible<MessageWindow>(w => { w.Close(); return Task.CompletedTask; }))
        { player.DeleteCurrentImageCommand.Execute(null); await Until(() => !Application.Current.Windows.OfType<MessageWindow>().Any()); }
        Check(File.Exists(image), "Cancelling player image deletion retains the file.");
        using (var prompt = OnVisible<MessageWindow>(w => { ((IMessageWindowViewModel)w.DataContext).OkCommand.Execute(null); return Task.CompletedTask; }))
        { player.DeleteCurrentImageCommand.Execute(null); await Until(() => !File.Exists(image)); }
        await Until(() => player.ImageService.CurrentImagePath == secondImage && !player.ImageService.IsLoading);
        string? recycledImage = isolated.RecycleBin.LastRecycledPath;
        try
        {
            Check(recycledImage is not null && File.Exists(recycledImage), "Confirmed player deletion moves the original image to the Windows Recycle Bin.");
            Check(File.Exists(secondImage), "Confirmed player deletion affects only the disposable selected image.");
        }
        finally
        {
            if(recycledImage is not null) await RecycleBinValidation.RestoreAsync(recycledImage, image);
        }
        Check(File.Exists(image), "The deleted player image can be restored from its exact Windows Recycle Bin entry.");
        windows.CloseWindow(mediaId);

        demonstrationFile = null;
        var emptyId = windows.CreateWindow<MediaPlayer>(); windows.ShowWindow(emptyId);
        var emptyPlayer = (MediaPlayer)windows.FindHostWindow(emptyId)!.Window; await Settle(emptyPlayer);
        if (!verify) Shot(emptyPlayer, "S31-a", "view.video", "open and multi-player controls", []);
        windows.CloseWindow(emptyId);
        string video = Path.Combine(isolated.DemoDirectory, "Silent.avi"), secondVideo = Path.Combine(isolated.DemoDirectory, "Silent-2.avi"); MakeSilentAvi(video); File.Copy(video, secondVideo);
        var videoId = windows.CreateWindow<MediaPlayer>(new Dictionary<string, object?> { ["path"] = video,
            [MediaCatalogSelection.WindowContextKey] = new MediaCatalogSelection(video, [video, secondVideo]) }); windows.ShowWindow(videoId);
        var videoWindow = (MediaPlayer)windows.FindHostWindow(videoId)!.Window; await Settle(videoWindow);
        var videoVm = (IMediaPlayerViewModel)videoWindow.DataContext;
        await Until(() => videoVm.IsVideoVisible && videoVm.VideoService.IsMediaLoaded);
        var realPlayer = videoVm.VideoService; realPlayer.Play(); await Task.Delay(700);
        Check(realPlayer.IsPlaying && realPlayer.Position > TimeSpan.Zero && realPlayer.Duration.TotalSeconds >= 4, "Real Flyleaf/FFmpeg plays the generated silent AVI and advances position.");
        videoVm.PauseAllCommand.Execute(null); await Task.Delay(100);
        Check(!realPlayer.IsPlaying, "Pause all pauses the actual video player.");
        realPlayer.Seek(TimeSpan.FromSeconds(2)); await Task.Delay(250);
        Check(Math.Abs(realPlayer.Position.TotalSeconds - 2) < .6, "Actual video seek reaches the requested position.");
        double seekSeconds = realPlayer.Position.TotalSeconds;
        async Task NavigateVideo(ICommand command, string expected)
        {
            Check(command.CanExecute(null), "Adjacent-video command is enabled for the selected catalog.");
            var opened = new TaskCompletionSource();
            void OnOpened(object? sender, EventArgs e) => opened.TrySetResult();
            realPlayer.MediaOpened += OnOpened;
            try
            {
                command.Execute(null);
                Check(await Task.WhenAny(opened.Task, Task.Delay(5000)) == opened.Task, "Adjacent video actually opens through Flyleaf.");
                await Settle(videoWindow);
                Check(realPlayer.Source == expected && realPlayer.IsMediaLoaded, "Adjacent video source matches the selected catalog item.");
            }
            finally { realPlayer.MediaOpened -= OnOpened; }
        }
        await NavigateVideo(videoVm.NextVideoCommand, secondVideo);
        await NavigateVideo(videoVm.PreviousVideoCommand, video);
        var peerId = windows.CreateWindow<MediaPlayer>(new Dictionary<string, object?> { ["path"] = secondVideo }); windows.ShowWindow(peerId);
        var peerWindow = (MediaPlayer)windows.FindHostWindow(peerId)!.Window; await Settle(peerWindow);
        var peer = (IMediaPlayerViewModel)peerWindow.DataContext;
        await Until(() => peer.IsVideoVisible && peer.VideoService.IsMediaLoaded);
        realPlayer.Play(); peer.VideoService.Play(); await Task.Delay(250);
        Check(realPlayer.IsPlaying && peer.VideoService.IsPlaying, "Two real player windows play independent disposable videos.");
        videoVm.PauseAllCommand.Execute(null); await Task.Delay(100);
        Check(!realPlayer.IsPlaying && !peer.VideoService.IsPlaying, "Pause all stops both real players.");
        videoVm.ToggleSynchronizationCommand.Execute(null);
        Check(videoVm.IsSynchronizationEnabled && peer.IsSynchronizationEnabled, "Experimental synchronization switch is shared by both real player windows.");
        videoVm.ToggleSynchronizationCommand.Execute(null);
        Check(!peer.IsSynchronizationEnabled, "Synchronization can be disabled again.");
        WriteJson($"docs/help/screenshots/validation/stage7/media-{culture}.json", new { codec = "uncompressed BGR24 AVI, silent, generated disposable frames", realPlayer.IsMediaLoaded, durationSeconds = realPlayer.Duration.TotalSeconds, seekSeconds, capture = "No D3D video pixel capture claimed", synchronization = "two real windows: independent playback, Pause all, shared switch on/off; timing alignment is not claimed" });
        windows.CloseWindow(peerId);
        windows.CloseWindow(videoId);

        // Read-only inventory only; never browse or mutate an actual device in this host.
        var portable = await new WindowsPortableDeviceBridge().GetDevicesAsync();
        WriteJson($"docs/help/screenshots/validation/stage7/devices-{culture}.json", new { transport = "production WindowsPortableDeviceBridge / StorageDevice selector", discoveredCount = portable.Count, availableCount = portable.Count(d => d.IsAvailable), deviceOperations = "none; no device identifiers or personal directory listings retained" });

        c.Resolve<IHelpWindowService>().Open("search.name"); await Settle(main);
        var help = Application.Current.Windows.OfType<HelpWindow>().Single(); var helpVm = (HelpWindowViewModel)help.DataContext;
        if (verify) foreach (string id in Stage7Topics)
        {
            helpVm.Navigate.Execute(id); await Settle(help);
            var article = c.Resolve<HelpContentService>().Find(culture, id)!;
            Check(helpVm.CurrentTopicId == id && Text(helpVm.Article).Contains(article.Title), "Embedded localized article: " + id);
            foreach (string path in article.Images) Check(c.Resolve<HelpContentService>().LoadImage(culture, article, path) is not null, "Embedded localized image: " + id + "/" + path);
        }
        help.Close();
        Check(isolated.Paths.All(p => p.StartsWith(isolated.Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)), "All persistent stores remain in the disposable validation root.");
    }

    private static void RaiseSearchKey(UIElement target, Window window, Key key) => target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,
        PresentationSource.FromVisual(window), Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });

    private static void MakeStage7Image(string path, Color color)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, 640, 360));
            dc.DrawEllipse(Brushes.Gold, null, new Point(480, 90), 42, 42);
            dc.DrawGeometry(Brushes.White, null, Geometry.Parse("M 20,340 L 230,110 410,340 Z"));
        }
        var bitmap = new RenderTargetBitmap(640, 360, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); WriteBitmap(bitmap, path);
    }

    private static void MakeSilentAvi(string path)
    {
        // Minimal indexed AVI: five seconds of changing BGR24 frames, no audio/device/network input.
        const int width = 160, height = 90, fps = 10, count = 50, frameSize = width * height * 3;
        using var stream = File.Create(path); using var w = new BinaryWriter(stream, Encoding.ASCII);
        void Four(string s) => w.Write(Encoding.ASCII.GetBytes(s));
        long Start(string id) { Four(id); long p = stream.Position; w.Write(0); return p; }
        void End(long p) { long end = stream.Position; stream.Position = p; w.Write((int)(end - p - 4)); stream.Position = end; }
        long riff = Start("RIFF"); Four("AVI "); long hdrl = Start("LIST"); Four("hdrl");
        Four("avih"); w.Write(56); w.Write(1000000 / fps); w.Write(frameSize * fps); w.Write(0); w.Write(0x10); w.Write(count); w.Write(0); w.Write(1); w.Write(frameSize); w.Write(width); w.Write(height); for (int i = 0; i < 4; i++) w.Write(0);
        long strl = Start("LIST"); Four("strl"); Four("strh"); w.Write(56); Four("vids"); Four("DIB "); w.Write(0); w.Write((short)0); w.Write((short)0); w.Write(0); w.Write(1); w.Write(fps); w.Write(0); w.Write(count); w.Write(frameSize); w.Write(-1); w.Write(0); w.Write((short)0); w.Write((short)0); w.Write((short)width); w.Write((short)height);
        Four("strf"); w.Write(40); w.Write(40); w.Write(width); w.Write(height); w.Write((short)1); w.Write((short)24); w.Write(0); w.Write(frameSize); for (int i = 0; i < 4; i++) w.Write(0); End(strl); End(hdrl);
        long movi = Start("LIST"); Four("movi"); long origin = stream.Position - 4; var offsets = new List<int>();
        for (int i = 0; i < count; i++)
        {
            offsets.Add((int)(stream.Position - origin)); Four("00db"); w.Write(frameSize);
            byte[] frame = new byte[frameSize]; for (int j = 0; j < frame.Length; j += 3) { frame[j] = (byte)(60 + i * 3); frame[j + 1] = (byte)(100 + (j / 3 % width)); frame[j + 2] = (byte)(220 - i * 2); } w.Write(frame);
        }
        End(movi); Four("idx1"); w.Write(count * 16); foreach (int offset in offsets) { Four("00db"); w.Write(0x10); w.Write(offset); w.Write(frameSize); } End(riff);
    }
}
