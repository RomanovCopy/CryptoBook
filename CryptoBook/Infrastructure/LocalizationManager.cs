using CryptoBook.DTO;

using System.Globalization;

namespace CryptoBook.Infrastructure
{
    public static class LocalizationManager
    {
        public const string DefaultCultureName = "en-US";
        public static event EventHandler? CultureChanged;

        public static IReadOnlyList<ApplicationLanguageOption> AvailableLanguages { get; } =
        [
            new(DefaultCultureName, "English"),
            new("ru-RU", "Русский")
        ];

        public static string CurrentCultureName =>
            NormalizeCultureName(CultureInfo.CurrentUICulture.Name);

        public static void InitializeFromSettings()
        {
            string cultureName = NormalizeCultureName(
                Properties.Settings.Default.CultureInfo);
            ApplyCulture(cultureName);

            if(!string.Equals(
                Properties.Settings.Default.CultureInfo,
                cultureName,
                StringComparison.Ordinal))
            {
                Properties.Settings.Default.CultureInfo = cultureName;
                Properties.Settings.Default.Save();
            }
        }

        public static void SelectCulture(string? cultureName)
        {
            string normalized = NormalizeCultureName(cultureName);
            Properties.Settings.Default.CultureInfo = normalized;
            Properties.Settings.Default.Save();
            ApplyCulture(normalized);
            ResourceWrapper.NotifyCultureChanged();
            CultureChanged?.Invoke(null, EventArgs.Empty);
        }

        public static string GetString(string key) =>
            Properties.Resources.ResourceManager.GetString(
                key,
                CultureInfo.CurrentUICulture) ?? key;

        public static string Format(string key, params object?[] arguments) =>
            string.Format(
                CultureInfo.CurrentCulture,
                GetString(key),
                arguments);

        public static string NormalizeCultureName(string? cultureName) =>
            cultureName?.StartsWith("ru", StringComparison.OrdinalIgnoreCase) is true
                ? "ru-RU"
                : DefaultCultureName;

        private static void ApplyCulture(string cultureName)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            Properties.Resources.Culture = culture;
        }
    }
}
