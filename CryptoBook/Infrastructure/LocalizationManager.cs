using CryptoBook.DTO;

using System.Globalization;
namespace CryptoBook.Infrastructure
{
    /// <summary>
    /// Менеджер локализации приложения: хранит доступные языки, текущую культуру
    /// и методы для инициализации и смены культуры.
    /// </summary>
    public static class LocalizationManager
    {
        /// <summary>Имя культуры по умолчанию.</summary>
        public const string DefaultCultureName = "en-US";

        private static readonly LocalizationCatalog languageCatalog =
            LocalizationCatalog.Create(["ru", "uk"]);

        /// <summary>Событие, возникающее при смене текущей культуры.</summary>
        public static event EventHandler? CultureChanged;

        /// <summary>Список доступных локалей, которые поддерживает приложение.</summary>
        public static IReadOnlyList<ApplicationLanguageOption> AvailableLanguages =>
            languageCatalog.Languages;

        /// <summary>Текущее имя культуры (нормализованное).</summary>
        public static string CurrentCultureName => NormalizeCultureName(CultureInfo.CurrentUICulture.Name);

        /// <summary>
        /// Инициализирует текущую культуру из настроек приложения.
        /// Если в настройках указана некорректная или устаревшая культура,
        /// сохраняет нормализованное значение в настройки.
        /// </summary>
        public static void InitializeFromSettings()
        {
            string cultureName = NormalizeCultureName( Properties.Settings.Default.CultureInfo);
            ApplyCulture(cultureName);

            if(!string.Equals(Properties.Settings.Default.CultureInfo, cultureName, StringComparison.Ordinal))
            {
                Properties.Settings.Default.CultureInfo = cultureName;
                Properties.Settings.Default.Save();
            }
        }

        /// <summary>
        /// Выбирает и применяет культуру по имени. Сохраняет выбор в настройках,
        /// уведомляет о смене ресурсов и вызывает событие CultureChanged.
        /// </summary>
        /// <param name="cultureName">Имя культуры (может быть null).</param>
        public static void SelectCulture(string? cultureName)
        {
            string normalized = NormalizeCultureName(cultureName);
            Properties.Settings.Default.CultureInfo = normalized;
            Properties.Settings.Default.Save();
            ApplyCulture(normalized);
            ResourceWrapper.NotifyCultureChanged();
            CultureChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Получает локализованную строку по ключу. Если ключ не найден,
        /// возвращает сам ключ.
        /// </summary>
        /// <param name="key">Ключ ресурса.</param>
        /// <returns>Локализованная строка или ключ при отсутствии ресурса.</returns>
        public static string GetString(string key) =>
            Properties.Resources.ResourceManager.GetString(
                key,
                CultureInfo.CurrentUICulture) ?? key;

        /// <summary>
        /// Форматирует локализованную строку с переданными аргументами
        /// в соответствии с текущей культурой (например, форматы дат/чисел).
        /// </summary>
        /// <param name="key">Ключ ресурса.</param>
        /// <param name="arguments">Параметры форматирования.</param>
        /// <returns>Форматированная строка.</returns>
        public static string Format(string key, params object?[] arguments) =>
            string.Format( CultureInfo.CurrentCulture, GetString(key), arguments);

        /// <summary>
        /// Нормализует имя культуры по списку найденных локализаций.
        /// Варианты языка, для которого существует нейтральный ресурс (например,
        /// ru), приводятся к поддерживаемой приложением конкретной культуре.
        /// </summary>
        /// <param name="cultureName">Входное имя культуры (может быть null).</param>
        /// <returns>Нормализованное имя культуры.</returns>
        public static string NormalizeCultureName(string? cultureName) =>
            languageCatalog.Normalize(cultureName);

        /// <summary>
        /// Применяет культуру к текущему потоку и ресурсам приложения.
        /// </summary>
        /// <param name="cultureName">Имя культуры для применения (должно быть нормализованным).</param>
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
