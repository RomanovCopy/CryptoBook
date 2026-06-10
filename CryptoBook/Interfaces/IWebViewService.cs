using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IWebViewService
    {
        // События
        event EventHandler<string>? NavigationCompleted;
        event EventHandler<string>? NavigationFailed;
        event EventHandler? Initialized;
        event EventHandler<string>? Loaded;

        // Свойства
        Uri CurrentSource { get; set; }
        string CurrentUrl { get; }
        bool IsInitialized { get; }

        // Методы
        Task InitializeAsync(string? initialUrl = null);
        void NavigateTo(string url);
        Task<string> ExecuteScriptAsync(string script);
        Task WaitUntilInitializedAsync();
        // Перезагрузка страницы с новым URL
        Task ReloadWithUriAsinc(string url);
        //Очистка кэша
        Task ClearCacheAsync();
    }
}
