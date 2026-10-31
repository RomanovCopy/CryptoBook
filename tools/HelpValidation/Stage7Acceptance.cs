using Autofac;
using CryptoBook;
using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;
using CryptoBook.Validation;
using CryptoBook.Views;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaPlayer = CryptoBook.Views.MediaPlayer;

internal static partial class Program
{
    // Opt-in, human-operated acceptance. No successful hardware checks are inferred
    // from button clicks, shell launches, dialog returns or registered drivers.
    private static async Task RunStage7Acceptance(IsolatedServices isolated)
    {
        var c = isolated.Container;
        var main = Application.Current.Windows.OfType<MainWindow>().Single();
        var windows = c.Resolve<IWindowManager>();
        var scope = windows.FindHostWindow(((IMainWindowViewModel)main.DataContext).WindowId)!.Scope;
        var navigation = scope.Resolve<IPageNavigationService>();
        var open = c.Resolve<IWorkspaceFileOpenService>();
        c.Resolve<IThemeManager>().ApplyTheme(ApplicationTheme.Light);
        navigation.Navigate("Home");
        c.Resolve<IWorkspaceService>().SetWorkspaceDirectory(isolated.DemoDirectory);
        main.WindowState = WindowState.Normal;
        main.Width = 1100; main.Height = 800;

        string plain = Path.Combine(isolated.DemoDirectory, "Stage7-transfer.txt");
        string rtf = Path.Combine(isolated.DemoDirectory, "Stage7-ordinary.rtf");
        string markdown = Path.Combine(isolated.DemoDirectory, "Stage7-markdown.md");
        string pdf = Path.Combine(isolated.DemoDirectory, "CryptoBook-Stage7.pdf");
        string video = Path.Combine(isolated.DemoDirectory, "Stage7-tone.avi");
        File.WriteAllText(plain, "CryptoBook Stage 7 disposable transfer. Ж Є ä\n");
        File.WriteAllText(rtf, @"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}\f0\fs28 CryptoBook Stage 7\par\b ORDINARY PRINT\b0\par Disposable document. One page.}", Encoding.ASCII);
        File.WriteAllText(markdown, "# CryptoBook Stage 7\n\n**MARKDOWN PRINT**\n\n- First item\n- Second item\n\nDisposable document. One page.\n");
        MakeStage7Image(Path.Combine(isolated.DemoDirectory, "Stage7-image.png"), Colors.SteelBlue);
        MakeAcceptancePdf(pdf);
        MakeAcceptanceAvi(video);
        // A public demonstration password, never copied from the user's profile.
        char[] password = "CryptoBook-Help-Stage7!".ToCharArray();
        try { c.Resolve<IKeyProvider>().SetKey(password); }
        finally { Array.Clear(password); }
        await c.Resolve<ISecureFileProcessor>().EncryptFileAsync(plain, Path.Combine(isolated.DemoDirectory, "Stage7-transfer.cbook"));
        await open.OpenAsync(rtf);

        var events = new List<object>();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var panel = new StackPanel { Margin = new Thickness(15) };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) };
        var control = new Window
        {
            Title = "CryptoBook — приёмка этапа 7", Width = 535, Height = 780,
            Content = new ScrollViewer { Content = panel }, WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        void Record(string action, object? detail = null)
        {
            events.Add(new { at = DateTimeOffset.UtcNow, action, detail });
            File.WriteAllText(Path.Combine(isolated.Root, "acceptance.json"), JsonSerializer.Serialize(new
            {
                culture, commit, mode = "stage7-acceptance", status = "requires independent human observations",
                root = isolated.Root, events, bindingErrors,
                scope = "User-operated production windows, native dialogs and device monitoring; settings and stores isolated. No personal file names or device IDs logged automatically. No inferred physical printing or audible sound."
            }, Json));
        }
        void Button(string title, Func<Task> action)
        {
            var button = new Button { Content = title, Margin = new Thickness(0, 3, 0, 3), Padding = new Thickness(8) };
            button.Click += async (_, _) =>
            {
                button.IsEnabled = false;
                try { await action(); }
                catch (Exception e) { status.Text = e.Message; Record("action-error", new { title, type = e.GetType().Name }); }
                finally { button.IsEnabled = true; }
            };
            panel.Children.Add(button);
        }
        panel.Children.Add(new TextBlock
        {
            Text = "Изолированная аппаратная приёмка. Используйте только созданные ниже файлы и новую папку CryptoBook-Help-Stage7 на Android.\n\nПроверка физического EPSON исключена: принтер в другой сети. В диалоге печати выбирайте только Microsoft Print to PDF или Отмена.\n\nПароль расходного .cbook: CryptoBook-Help-Stage7!",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBox { Text = isolated.DemoDirectory, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) });
        panel.Children.Add(status);
        async Task Inventory()
        {
            var devices = await new WindowsPortableDeviceBridge().GetDevicesAsync();
            await c.Resolve<IDriveManagerService>().RefreshAsync();
            int available = devices.Count(d => d.IsAvailable);
            status.Text = $"MTP: обнаружено {devices.Count}, доступно {available}.";
            Record("device-inventory", new { discovered = devices.Count, available, states = devices.GroupBy(d => d.StatusText).Select(g => new { state = g.Key, count = g.Count() }) });
        }
        Button("1. Обновить устройства", Inventory);
        Button("2. Открыть расходные файлы / Android", async () =>
        {
            c.Resolve<IFileExplorerService>().Show(isolated.DemoDirectory);
            await Settle(main); Record("disposable-explorer-opened");
        });
        Button("3. Видео со звуком (первое окно)", () => ShowVideo(video));
        Button("4. Видео во втором окне", () => ShowVideo(video));
        async Task ShowVideo(string path)
        {
            var id = windows.CreateWindow<MediaPlayer>(new Dictionary<string, object?> { ["path"] = path });
            windows.ShowWindow(id);
            var window = (MediaPlayer)windows.FindHostWindow(id)!.Window;
            var vm = (IMediaPlayerViewModel)window.DataContext;
            await Until(() => vm.VideoService.IsMediaLoaded);
            vm.VideoService.Pause(); vm.VideoService.Volume = 20;
            status.Text = "Видео подготовлено на паузе. Активируйте плеер, нажмите Play в Flyleaf; проверьте движущийся кадр, звук, шкалу и клавиши.";
            Record("video-loaded", new { duration = vm.VideoService.Duration.TotalSeconds, vm.VideoService.IsMediaLoaded });
        }
        Button("5. Сохранить реальный кадр Flyleaf", async () =>
        {
            var player = Application.Current.Windows.OfType<MediaPlayer>().FirstOrDefault(w => w.IsVisible)
                ?? throw new InvalidOperationException("Сначала откройте видео.");
            var vm = (IMediaPlayerViewModel)player.DataContext;
            var native = (FlyleafLib.MediaPlayer.Player)vm.VideoService.PlayerInstance;
            string output = Path.Combine(isolated.Root, $"flyleaf-frame-{events.Count}.png");
            var bitmap = native.TakeSnapshotToBitmapSource();
            if (bitmap is null) throw new InvalidOperationException("Flyleaf не вернул кадр.");
            WriteBitmap(bitmap, output);
            status.Text = "Сохранён декодированный кадр Flyleaf. Это отдельный кадр, без имитации снимка окна.";
            Record("flyleaf-frame", new { file = Path.GetFileName(output), bitmap.PixelWidth, bitmap.PixelHeight, position = vm.VideoService.Position.TotalSeconds });
            await Task.CompletedTask;
        });
        Button("6. Открыть валидный PDF внешней программой", async () =>
        {
            string? before = c.Resolve<IDocumentSession>().FilePath;
            var result = await open.OpenAsync(pdf);
            if (!result.Success || result.OpenedInternally) throw new InvalidOperationException("Не удалось передать PDF внешней программе: " + result.Error);
            Record("pdf-shell-dispatch", new { result.Success, result.OpenedInternally, currentDocumentPreserved = before == c.Resolve<IDocumentSession>().FilePath });
            status.Text = "Windows приняла PDF. Подтвердите видимую страницу CryptoBook Stage 7 во внешнем приложении и вернитесь сюда.";
        });
        Button("7. Печать обычного документа / Отмена", () => Print(rtf));
        Button("8. Печать Markdown / Отмена", () => Print(markdown));
        async Task Print(string path)
        {
            var result = await open.OpenAsync(path);
            if (!result.Success) throw new InvalidOperationException(result.Error);
            await Settle(main);
            var command = scope.Resolve<IMenuFileViewModel>().PrintFile;
            if (!command.CanExecute(null)) throw new InvalidOperationException("Команда печати недоступна.");
            Record("native-print-requested", new { fixture = Path.GetFileName(path) });
            command.Execute(null);
            // The production print service returns void. Returning is not proof of a job or a printed sheet.
            Record("native-print-returned", new { fixture = Path.GetFileName(path), outcome = "requires human observation or output inspection" });
            status.Text = "Диалог закрыт. Укажите отдельно: отмена, PDF-вывод или ошибка. Возврат команды не подтверждает печать.";
        }
        Button("9. Снимок расходной папки (четыре языка)", async () =>
        {
            var explorer = Application.Current.Windows.OfType<FileExplorer>().FirstOrDefault(w => w.IsVisible)
                ?? throw new InvalidOperationException("Откройте файловый менеджер в расходной папке.");
            var vm = (IFileExplorerViewModel)explorer.DataContext;
            var storage = c.Resolve<IStorageFacade>();
            var location = storage.Resolve(vm.CurrentPath);
            bool disposable = location.IsLocal
                ? SameLocalPath(vm.CurrentPath, isolated.DemoDirectory)
                : storage.FormatDisplayPath(location).Replace('\\', '/').Split('/')
                    .Contains("CryptoBook-Help-Stage7", StringComparer.Ordinal);
            if (!disposable) throw new InvalidOperationException("Снимок разрешён только в расходной папке.");
            foreach (string language in new[] { "en-US", "ru-RU", "uk-UA", "de-DE" })
            {
                LocalizationManager.SelectCulture(language); await Settle(explorer);
                SaveWindow(explorer, Path.Combine(isolated.Root, $"device-explorer-{events.Count}-{language}.png"));
            }
            LocalizationManager.SelectCulture(culture);
            Record("explorer-evidence-captured", new { remote = !vm.CurrentPath.StartsWith(isolated.Root, StringComparison.OrdinalIgnoreCase), productionWindow = true });
        });
        Button("Завершить сеанс (не объявляет проверки пройденными)", () => { control.Close(); return Task.CompletedTask; });
        control.Closed += (_, _) => done.TrySetResult();
        Record("session-started", new { nativePrinting = c.Resolve<IDocumentPrintService>() is WpfDocumentPrintService });
        control.Show();
        await Inventory();
        Console.WriteLine($"HELP_ACCEPTANCE READY root={isolated.Root}");
        await done.Task;
        Record("session-closed");
    }

    private static void MakeAcceptancePdf(string path)
    {
        // A complete one-page ASCII PDF with byte-correct stream length and xref.
        const string content = "BT /F1 24 Tf 50 760 Td (CryptoBook Stage 7) Tj 0 -40 Td /F1 14 Tf (Disposable external PDF acceptance.) Tj ET\n";
        string[] objects = [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream"
        ];
        var text = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (int i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(text.ToString()));
            text.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        int xref = Encoding.ASCII.GetByteCount(text.ToString());
        text.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (int offset in offsets) text.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        text.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        File.WriteAllText(path, text.ToString(), Encoding.ASCII);
    }

    private static void MakeAcceptanceAvi(string path)
    {
        // 30 seconds of moving BGR24 bars plus a quiet pulsed 440 Hz PCM tone.
        // Both streams are generated; no screen, microphone or user media is read.
        const int width = 320, height = 180, fps = 10, count = 300;
        const int frameSize = width * height * 3, rate = 22050, samples = rate / fps, audioBytes = samples * 2;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII);
        void Four(string value) => writer.Write(Encoding.ASCII.GetBytes(value));
        long Start(string id) { Four(id); long at = stream.Position; writer.Write(0); return at; }
        void End(long at) { long end = stream.Position; stream.Position = at; writer.Write((int)(end - at - 4)); stream.Position = end; }
        long riff = Start("RIFF"); Four("AVI "); long header = Start("LIST"); Four("hdrl");
        Four("avih"); writer.Write(56); writer.Write(1000000 / fps); writer.Write(frameSize * fps + rate * 2); writer.Write(0); writer.Write(0x10); writer.Write(count); writer.Write(0); writer.Write(2); writer.Write(frameSize); writer.Write(width); writer.Write(height); for (int i = 0; i < 4; i++) writer.Write(0);
        long video = Start("LIST"); Four("strl"); Four("strh"); writer.Write(56); Four("vids"); Four("DIB "); writer.Write(0); writer.Write((short)0); writer.Write((short)0); writer.Write(0); writer.Write(1); writer.Write(fps); writer.Write(0); writer.Write(count); writer.Write(frameSize); writer.Write(-1); writer.Write(0); writer.Write((short)0); writer.Write((short)0); writer.Write((short)width); writer.Write((short)height);
        Four("strf"); writer.Write(40); writer.Write(40); writer.Write(width); writer.Write(height); writer.Write((short)1); writer.Write((short)24); writer.Write(0); writer.Write(frameSize); for (int i = 0; i < 4; i++) writer.Write(0); End(video);
        long audio = Start("LIST"); Four("strl"); Four("strh"); writer.Write(56); Four("auds"); writer.Write(0); writer.Write(0); writer.Write((short)0); writer.Write((short)0); writer.Write(0); writer.Write(2); writer.Write(rate * 2); writer.Write(0); writer.Write(count * samples); writer.Write(audioBytes); writer.Write(-1); writer.Write(2); for (int i = 0; i < 4; i++) writer.Write((short)0);
        Four("strf"); writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16); End(audio); End(header);
        long movi = Start("LIST"); Four("movi"); long origin = stream.Position - 4;
        var index = new List<(string id, int offset, int length)>();
        var frame = new byte[frameSize];
        for (int n = 0; n < count; n++)
        {
            index.Add(("00db", (int)(stream.Position - origin), frameSize)); Four("00db"); writer.Write(frameSize);
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int pixel = (y * width + x) * 3; bool bar = (x + n * 7) % width < 45;
                frame[pixel] = bar ? (byte)40 : (byte)160;
                frame[pixel + 1] = bar ? (byte)210 : (byte)65;
                frame[pixel + 2] = bar ? (byte)250 : (byte)35;
            }
            writer.Write(frame);
            index.Add(("01wb", (int)(stream.Position - origin), audioBytes)); Four("01wb"); writer.Write(audioBytes);
            for (int s = 0; s < samples; s++)
            {
                int sample = n * samples + s;
                double envelope = sample % rate < rate / 2 ? 0.08 : 0;
                writer.Write((short)(Math.Sin(2 * Math.PI * 440 * sample / rate) * short.MaxValue * envelope));
            }
        }
        End(movi); Four("idx1"); writer.Write(index.Count * 16);
        foreach (var item in index) { Four(item.id); writer.Write(0x10); writer.Write(item.offset); writer.Write(item.length); }
        End(riff);
    }
}
