using CryptoBook.Infrastructure;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace CryptoBook.AI;

public sealed record AiPromptTemplate
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public string SystemPrompt { get; init; } = string.Empty;
}

internal static class AiPromptTemplates
{
    internal static string GetDisplayName(string id, string name)
    {
        string? key = id switch
        {
            "default" => "AI.TemplatePreset.Default",
            "concise" => "AI.TemplatePreset.Concise",
            "explainer" => "AI.TemplatePreset.Explainer",
            "editor" => "AI.TemplatePreset.Editor",
            "analyst" => "AI.TemplatePreset.Analyst",
            "developer" => "AI.TemplatePreset.Developer",
            _ => null
        };
        // Recognize names saved by older versions in any supported language.
        // A renamed preset or a user-created template keeps its own name.
        if(key is null || !LocalizationManager.AvailableLanguages.Any(language =>
            Properties.Resources.ResourceManager.GetString(key, CultureInfo.GetCultureInfo(language.CultureName)) == name))
            return name;
        return LocalizationManager.GetString(key);
    }

    // Embedded in the assembly so presets travel with every build and installer.
    // Deserialize a fresh copy: edits to one settings draft cannot change future defaults.
    internal static List<AiPromptTemplate> CreateDefaults()
    {
        using var stream = typeof(AiPromptTemplates).Assembly.GetManifestResourceStream("CryptoBook.AI.PromptTemplates.json")
            ?? throw new InvalidOperationException("Bundled AI conversation templates are missing.");
        return JsonSerializer.Deserialize<List<AiPromptTemplate>>(stream)
            ?? throw new InvalidOperationException("Bundled AI conversation templates are invalid.");
    }

    internal static AiConfiguration Normalize(AiConfiguration configuration)
    {
        var templates = configuration.PromptTemplates?.Where(template => template is not null &&
            !string.IsNullOrWhiteSpace(template.Id) && !string.IsNullOrWhiteSpace(template.Name))
            .DistinctBy(template => template.Id).Select(template => template with
            { SystemPrompt = template.SystemPrompt ?? string.Empty }).ToList() ?? [];
        string selectedId = configuration.SelectedPromptTemplateId;
        if(templates.Count == 0)
        {
            templates = CreateDefaults();
            // Older versions stored instructions per model. Preserve every distinct instruction,
            // and make the previously selected model's instruction the global selection.
            foreach(var model in configuration.Models.Where(model => !string.IsNullOrWhiteSpace(model.SystemPrompt)))
            {
                var template = templates.FirstOrDefault(template => template.SystemPrompt == model.SystemPrompt);
                if(template is null)
                {
                    string name = Path.GetFileNameWithoutExtension(model.ModelPath);
                    if(string.IsNullOrWhiteSpace(name)) name = LocalizationManager.GetString("AI.NewTemplate");
                    string uniqueName = name;
                    for(int suffix = 2; templates.Any(template => string.Equals(template.Name, uniqueName, StringComparison.OrdinalIgnoreCase)); suffix++)
                        uniqueName = name + " (" + suffix + ")";
                    template = new() { Name = uniqueName, SystemPrompt = model.SystemPrompt };
                    templates.Add(template);
                }
                if(string.Equals(model.ModelPath, configuration.SelectedModelPath, StringComparison.OrdinalIgnoreCase))
                    selectedId = template.Id;
            }
        }
        if(!templates.Any(template => template.Id == selectedId)) selectedId = templates[0].Id;
        return configuration with { PromptTemplates = templates, SelectedPromptTemplateId = selectedId };
    }
}
