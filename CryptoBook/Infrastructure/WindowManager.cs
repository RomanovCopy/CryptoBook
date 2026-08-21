using Autofac;

using CryptoBook.Styles;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.Views;

using System.Windows;
using System.Windows.Interop;

using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using CryptoBook.DTO;
using CryptoBook.Injections;

namespace CryptoBook.Infrastructure
{
    public class WindowManager: IWindowManager, IDisposable
    {
        private readonly ILifetimeScope _root;
        private readonly Dictionary<Guid, WindowHost> _windowHosts;
        private readonly Dictionary<Guid, IDialogResult> _results;

        static Window? GetOwner()
        {
            var windows = System.Windows.Application.Current.Windows;
            if(windows.Count > 0)
            {
                foreach(Window vin in windows)
                {
                    if(vin.IsActive)
                        return vin;
                }
            }
            return null;
        }

        public TResult? GetResult<TResult>(Guid guid)
        {
            if(_results.ContainsKey(guid))
            {
                if(_results.TryGetValue(guid, out var tresult) && tresult is IDialogResult<TResult> result)
                {
                    _results.Remove(guid);
                    return result.Result;
                }
            }
            return default;
        }

        public WindowManager(ILifetimeScope scope)
        {
            _root = scope;
            _windowHosts = [];
            _results = [];
        }

        public Guid CreateWindow<T>(IReadOnlyDictionary<string, object?>? args = null)
            where T: Window => CreateWindow<T>(args, GetOwner());

        public Guid CreateSiblingWindow<T>(
            IReadOnlyDictionary<string, object?>? args = null)
            where T: Window
        {
            Window? activeWindow = GetOwner();
            return CreateWindow<T>(args, activeWindow?.Owner ?? activeWindow);
        }

        private Guid CreateWindow<T>(
            IReadOnlyDictionary<string, object?>? args,
            Window? owner)
            where T: Window
        {
            var scope = _root.BeginLifetimeScope(b =>
            {
                b.RegisterInstance<IWindowContext>(new WindowContext(args ?? new Dictionary<string, object?>()))
                 .As<IWindowContext>().SingleInstance();
            });

            T window;
            try
            {
                using(AmbientScope.Push(scope))
                {
                    window = scope.Resolve<T>();
                    DiScope.SetScope(window, scope);
                    window.Owner = owner;
                }
            } catch
            {
                scope.Dispose();
                throw;
            }


            var host = RegisterWindow(scope, window) ?? throw new InvalidOperationException("Failed to register window");

            window.Closed += (_, __) =>
            {
                if(window.DataContext is IDialogResult dialogResult)
                {
                    if(dialogResult is not IConditionalDialogResult conditional ||
                       conditional.HasResult)
                    {
                        _results[host.Key] = dialogResult;
                    }
                }
                UnregisterWindow(host);
                FocusWindowIfAvailable(window.Owner);
                window = null;
            };

            return host.Key;
        }


        public void ShowWindow(Guid windowId)
        {
            var winHost = FindHostWindow(windowId);
            if(winHost is null || winHost.IsClosing || winHost.IsClosed)
                return;

            if(!winHost.Window.IsVisible)
            {
                if(winHost.Window is MediaPlayer mediaPlayer)
                    PlaceMediaPlayerWindow(mediaPlayer);
                winHost.Window.Show();
            }
        }

        public void ShowWindowDialog(Guid windowId)
        {
            var winHost = FindHostWindow(windowId);
            if(winHost is null || winHost.IsClosing || winHost.IsClosed)
                return;

            if(!winHost.Window.IsVisible)
                winHost.Window.ShowDialog();
        }

        public void ActivateWindow(Guid windowId)
        {
            var winHost = FindHostWindow(windowId);
            if(winHost is null || winHost.IsClosing || winHost.IsClosed)
                return;

            if(!winHost.Window.IsVisible)
                winHost.Window.Show();
            if(winHost.Window.WindowState == WindowState.Minimized)
                winHost.Window.WindowState = WindowState.Normal;

            winHost.Window.Activate();
            winHost.Window.Focus();
        }

        public void CloseWindow(Guid windowId)
        {
            var winHost = FindHostWindow(windowId);
            if(winHost is null || winHost.IsClosing || winHost.IsClosed)
                return;

            WinClose(winHost);
        }

