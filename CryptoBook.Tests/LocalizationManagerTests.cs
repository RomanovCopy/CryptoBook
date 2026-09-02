using CryptoBook.Infrastructure;

using System.Collections;
using System.Globalization;
using System.Resources;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class LocalizationManagerTests
    {
        [Theory]
        [InlineData(null, "en-US")]
        [InlineData("", "en-US")]
        [InlineData("de-DE", "en-US")]
        [InlineData("en-GB", "en-US")]
        [InlineData("ru", "ru-RU")]
        [InlineData("ru-RU", "ru-RU")]
        [InlineData("uk", "uk-UA")]
        [InlineData("uk-UA", "uk-UA")]
        public void NormalizeCultureName_OnlyAllowsSupportedLanguages(
            string? value,
            string expected)
        {
            Assert.Equal(
                expected,
                LocalizationManager.NormalizeCultureName(value));
        }

        [Fact]
        public void GetString_UsesEnglishNeutralResourcesAndLocalizedSatellites()
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                Assert.Equal(
                    "Language",
                    LocalizationManager.GetString("Settings.Language"));

                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
                Assert.Equal(
                    "Язык",
                    LocalizationManager.GetString("Settings.Language"));

                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("uk-UA");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("uk-UA");
                Assert.Equal(
                    "Мова",
                    LocalizationManager.GetString("Settings.Language"));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        }

        [Fact]
        public void AvailableLanguages_DefaultsToEnglish()
        {
            Assert.Equal(
                LocalizationManager.DefaultCultureName,
                LocalizationManager.AvailableLanguages[0].CultureName);
            Assert.Contains(
                LocalizationManager.AvailableLanguages,
                language => language.CultureName == "ru-RU");
            Assert.Contains(
                LocalizationManager.AvailableLanguages,
                language => language.CultureName == "uk-UA");
            Assert.All(
                LocalizationManager.AvailableLanguages,
                language => Assert.False(
                    string.IsNullOrWhiteSpace(language.DisplayName)));
        }

        [Fact]
        public void Catalog_DiscoversAdditionalSatelliteLanguages()
        {
            LocalizationCatalog catalog = LocalizationCatalog.Create(
                ["ru", "uk", "de-DE", "not-a-culture"]);

            Assert.Equal(
                LocalizationManager.DefaultCultureName,
                catalog.Languages[0].CultureName);
            Assert.Contains(
                catalog.Languages,
                language => language.CultureName == "ru-RU");
            Assert.Contains(
                catalog.Languages,
                language => language.CultureName == "de-DE");
            Assert.Contains(
                catalog.Languages,
                language => language.CultureName == "uk-UA");
            Assert.Equal("ru-RU", catalog.Normalize("ru-UA"));
            Assert.Equal("uk-UA", catalog.Normalize("uk-PL"));
            Assert.Equal("de-DE", catalog.Normalize("de-DE"));
            Assert.Equal(
                LocalizationManager.DefaultCultureName,
                catalog.Normalize("de-AT"));
        }

        [Fact]
        public void LocalizedResources_HaveIdenticalNonEmptyKeySets()
        {
            ResourceManager manager =
                CryptoBook.Properties.Resources.ResourceManager;
            ResourceSet neutral = Assert.IsAssignableFrom<ResourceSet>(
                manager.GetResourceSet(
                    CultureInfo.InvariantCulture,
                    createIfNotExists: true,
                    tryParents: false));
            ResourceSet russian = Assert.IsAssignableFrom<ResourceSet>(
                manager.GetResourceSet(
                    CultureInfo.GetCultureInfo("ru"),
                    createIfNotExists: true,
                    tryParents: false));
            ResourceSet ukrainian = Assert.IsAssignableFrom<ResourceSet>(
                manager.GetResourceSet(
                    CultureInfo.GetCultureInfo("uk"),
                    createIfNotExists: true,
                    tryParents: false));

            Dictionary<string, string> neutralValues = ReadStrings(neutral);
            Dictionary<string, string> russianValues = ReadStrings(russian);
            Dictionary<string, string> ukrainianValues = ReadStrings(ukrainian);

            Assert.NotEmpty(neutralValues);
            Assert.Equal(
                neutralValues.Keys.OrderBy(key => key),
                russianValues.Keys.OrderBy(key => key));
            Assert.Equal(
                neutralValues.Keys.OrderBy(key => key),
                ukrainianValues.Keys.OrderBy(key => key));
            Assert.All(
                neutralValues,
                pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value)));
            Assert.All(
                russianValues,
                pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value)));
            Assert.All(
                ukrainianValues,
                pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value)));
        }

        [Fact]
        public void GetString_MissingKey_ReturnsKeyForObservableFallback()
        {
            const string missingKey = "Localization.Tests.Missing";

            Assert.Equal(missingKey, LocalizationManager.GetString(missingKey));
        }

        private static Dictionary<string, string> ReadStrings(
            ResourceSet resourceSet) =>
            resourceSet
                .Cast<DictionaryEntry>()
                .ToDictionary(
                    entry => Assert.IsType<string>(entry.Key),
                    entry => Assert.IsType<string>(entry.Value),
                    StringComparer.Ordinal);
    }
}
