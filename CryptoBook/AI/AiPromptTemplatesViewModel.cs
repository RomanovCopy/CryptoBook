using CryptoBook.Infrastructure;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CryptoBook.AI;

public sealed class AiPromptTemplateViewModel(AiPromptTemplate template) : ViewModelBase
{
    private string name = template.Name, systemPrompt = template.SystemPrompt;
    public string Id { get; } = template.Id;
    public string Name { get => name; set => SetProperty(ref name, value); }
    public string SystemPrompt { get => systemPrompt; set => SetProperty(ref systemPrompt, value); }
    internal AiPromptTemplate ToTemplate() => new() { Id = Id, Name = Name.Trim(), SystemPrompt = SystemPrompt };
}

/// <summary>A detached settings draft: changing or removing a template never saves implicitly.</summary>
public sealed class AiPromptTemplatesViewModel : ViewModelBase
{
    private AiPromptTemplateViewModel? selectedTemplate;

    public AiPromptTemplatesViewModel(AiConfiguration configuration)
    {
        foreach(var template in configuration.PromptTemplates) Items.Add(new(template));
        selectedTemplate = Items.FirstOrDefault(template => template.Id == configuration.SelectedPromptTemplateId) ?? Items.FirstOrDefault();
        Add = new RelayCommand(_ => AddTemplate());
        Remove = new RelayCommand(_ => RemoveTemplate(), _ => SelectedTemplate is not null && Items.Count > 1);
    }

    public ObservableCollection<AiPromptTemplateViewModel> Items { get; } = [];
    public AiPromptTemplateViewModel? SelectedTemplate
    {
        get => selectedTemplate;
        set
        {
            if(value is null || !Items.Contains(value) || !SetProperty(ref selectedTemplate, value)) return;
            (Remove as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }
    public ICommand Add { get; }
    public ICommand Remove { get; }

    private void AddTemplate()
    {
        string name = LocalizationManager.GetString("AI.NewTemplate"), uniqueName = name;
        for(int suffix = 2; Items.Any(template => string.Equals(template.Name.Trim(), uniqueName, StringComparison.OrdinalIgnoreCase)); suffix++)
            uniqueName = name + " (" + suffix + ")";
        var template = new AiPromptTemplateViewModel(new() { Name = uniqueName });
        Items.Add(template);
        SelectedTemplate = template;
    }

    private void RemoveTemplate()
    {
        if(SelectedTemplate is null || Items.Count <= 1) return;
        var removed = SelectedTemplate;
        SelectedTemplate = Items.First(template => !ReferenceEquals(template, removed));
        Items.Remove(removed);
        (Remove as RelayCommand)?.RaiseCanExecuteChanged();
    }

    internal List<AiPromptTemplate> ValidatedTemplates()
    {
        if(Items.Count == 0 || SelectedTemplate is null || Items.Any(template => string.IsNullOrWhiteSpace(template.Name)))
            throw new AiException("AI.TemplateNameRequired");
        if(Items.Select(template => template.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Items.Count)
            throw new AiException("AI.TemplateNameDuplicate");
        return Items.Select(template => template.ToTemplate()).ToList();
    }
}
