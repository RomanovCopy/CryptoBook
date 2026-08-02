using CryptoBook.DTO;

using System.Globalization;

namespace CryptoBook.Infrastructure
{
    /// <summary>
    /// Каталог доступных локализаций, построенный по культурам спутниковых
    /// сборок ресурсов приложения.
    /// </summary>
    internal sealed class LocalizationCatalog
    {
        private readonly IReadOnlyDictionary<string, string> cultureAliases;

        private LocalizationCatalog(
            IReadOnlyList<ApplicationLanguageOption> languages,
            IReadOnlyDictionary<string, string> cultureAliases)
        {
            Languages = languages;
            this.cultureAliases = cultureAliases;
        }

        public IReadOnlyList<ApplicationLanguageOption> Languages { get; }

        public static LocalizationCatalog Create(
            IEnumerable<string> resourceCultureNames)
        {
            ArgumentNullException.ThrowIfNull(resourceCultureNames);

            var languages = new Dictionary<string, ApplicationLanguageOption>(
                StringComparer.OrdinalIgnoreCase);
            var aliases = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            AddLanguage(
                CultureInfo.GetCultureInfo("en"),
                LocalizationManager.DefaultCultureName,
                languages,
                aliases);

            foreach(string resourceCultureName in resourceCultureNames)
            {
                if(!TryGetCulture(resourceCultureName, out CultureInfo culture))
                    continue;

                string applicationCultureName = culture.IsNeutralCulture
                    ? CultureInfo.CreateSpecificCulture(culture.Name).Name
                    : culture.Name;
                AddLanguage(
                    culture,
                    applicationCultureName,
                    languages,
                    aliases);
            }

            ApplicationLanguageOption defaultLanguage =
                languages[LocalizationManager.DefaultCultureName];
            ApplicationLanguageOption[] orderedLanguages =
                languages.Values
                    .Where(language => !string.Equals(
                        language.CultureName,
                        LocalizationManager.DefaultCultureName,
                        StringComparison.OrdinalIgnoreCase))
                    .OrderBy(
                        language => language.DisplayName,
                        StringComparer.OrdinalIgnoreCase)
                    .Prepend(defaultLanguage)
                    .ToArray();

            return new LocalizationCatalog(orderedLanguages, aliases);
        }

        public string Normalize(string? cultureName)
        {
            if(!TryGetCulture(cultureName, out CultureInfo culture))
                return LocalizationManager.DefaultCultureName;

            for(CultureInfo current = culture;
                !string.IsNullOrEmpty(current.Name);
                current = current.Parent)
            {
                if(cultureAliases.TryGetValue(
                    current.Name,
                    out string? supportedCultureName))
                {
                    return supportedCultureName;
                }
            }

            return LocalizationManager.DefaultCultureName;
        }

        private static void AddLanguage(
            CultureInfo resourceCulture,
            string applicationCultureName,
            IDictionary<string, ApplicationLanguageOption> languages,
            IDictionary<string, string> aliases)
        {
            if(!languages.ContainsKey(applicationCultureName))
            {
                languages.Add(
                    applicationCultureName,
                    new ApplicationLanguageOption(
                        applicationCultureName,
                        GetDisplayName(resourceCulture)));
            }

            aliases[resourceCulture.Name] = applicationCultureName;
            aliases[applicationCultureName] = applicationCultureName;
        }

        private static string GetDisplayName(CultureInfo culture)
        {
            string displayName = culture.NativeName;
            return culture.TextInfo.ToTitleCase(displayName);
        }

        private static bool TryGetCulture(
            string? cultureName,
            out CultureInfo culture)
        {
            culture = null!;
            if(string.IsNullOrWhiteSpace(cultureName))
                return false;

            try
            {
                culture = CultureInfo.GetCultureInfo(cultureName);
                return !string.IsNullOrEmpty(culture.Name);
            }
            catch(CultureNotFoundException)
            {
                return false;
            }
        }
    }
}