        public bool IsWindowOpen(Guid windowId)
        {
            var host = FindHostWindow(windowId);
            return host is { IsClosing: false, IsClosed: false };
        }

        public WindowHost? FindHostWindow(Guid windowId)
        {
            return _windowHosts.ContainsKey(windowId) ? _windowHosts[windowId] : null;
        }

        private WindowHost? RegisterWindow<T>(ILifetimeScope scope, T window) where T : Window
        {
            if(window.DataContext is IWindowWithId withId)
            {
                var host = new WindowHost(withId.WindowId, scope, window);
                _windowHosts[host.Key] = host;
                return host;
            } else
                throw new InvalidOperationException("Window's DataContext must implement IWindowWithId");
        }

        private void UnregisterWindow(WindowHost windowHost)
        {
            if(windowHost is null)
                return;
            if(_windowHosts.ContainsKey(windowHost.Key))
                _windowHosts.Remove(windowHost.Key);

            (windowHost.Window.Parent as Window)?.Focus();
        }

        private void WinClose(WindowHost windowHost)
        {
            if(windowHost.IsClosing || windowHost.IsClosed)
                return;

            FocusWindowIfAvailable(windowHost.Window.Owner);
            windowHost.Window.Close();
        }

        private static void FocusWindowIfAvailable(Window? window)
        {
            if(window is { IsLoaded: true, IsVisible: true })
                window.Focus();
        }

        private void PlaceMediaPlayerWindow(MediaPlayer window)
        {
            MediaPlayer[] visiblePlayers = _windowHosts.Values
                .Select(host => host.Window)
                .OfType<MediaPlayer>()
                .Where(player =>
                    !ReferenceEquals(player, window) &&
                    player.IsVisible)
                .ToArray();
            if(visiblePlayers.Length == 0)
                return;

            MediaPlayer anchor = visiblePlayers.FirstOrDefault(player =>
                    ReferenceEquals(player, window.Owner)) ??
                visiblePlayers.FirstOrDefault(player => player.IsActive) ??
                visiblePlayers[^1];
            Rect anchorBounds = GetWindowBounds(anchor);
            Rect workArea = GetWorkArea(anchor);
            var windowSize = new Size(
                GetWindowWidth(window),
                GetWindowHeight(window));
            Point placement = WindowLayoutDefaults.CreateMediaPlayerCascade(
                workArea,
                anchorBounds,
                windowSize,
                visiblePlayers.Select(GetWindowBounds).ToArray());

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = placement.X;
            window.Top = placement.Y;
        }

        private static Rect GetWindowBounds(Window window)
        {
            if(window.WindowState != WindowState.Normal &&
               !window.RestoreBounds.IsEmpty)
            {
                return window.RestoreBounds;
            }

            return new Rect(
                window.Left,
                window.Top,
                GetWindowWidth(window),
                GetWindowHeight(window));
        }

        private static double GetWindowWidth(Window window) =>
            IsFinitePositive(window.ActualWidth)
                ? window.ActualWidth
                : IsFinitePositive(window.Width)
                    ? window.Width
                    : window.MinWidth;

        private static double GetWindowHeight(Window window) =>
            IsFinitePositive(window.ActualHeight)
                ? window.ActualHeight
                : IsFinitePositive(window.Height)
                    ? window.Height
                    : window.MinHeight;

        private static Rect GetWorkArea(Window window)
        {
            try
            {
                var screen = System.Windows.Forms.Screen.FromHandle(
                    new WindowInteropHelper(window).Handle);
                var dpi = VisualTreeHelper.GetDpi(window);
                return new Rect(
                    screen.WorkingArea.Left / dpi.DpiScaleX,
                    screen.WorkingArea.Top / dpi.DpiScaleY,
                    screen.WorkingArea.Width / dpi.DpiScaleX,
                    screen.WorkingArea.Height / dpi.DpiScaleY);
            }
            catch(InvalidOperationException)
            {
                return SystemParameters.WorkArea;
            }
        }

        private static bool IsFinitePositive(double value) =>
            value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);

        public void Dispose()
        {
            // Корневым scope владеет App. WindowManager использует его только
            // как фабрику дочерних scope и не должен уничтожать контейнер повторно.
            _windowHosts.Clear();
            _results.Clear();
        }

    }

}
