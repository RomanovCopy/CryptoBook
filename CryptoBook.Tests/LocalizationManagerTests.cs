using CryptoBook.Infrastructure;

using System.Globalization;

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
    }
}
