using Autofac;

using CryptoBook.Injections;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.ComponentModel;

namespace CryptoBook.DTO
{
    public sealed class WindowHost: IDisposable
    {
        public WindowHost(Guid id, ILifetimeScope scope, Window window)
        {
            Key = id;
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            Window = window ?? throw new ArgumentNullException(nameof(window));

            Window.Closing += OnClosing;
            Window.Closed += OnClosed;
        }

        public Guid Key { get; }
        public ILifetimeScope Scope { get; }
        public Window Window { get; }

        private CancelEventArgs? closingArgs;
        public bool IsClosing => IsClosed || closingArgs is { Cancel: false };
        public bool IsClosed { get; private set; }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            // Поздние обработчики Closing тоже могут отменить закрытие.
            closingArgs = e;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            IsClosed = true;
            Window.Closing -= OnClosing;
            Window.Closed -= OnClosed;

            // Сначала завершается внутренняя фаза закрытия WPF. Затем разрываются
            // привязки к модели и Flyleaf, и только после этого уничтожается scope.
            Window.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() =>
                {
                    Window.DataContext = null;
                    DiScope.SetScope(Window, null);
                    Dispose();
                }));
        }

        public void Dispose()
        {
            if(ScopeDisposing)
                return;

            ScopeDisposing = true;
            Scope.Dispose();
        }

        private bool ScopeDisposing { get; set; }
    }
}
