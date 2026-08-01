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
        public void NormalizeCultureName_OnlyAllowsSupportedLanguages(
            string? value,
            string expected)
        {
            Assert.Equal(
                expected,
                LocalizationManager.NormalizeCultureName(value));
        }

        [Fact]
        public void GetString_UsesEnglishNeutralResourcesAndRussianSatellite()
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
        }

        [Fact]
        public void NeutralAndRussianResources_HaveIdenticalNonEmptyKeySets()
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
                    CultureInfo.GetCultureInfo("ru-RU"),
                    createIfNotExists: true,
                    tryParents: false));

            Dictionary<string, string> neutralValues = ReadStrings(neutral);
            Dictionary<string, string> russianValues = ReadStrings(russian);

            Assert.NotEmpty(neutralValues);
            Assert.Equal(
                neutralValues.Keys.OrderBy(key => key),
                russianValues.Keys.OrderBy(key => key));
            Assert.All(
                neutralValues,
                pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value)));
            Assert.All(
                russianValues,
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
