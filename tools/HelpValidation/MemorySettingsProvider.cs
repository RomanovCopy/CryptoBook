using System.Collections.Specialized;
using System.Configuration;

namespace CryptoBook.Validation;

/// <summary>No access to LocalFileSettingsProvider, even during Save/Reload.</summary>
public sealed class MemorySettingsProvider : SettingsProvider
{
    private readonly Dictionary<string, object?> values = [];
    public override string ApplicationName { get; set; } = "HelpValidation";
    public int SaveCount { get; private set; }
    public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection properties)
    {
        var result = new SettingsPropertyValueCollection();
        foreach (SettingsProperty property in properties)
            result.Add(new SettingsPropertyValue(property)
            {
                SerializedValue = values.TryGetValue(property.Name, out object? value) ? value : property.DefaultValue,
                IsDirty = false
            });
        return result;
    }
    public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection properties)
    {
        SaveCount++;
        foreach (SettingsPropertyValue property in properties) values[property.Name] = property.SerializedValue;
    }
    public static MemorySettingsProvider Install()
    {
        var settings = Properties.Settings.Default;
        var provider = new MemorySettingsProvider();
        provider.Initialize("HelpValidation", new NameValueCollection());
        foreach (SettingsProperty property in settings.Properties) property.Provider = provider;
        settings.Providers.Clear(); settings.Providers.Add(provider); settings.Reload();
        if (settings.Properties.Cast<SettingsProperty>().Any(p => !ReferenceEquals(p.Provider, provider)))
            throw new InvalidOperationException("Settings isolation failed.");
        return provider;
    }
}
