using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    public class WebViewService: WebView2, IWebViewService
    {
        // События
        public new event EventHandler<string>? NavigationCompleted;
        public event EventHandler<string>? NavigationFailed;
        public new event EventHandler? Initialized;
        public new event EventHandler<string>? Loaded;

        // Приватный TaskCompletionSource для ожидания инициализации
        private TaskCompletionSource<bool>? _initTcs;

        public WebViewService()
        {

        }

        // Свойства
        public Uri CurrentSource { get => this.Source; set => this.Source = value; }
        public string CurrentUrl => this.Source?.ToString() ?? string.Empty;
        public new bool IsInitialized { get; private set; } = false;


        // Инициализация WebView2
        public async Task InitializeAsync(string? initialUrl = null)
        {
            if(this.CoreWebView2 == null)
            {
                _initTcs = new TaskCompletionSource<bool>();

                await this.EnsureCoreWebView2Async();

                if(this.CoreWebView2 == null)
                {
                    _initTcs.SetException(new InvalidOperationException("Failed to initialize WebView2."));
                    return;
                }

                // Подписка на события WebView2
                this.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                this.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                this.NavigationFailed += CoreWebView2_NavigationFailed;

                IsInitialized = true;
                Initialized?.Invoke(this, EventArgs.Empty);

                // Завершаем TaskCompletionSource
                _initTcs.SetResult(true);

                if(!string.IsNullOrEmpty(initialUrl))
                    this.Source = new Uri(initialUrl);
            }
        }


        // Навигация
        public void NavigateTo(string url)
        {
            if(Uri.TryCreate(url, UriKind.Absolute, out var uri))
                this.Source = uri;
        }

        // Выполнение JS
        public new async Task<string> ExecuteScriptAsync(string script)
        {
            await WaitUntilInitializedAsync();

            if(this.CoreWebView2 != null)
                return await this.CoreWebView2.ExecuteScriptAsync(script);

            return string.Empty;
        }

        // Ждать инициализации
        public async Task WaitUntilInitializedAsync()
        {
            if(IsInitialized)
                return;

            if(_initTcs != null)
                await _initTcs.Task;
        }

        // Перезагрузка страницы с новым URL
        public async Task ReloadWithUriAsinc(string url)
        {
            await WaitUntilInitializedAsync();

            if(Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                // Можно очистить кэш при необходимости:
                await ClearCacheAsync();

                this.Source = uri;
            }
        }

        // Очистка кэша
        public async Task ClearCacheAsync()
        {
            await WaitUntilInitializedAsync();

            if(this.CoreWebView2?.Profile != null)
            {
                // Очистка всего кэша и данных браузера
                await this.CoreWebView2.Profile.ClearBrowsingDataAsync(
                    CoreWebView2BrowsingDataKinds.AllProfile);
            }
        }

        // События WebView2
        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if(e.IsSuccess)
                NavigationCompleted?.Invoke(this, this.CurrentUrl);
            else
                NavigationFailed?.Invoke(this, $"Error Code: {e.WebErrorStatus}");
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // Можно добавить логику "загрузка началась"
        }

        private void CoreWebView2_NavigationFailed(object? sender, string e)
        {
            NavigationFailed?.Invoke(this, $"Navigation failed: {e}");
        }

        // Настройка по умолчанию
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            this.DefaultBackgroundColor = System.Drawing.Color.White;
        }



    }
}
