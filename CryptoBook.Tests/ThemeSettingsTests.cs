using Autofac;

using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.Models;
using CryptoBook.Services;
using CryptoBook.Views;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Xunit;

namespace CryptoBook.Tests;

public sealed class ThemeSettingsTests
{
    [WpfTheory]
    [InlineData("LightTheme")]
    [InlineData("DarkTheme")]
    [InlineData("SepiaTheme")]
    public void ThemeTextPairs_HaveReadableContrast(string resourceName)
    {
        ResourceDictionary theme = LoadDictionary(
            $"/CryptoBook;component/Themes/{resourceName}.xaml");

        AssertContrast(theme, "CurrentWindowForeground", "CurrentWindowBackground");
        AssertContrast(theme, "CurrentTitleBarForeground", "CurrentTitleBarBackground");
        AssertContrast(theme, "CurrentInputForeground", "CurrentInputBackground");
        AssertContrast(theme, "CurrentMutedForeground", "CurrentWindowBackground");
        AssertContrast(theme, "CurrentDisabledForeground", "CurrentControlBackground");
        AssertContrast(theme, "CurrentErrorForeground", "CurrentWindowBackground");
        AssertContrast(theme, "CurrentSelectionForeground", "CurrentSelectionBackground");
    }

    [WpfFact]
    public void ThemedControls_DefineImplicitReadableStyles()
    {
        ResourceDictionary styles = LoadDictionary(
            "/CryptoBook;component/Styles/ThemedControls.xaml");

        Assert.IsType<Style>(styles[typeof(Window)]);
        Assert.IsType<Style>(styles[typeof(TextBlock)]);
        Assert.IsType<Style>(styles[typeof(TextBox)]);
        Assert.IsType<Style>(styles[typeof(PasswordBox)]);
        Assert.IsType<Style>(styles[typeof(ContextMenu)]);
        Assert.IsType<Style>(styles[typeof(ToolTip)]);
    }

    [WpfFact]
    public void ThemeManager_InitializesAndReplacesActiveDictionary()
    {
        Application app = Application.Current ?? new Application();
        var previousResources = app.Resources;
        var store = new ThemePreferenceStoreStub(ApplicationTheme.Dark);
        var windowsTheme = new WindowsThemeProviderStub(
            usesLightTheme: false);

        try
        {
            app.Resources = new ResourceDictionary();
            using var manager =
                new ThemeManager(app, store, windowsTheme);

            manager.Initialize();

            Assert.Equal(ApplicationTheme.Dark, manager.CurrentTheme);
            Assert.Single(app.Resources.MergedDictionaries);
            Assert.EndsWith(
                "/Themes/DarkTheme.xaml",
                app.Resources.MergedDictionaries[0].Source?.OriginalString);
            Assert.Equal(4, manager.AvailableThemes.Count);
            Assert.Null(store.SavedTheme);

            manager.ApplyTheme(ApplicationTheme.Sepia);

            Assert.Equal(ApplicationTheme.Sepia, manager.CurrentTheme);
            Assert.Single(app.Resources.MergedDictionaries);
            Assert.EndsWith(
                "/Themes/SepiaTheme.xaml",
                app.Resources.MergedDictionaries[0].Source?.OriginalString);
            Assert.Equal(ApplicationTheme.Sepia, store.SavedTheme);
            Assert.NotNull(app.Resources["CurrentWindowBackground"]);
            Assert.NotNull(app.Resources["CurrentAccent"]);
        }
        finally
        {
            app.Resources = previousResources;
        }
    }

    [WpfFact]
    public void SystemTheme_UsesWindowsApplicationMode()
    {
        Application app = Application.Current ?? new Application();
        var previousResources = app.Resources;
        var store = new ThemePreferenceStoreStub(ApplicationTheme.System);
        var windowsTheme = new WindowsThemeProviderStub(
            usesLightTheme: false);

        try
        {
            app.Resources = new ResourceDictionary();
            using var manager =
                new ThemeManager(app, store, windowsTheme);

            manager.Initialize();

            Assert.Equal(ApplicationTheme.System, manager.CurrentTheme);
            Assert.EndsWith(
                "/Themes/DarkTheme.xaml",
                app.Resources.MergedDictionaries[0].Source?.OriginalString);

            windowsTheme.SetUsesLightTheme(true);
            manager.Initialize();

            Assert.Equal(ApplicationTheme.System, manager.CurrentTheme);
            Assert.EndsWith(
                "/Themes/LightTheme.xaml",
                app.Resources.MergedDictionaries[0].Source?.OriginalString);
            Assert.Null(store.SavedTheme);
        }
        finally
        {
            app.Resources = previousResources;
        }
    }

