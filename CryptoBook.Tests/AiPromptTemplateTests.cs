using CryptoBook.AI;
using CryptoBook.Infrastructure;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace CryptoBook.Tests;

public sealed partial class AiIntegrationTests
{
    [Fact]
    public void Templates_FirstRunLoadsAllBundledNamesAndMultilineInstructions()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        var store = new AiSettingsStore(Path.Combine(directory, "settings.json"));
        Assert.False(Directory.Exists(directory));
        Assert.Equal(new[] { "Default", "Concise answers", "Step-by-step explanations", "Text editor", "Analyst",
            "Programming assistant" }, store.Current.PromptTemplates.Select(template => template.Name));
        Assert.Equal(6, store.Current.PromptTemplates.Select(template => template.Id).Distinct().Count());
        Assert.All(store.Current.PromptTemplates, template =>
        {
            Assert.False(string.IsNullOrWhiteSpace(template.SystemPrompt));
            Assert.Contains("\r\n", template.SystemPrompt);
        });
        Assert.Equal("default", store.Current.SelectedPromptTemplateId);
        Assert.StartsWith("# Role\r\n", store.Current.SelectedModel.SystemPrompt);
    }

    [Theory]
    [InlineData("en-US", new[] { "Default", "Concise answers", "Step-by-step explanations", "Text editor", "Analyst", "Programming assistant" })]
    [InlineData("ru-RU", new[] { "Обычный", "Краткие ответы", "Подробные объяснения", "Редактор текста", "Аналитик", "Помощник программиста" })]
    [InlineData("uk-UA", new[] { "Звичайний", "Короткі відповіді", "Докладні пояснення", "Редактор тексту", "Аналітик", "Помічник програміста" })]
    [InlineData("de-DE", new[] { "Standard", "Kurze Antworten", "Ausführliche Erklärungen", "Textredaktion", "Analyse", "Programmierassistent" })]
    public void Templates_LocalizePresetNames_WithoutChangingSavedNamesOrInstructions(string culture, string[] names)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var defaults = AiPromptTemplates.CreateDefaults();
            var draft = new AiPromptTemplatesViewModel(new() { PromptTemplates = defaults });
            Assert.Equal(names, draft.Items.Select(template => template.Name));
            Assert.Equal(defaults, draft.ValidatedTemplates());
            // Recognize preinstalled names already saved in another supported language.
            for(int index = 0; index < defaults.Count; index++)
            {
                foreach(var language in LocalizationManager.AvailableLanguages)
                {
                    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language.CultureName);
                    string savedName = AiPromptTemplates.GetDisplayName(defaults[index].Id, defaults[index].Name);
                    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                    Assert.Equal(names[index], AiPromptTemplates.GetDisplayName(defaults[index].Id, savedName));
                }
            }
            draft.Items[0].Name = names[0];
            Assert.Equal(defaults[0], draft.Items[0].ToTemplate());
            draft.Items[0].Name = "My renamed preset";
            draft.Items[0].SystemPrompt = "My instruction";
            var renamed = draft.Items[0].ToTemplate();
            Assert.Equal("My renamed preset", AiPromptTemplates.GetDisplayName(renamed.Id, renamed.Name));
            Assert.Equal("My instruction", renamed.SystemPrompt);
            Assert.Equal("Обычный", AiPromptTemplates.GetDisplayName("custom", "Обычный"));
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [WpfFact]
    public async Task Templates_LanguageChangeRefreshesPanelAndEditor_PreservingSelectionAndSavedData()
    {
        CultureInfo previous = CultureInfo.CurrentCulture, previousUi = CultureInfo.CurrentUICulture;
        CultureInfo? previousDefault = CultureInfo.DefaultThreadCurrentCulture, previousDefaultUi = CultureInfo.DefaultThreadCurrentUICulture;
        string previousSetting = CryptoBook.Properties.Settings.Default.CultureInfo;
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        Window? host = null;
        try
        {
            LocalizationManager.SelectCulture("ru-RU");
            var (assistant, store, runtime, _) = CreateAssistant(directory);
            using(assistant)
            using(var settings = new AiModelSettingsViewModel(store, runtime))
            {
                var saved = store.Current;
                string savedJson = File.ReadAllText(Path.Combine(directory, "settings.json"));
                using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestAssets", "ComboBoxStyles.xaml"));
                var styles = (ResourceDictionary)XamlReader.Load(stream);
                var style = (Style)styles["DropUpComboBoxStyle"];
                var panelSelector = new ComboBox { DataContext = assistant, DisplayMemberPath = "Name", Style = style };
                panelSelector.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(assistant.PromptTemplates)));
                panelSelector.SetBinding(ComboBox.SelectedItemProperty, new Binding(nameof(assistant.SelectedPromptTemplate)) { Mode = BindingMode.TwoWay });
                var editorSelector = new ComboBox { DataContext = settings.Templates, DisplayMemberPath = "Name", Style = style };
                editorSelector.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(settings.Templates.Items)));
                editorSelector.SetBinding(ComboBox.SelectedItemProperty, new Binding(nameof(settings.Templates.SelectedTemplate)) { Mode = BindingMode.TwoWay });
                var nameBox = new TextBox { DataContext = settings.Templates };
                nameBox.SetBinding(TextBox.TextProperty, new Binding("SelectedTemplate.Name") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                host = new Window
                {
                    Content = new StackPanel { Children = { panelSelector, editorSelector, nameBox } },
                    Width = 320, Height = 200, ShowInTaskbar = false, ShowActivated = false
                };
                host.Show();
                foreach(var (culture, name) in new[] { ("en-US", "Default"), ("uk-UA", "Звичайний"), ("de-DE", "Standard"), ("ru-RU", "Обычный") })
                {
                    LocalizationManager.SelectCulture(culture);
                    await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    Assert.Equal("default", Assert.IsType<AiPromptTemplateViewModel>(panelSelector.SelectedItem).Id);
                    Assert.Equal("default", settings.Templates.SelectedTemplate!.Id);
                    host.UpdateLayout();
                    Assert.Contains(TemplateNameTextBlocks(panelSelector), text => text.Text == name);
                    Assert.Contains(TemplateNameTextBlocks(editorSelector), text => text.Text == name);
                    Assert.Equal(name, nameBox.Text);
                    Assert.Same(saved, store.Current);
                    Assert.Equal(savedJson, File.ReadAllText(Path.Combine(directory, "settings.json")));
                }
                LocalizationManager.SelectCulture("en-US");
                settings.Save.Execute(null);
                Assert.Empty(settings.Status);
                Assert.Equal(saved.PromptTemplates, new AiSettingsStore(Path.Combine(directory, "settings.json")).Current.PromptTemplates);
                Assert.Equal(0, runtime.UnloadCount);
            }
        }
        finally
        {
            host?.Close();
            LocalizationManager.SelectCulture(previousSetting);
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
            CultureInfo.DefaultThreadCurrentCulture = previousDefault;
            CultureInfo.DefaultThreadCurrentUICulture = previousDefaultUi;
            if(Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static IEnumerable<TextBlock> TemplateNameTextBlocks(DependencyObject parent)
    {
        for(int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if(child is TextBlock text) yield return text;
            foreach(var descendant in TemplateNameTextBlocks(child)) yield return descendant;
        }
    }

    [Fact]
    public void Templates_BundledDefaultsDoNotOverwriteSavedEditsOrRestoreDeletedTemplates()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(directory, "settings.json");
            var store = new AiSettingsStore(path);
            var edited = store.Current.PromptTemplates[0] with { Name = "Моя инструкция", SystemPrompt = "Сохранённая правка" };
            var custom = new AiPromptTemplate { Name = "Пользовательский шаблон", SystemPrompt = "Мой промпт" };
            store.Save(store.Current with { PromptTemplates = [edited, custom], SelectedPromptTemplateId = custom.Id });
            var restored = new AiSettingsStore(path);
            Assert.Equal(new[] { edited, custom }, restored.Current.PromptTemplates);
            Assert.Equal(custom.Id, restored.Current.SelectedPromptTemplateId);
            restored.Save(restored.Current with { SelectedPromptTemplateId = edited.Id });
            Assert.Equal("Сохранённая правка", restored.Current.SelectedModel.SystemPrompt);
            var fresh = new AiSettingsStore(Path.Combine(directory, "fresh.json"));
            Assert.Equal(6, fresh.Current.PromptTemplates.Count);
            Assert.StartsWith("# Role\r\n", fresh.Current.SelectedModel.SystemPrompt);
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Templates_MigrateLegacyPrompts_AndPersistGlobalSelectionForExistingAndNewModels()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                SelectedModelPath = "second.gguf",
                Models = new[]
                {
                    new AiModelSettings { ModelPath = "first.gguf", SystemPrompt = "legacy first" },
                    new AiModelSettings { ModelPath = "second.gguf", SystemPrompt = "legacy second" },
                    new AiModelSettings { ModelPath = "third.gguf", SystemPrompt = "legacy first" }
                }
            }));
            var store = new AiSettingsStore(path);
            Assert.Equal("legacy second", store.Current.SelectedModel.SystemPrompt);
            Assert.Single(store.Current.PromptTemplates, template => template.SystemPrompt == "legacy first");
            string selectedId = store.Current.SelectedPromptTemplateId;
            foreach(string model in new[] { "first.gguf", "new-model.gguf" })
            {
                store.Save(store.Current with { SelectedModelPath = model });
                Assert.Equal("legacy second", store.Current.SelectedModel.SystemPrompt);
            }
            var restored = new AiSettingsStore(path);
            Assert.Equal(selectedId, restored.Current.SelectedPromptTemplateId);
            Assert.Equal(store.Current.PromptTemplates, restored.Current.PromptTemplates);
            Assert.Equal("legacy second", restored.Current.SelectedModel.SystemPrompt);
            store.Save(store.Current with { SelectedPromptTemplateId = "default" });
            Assert.Equal(store.Current.PromptTemplates.Single(template => template.Id == "default").SystemPrompt, store.Current.SelectedModel.SystemPrompt);
            Assert.Equal(8, store.Current.PromptTemplates.Count);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("[null]")]
    public void Templates_InvalidOptionalDataPreservesModelSettings(string templates)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, "{\"SelectedModelPath\":\"test.gguf\",\"Models\":[{\"ModelPath\":\"test.gguf\",\"MaxTokens\":321}],\"PromptTemplates\":" + templates + ",\"SelectedPromptTemplateId\":\"missing\"}");
            var store = new AiSettingsStore(path);
            Assert.Equal(321, store.Current.SelectedModel.MaxTokens);
            Assert.NotNull(store.Current.SelectedPromptTemplate);
            Assert.Equal(6, store.Current.PromptTemplates.Count);
        }
        finally { Directory.Delete(directory, true); }
    }

    [WpfFact]
    public void TemplateEditor_CreatesEditsDeletesSaves_AndDiscardsCancelledDraft()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, store, runtime, _) = CreateAssistant(directory);
            using(assistant)
            {
                string selectedId;
                using(var settings = new AiModelSettingsViewModel(store, runtime))
                {
                    settings.Templates.Add.Execute(null);
                    var custom = settings.Templates.SelectedTemplate!;
                    selectedId = custom.Id;
                    custom.Name = "Мой редактор";
                    custom.SystemPrompt = "Сохраняй смысл.\nИсправляй ошибки.";
                    settings.ModelPath = Path.Combine(directory, "another.gguf");
                    File.WriteAllBytes(settings.ModelPath, "GGUF"u8.ToArray());
                    Assert.Same(custom, settings.Templates.SelectedTemplate);
                    Assert.DoesNotContain(store.Current.PromptTemplates, template => template.Id == selectedId);
                    settings.Save.Execute(null);
                    Assert.Empty(settings.Status);
                }
                var restored = new AiSettingsStore(Path.Combine(directory, "settings.json"));
                Assert.Equal(selectedId, restored.Current.SelectedPromptTemplateId);
                Assert.Equal("Сохраняй смысл.\nИсправляй ошибки.", restored.Current.SelectedModel.SystemPrompt);
                using(var settings = new AiModelSettingsViewModel(store, runtime))
                {
                    settings.Templates.SelectedTemplate!.Name = "Новое имя";
                    settings.Templates.SelectedTemplate.SystemPrompt = "Новая инструкция";
                    settings.Save.Execute(null);
                }
                Assert.Equal("Новое имя", store.Current.SelectedPromptTemplate!.Name);
                Assert.Equal("Новая инструкция", store.Current.SelectedModel.SystemPrompt);
                using(var cancelled = new AiModelSettingsViewModel(store, runtime))
                {
                    cancelled.Templates.SelectedTemplate!.SystemPrompt = "Не сохранять";
                    cancelled.Templates.Remove.Execute(null);
                }
                Assert.Equal("Новая инструкция", store.Current.SelectedModel.SystemPrompt);
                using(var settings = new AiModelSettingsViewModel(store, runtime))
                {
                    settings.Templates.Remove.Execute(null);
                    settings.Save.Execute(null);
                }
                Assert.DoesNotContain(store.Current.PromptTemplates, template => template.Id == selectedId);
                Assert.NotNull(store.Current.SelectedPromptTemplate);
                using(var settings = new AiModelSettingsViewModel(store, runtime))
                {
                    while(settings.Templates.Remove.CanExecute(null)) settings.Templates.Remove.Execute(null);
                    Assert.Single(settings.Templates.Items);
                    settings.Save.Execute(null);
                }
                Assert.Single(new AiSettingsStore(Path.Combine(directory, "settings.json")).Current.PromptTemplates);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TemplateEditor_RejectsEmptyOrDuplicateNames(bool duplicate)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, store, runtime, _) = CreateAssistant(directory);
            using(assistant)
            using(var settings = new AiModelSettingsViewModel(store, runtime))
            {
                var original = store.Current;
                settings.Templates.Add.Execute(null);
                settings.Templates.SelectedTemplate!.Name = duplicate ? " " + settings.Templates.Items[0].Name.ToUpperInvariant() + " " : " ";
                settings.Save.Execute(null);
                Assert.Same(original, store.Current);
                Assert.Equal(LocalizationManager.GetString(duplicate ? "AI.TemplateNameDuplicate" : "AI.TemplateNameRequired"), settings.Status);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task PanelTemplateSelection_PreservesChatDraftAttachmentsAndLoadedModel_AndReachesNextRequests()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, store, runtime, _) = CreateAssistant(directory);
            using(assistant)
            {
                runtime.CompleteImmediately = true;
                var selector = new ComboBox { DataContext = assistant, DisplayMemberPath = "Name" };
                selector.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(assistant.PromptTemplates)));
                selector.SetBinding(ComboBox.SelectedItemProperty, new Binding(nameof(assistant.SelectedPromptTemplate)) { Mode = BindingMode.TwoWay });
                assistant.Prompt = "first question";
                await assistant.Send.ExecuteAsync();
                assistant.Prompt = "draft";
                assistant.Attachments.Add(AiAttachmentService.Document("note.txt", "document"));
                var history = assistant.Messages.ToArray();
                int unloads = runtime.UnloadCount;
                assistant.SelectedPromptTemplateId = "concise";
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                Assert.Equal("concise", Assert.IsType<AiPromptTemplateViewModel>(selector.SelectedItem).Id);
                Assert.Equal(history, assistant.Messages);
                Assert.Equal("draft", assistant.Prompt);
                Assert.Single(assistant.Attachments);
                Assert.True(runtime.IsLoaded);
                Assert.Equal(unloads, runtime.UnloadCount);
                Assert.Equal("concise", new AiSettingsStore(Path.Combine(directory, "settings.json")).Current.SelectedPromptTemplateId);
                await assistant.Send.ExecuteAsync();
                Assert.Equal(store.Current.SelectedPromptTemplate!.SystemPrompt, runtime.LastSettings!.SystemPrompt);
                Assert.Equal(3, runtime.LastConversation.Count);
                assistant.SelectedPromptTemplateId = "editor";
                assistant.Prompt = "edit next";
                await assistant.Send.ExecuteAsync();
                Assert.Equal(store.Current.SelectedPromptTemplate!.SystemPrompt, runtime.LastSettings!.SystemPrompt);
                Assert.Equal(5, runtime.LastConversation.Count);
                store.Save(store.Current with { IdleMinutes = 11 });
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                Assert.Equal("editor", Assert.IsType<AiPromptTemplateViewModel>(selector.SelectedItem).Id);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task EditingActiveTemplateDuringGeneration_UsesSnapshot_AndAppliesEditNextTime()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, store, runtime, _) = CreateAssistant(directory);
            using(assistant)
            {
                runtime.CompleteImmediately = true;
                var release = new TaskCompletionSource();
                runtime.BeforeText = release.Task;
                assistant.SelectedPromptTemplateId = "concise";
                assistant.Prompt = "question";
                var sending = assistant.Send.ExecuteAsync();
                Assert.True(assistant.IsBusy);
                Assert.False(assistant.CanChangePromptTemplate);
                assistant.SelectedPromptTemplateId = "editor";
                Assert.Equal("concise", assistant.SelectedPromptTemplateId);
                string originalPrompt = runtime.LastSettings!.SystemPrompt;
                using(var settings = new AiModelSettingsViewModel(store, runtime))
                {
                    settings.Templates.SelectedTemplate!.SystemPrompt = "Edited instruction";
                    settings.Save.Execute(null);
                }
                Assert.True(assistant.IsBusy);
                Assert.Equal(originalPrompt, runtime.LastSettings.SystemPrompt);
                Assert.Equal(0, runtime.UnloadCount);
                release.SetResult();
                await sending;
                Assert.Equal(2, assistant.Messages.Count);
                assistant.Prompt = "follow up";
                await assistant.Send.ExecuteAsync();
                Assert.Equal("Edited instruction", runtime.LastSettings.SystemPrompt);
                Assert.Equal(3, runtime.LastConversation.Count);
            }
        }
        finally { Directory.Delete(directory, true); }
    }
}