    [Fact]
    public void SettingsModel_AppliesSelectedThemeAndClosesThroughManager()
    {
        var themeManager = new ThemeManagerStub(ApplicationTheme.Light);
        var windowManager = new WindowManagerStub();
        var model = new SettingsModel(themeManager, windowManager);

        ApplicationThemeOption dark = Assert.Single(
            model.Themes,
            option => option.Theme == ApplicationTheme.Dark);
        model.SelectedTheme = dark;

        Assert.Equal(ApplicationTheme.Dark, themeManager.AppliedTheme);

        model.Close();

        Assert.Equal(model.WindowId, windowManager.ClosedWindowId);
    }

    [Fact]
    public void SettingsModel_PersistsNavigationPaneWidthOnClosing()
    {
        double originalWidth =
            Properties.Settings.Default.SettingsNavigationPaneWidth;

        try
        {
            var model = new SettingsModel(
                new ThemeManagerStub(ApplicationTheme.Light),
                new WindowManagerStub())
            {
                NavigationPaneWidth = new GridLength(275)
            };

            model.Closing();

            Assert.Equal(
                275,
                Properties.Settings.Default.SettingsNavigationPaneWidth);
        }
        finally
        {
            Properties.Settings.Default.SettingsNavigationPaneWidth =
                originalWidth;
            Properties.Settings.Default.Save();
        }
    }

    [Fact]
    public async Task SettingsModel_OpenSearchResult_UsesWorkspaceOpener()
    {
        var windowManager = new WindowManagerStub();
        var fileOpenService = new WorkspaceFileOpenServiceStub(
            WorkspaceFileOpenResult.InternalSuccess());
        var model = new SettingsModel(
            new ThemeManagerStub(ApplicationTheme.Light),
            windowManager,
            null,
            null,
            null,
            fileOpenService);
        var result = new WorkspaceSearchResult(
            "secret.cbook",
            @"C:\Workspace\secret.cbook",
            "secret.cbook");

        await model.OpenSearchResultAsync(result);

        Assert.Equal(result.FullPath, fileOpenService.OpenedPath);
        Assert.Equal(model.WindowId, windowManager.ClosedWindowId);
    }

    [WpfFact]
    public void Startup_ResolvesSettingsModelWithWorkspaceFileOpener()
    {
        var app = Application.Current ?? new Application();
        using IContainer container = new Startup().ConfigureServices(app);
        using ILifetimeScope scope = container.BeginLifetimeScope();

        ISettingsModel model = scope.Resolve<ISettingsModel>();

        Assert.NotNull(model);
        Assert.NotNull(scope.Resolve<IWorkspaceFileOpenService>());
    }

    [Fact]
    public void SettingsWindowService_ReusesOpenWindow()
    {
        var windowManager = new WindowManagerStub();
        var service = new SettingsWindowService(windowManager);

        service.Open();
        service.Open();

        Assert.Equal(1, windowManager.CreateCount);
        Assert.Equal(1, windowManager.ShowCount);
        Assert.Equal(1, windowManager.ActivateCount);
        Assert.Equal(typeof(SettingsWindow), windowManager.CreatedWindowType);
    }

    private sealed class ThemePreferenceStoreStub:
        IThemePreferenceStore
    {
        private readonly ApplicationTheme storedTheme;

        public ThemePreferenceStoreStub(ApplicationTheme storedTheme)
        {
            this.storedTheme = storedTheme;
        }

        public ApplicationTheme? SavedTheme { get; private set; }

        public ApplicationTheme Load() => storedTheme;

        public void Save(ApplicationTheme theme) =>
            SavedTheme = theme;
    }

    private static ResourceDictionary LoadDictionary(string source) =>
        new()
        {
            Source = new Uri(source, UriKind.Relative)
        };

    private static void AssertContrast(
        ResourceDictionary theme,
        string foregroundKey,
        string backgroundKey)
    {
        Color foreground =
            Assert.IsType<SolidColorBrush>(theme[foregroundKey]).Color;
        Color background =
            Assert.IsType<SolidColorBrush>(theme[backgroundKey]).Color;
        double ratio = ContrastRatio(foreground, background);

        Assert.True(
            ratio >= 4.5,
            $"{foregroundKey}/{backgroundKey}: {ratio:F2}:1");
    }

    private static double ContrastRatio(Color first, Color second)
    {
        double firstLuminance = RelativeLuminance(first);
        double secondLuminance = RelativeLuminance(second);
        double light = Math.Max(firstLuminance, secondLuminance);
        double dark = Math.Min(firstLuminance, secondLuminance);
        return (light + 0.05) / (dark + 0.05);
    }

    private static double RelativeLuminance(Color color) =>
        0.2126 * Linearize(color.R / 255d) +
        0.7152 * Linearize(color.G / 255d) +
        0.0722 * Linearize(color.B / 255d);

    private static double Linearize(double channel) =>
        channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private sealed class ThemeManagerStub: IThemeManager
    {
        public ThemeManagerStub(ApplicationTheme currentTheme)
        {
            CurrentTheme = currentTheme;
        }

        public IReadOnlyList<ApplicationThemeOption> AvailableThemes { get; } =
        [
            new(
                ApplicationTheme.System,
                "Системная",
                string.Empty,
                "SystemTheme",
                "#FFFFFFFF",
                "#FF000000"),
            new(
                ApplicationTheme.Light,
                "Светлая",
                string.Empty,
                "LightTheme",
                "#FFFFFFFF",
                "#FF000000"),
            new(
                ApplicationTheme.Dark,
                "Тёмная",
                string.Empty,
                "DarkTheme",
                "#FF000000",
                "#FFFFFFFF"),
            new(
                ApplicationTheme.Sepia,
                "Сепия",
                string.Empty,
                "SepiaTheme",
                "#FFF2E8D5",
                "#FFA05A2C")
        ];

        public ApplicationTheme CurrentTheme { get; private set; }
        public ApplicationTheme? AppliedTheme { get; private set; }

        public void Initialize()
        {
        }

        public void ApplyTheme(ApplicationTheme theme)
        {
            AppliedTheme = theme;
            CurrentTheme = theme;
        }
    }

    private sealed class WindowsThemeProviderStub:
        IWindowsThemeProvider
    {
        public WindowsThemeProviderStub(bool usesLightTheme)
        {
            UsesLightTheme = usesLightTheme;
        }

        public bool UsesLightTheme { get; private set; }

        public event EventHandler? ThemeChanged;

        public void SetUsesLightTheme(bool usesLightTheme)
        {
            UsesLightTheme = usesLightTheme;
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class WorkspaceFileOpenServiceStub:
        IWorkspaceFileOpenService
    {
        private readonly WorkspaceFileOpenResult result;

        public WorkspaceFileOpenServiceStub(WorkspaceFileOpenResult result)
        {
            this.result = result;
        }

        public string? OpenedPath { get; private set; }

        public Task<WorkspaceFileOpenResult> OpenAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            OpenedPath = filePath;
            return Task.FromResult(result);
        }
    }

    private sealed class WindowManagerStub: IWindowManager
    {
        public Guid? ClosedWindowId { get; private set; }
        public int CreateCount { get; private set; }
        public int ShowCount { get; private set; }
        public int ActivateCount { get; private set; }
        public Type? CreatedWindowType { get; private set; }
        private Guid? openWindowId;

        public Guid CreateWindow<T>(
            IReadOnlyDictionary<string, object?>? args = null)
            where T: Window
        {
            CreateCount++;
            CreatedWindowType = typeof(T);
            openWindowId = Guid.NewGuid();
            return openWindowId.Value;
        }

        public TResult? GetResult<TResult>(Guid guid) => default;

        public void ShowWindow(Guid windowId)
        {
            ShowCount++;
        }

        public void ShowWindowDialog(Guid windowId)
        {
        }

        public void ActivateWindow(Guid windowId) =>
            ActivateCount++;

        public void CloseWindow(Guid windowId)
        {
            ClosedWindowId = windowId;
            if(openWindowId == windowId)
                openWindowId = null;
        }

        public bool IsWindowOpen(Guid windowId) =>
            openWindowId == windowId;

        public WindowHost? FindHostWindow(Guid windowId) => null;
    }
}
