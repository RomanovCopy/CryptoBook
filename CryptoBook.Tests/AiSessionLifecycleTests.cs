using CryptoBook.AI;
using CryptoBook.Infrastructure;
using CryptoBook.Security;
using LLama.Native;
using System.IO;
using System.Text.Json;
using Xunit;

namespace CryptoBook.Tests;

public sealed partial class AiIntegrationTests
{
    [WpfFact]
    public async Task StopAndNormalCompletion_KeepPartialAnswerAndLoadedModelUntilExplicitUnload()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, _, runtime, _) = CreateAssistant(directory);
            using(assistant)
            {
                assistant.Prompt = "first question";
                var reply = assistant.Send.ExecuteAsync();
                // The backend has delivered a fragment, but its dispatcher callback has not run yet.
                Assert.Empty(assistant.Messages.Last().Content);
                assistant.Stop.Execute(null);
                Assert.False(assistant.Stop.CanExecute(null));
                await reply;
                Assert.Equal(LocalizationManager.GetString("AI.Stopped"), assistant.Status);
                Assert.True(assistant.IsModelLoaded);
                Assert.Equal("partial answer", assistant.Messages.Last().Content);
                Assert.Equal(0, runtime.UnloadCount);
                runtime.CompleteImmediately = true;
                assistant.Prompt = "follow up";
                await assistant.Send.ExecuteAsync();
                Assert.True(assistant.IsModelLoaded);
                Assert.Equal(3, runtime.LastConversation.Count);
                Assert.Equal(0, runtime.UnloadCount);
                await assistant.ClearChat.ExecuteAsync();
                Assert.True(assistant.IsModelLoaded);
                await assistant.Unload.ExecuteAsync();
                Assert.False(assistant.IsModelLoaded);
                Assert.Equal(1, runtime.UnloadCount);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void SessionOptions_OldSettingsKeepBothOptionsDisabled()
    {
        var settings = JsonSerializer.Deserialize<AiModelSettings>("{\"ContextLength\":4096}")!;
        Assert.False(settings.ReuseContext);
        Assert.False(settings.PreloadOnPanelOpen);
    }

    [WpfTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SessionOptions_AreIndependentPerModel_AndPreserveChatWhenApplied(bool reuse, bool preload)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, store, runtime, _) = CreateAssistant(directory);
            using(assistant)
            using(var settings = new AiModelSettingsViewModel(store, runtime))
            {
                string first = settings.ModelPath, second = Path.Combine(directory, "second.gguf");
                File.WriteAllBytes(second, "GGUF"u8.ToArray());
                assistant.Messages.Add(new("user", "kept history"));
                assistant.Prompt = "kept draft";
                settings.ReuseContext = reuse;
                settings.PreloadOnPanelOpen = preload;
                Assert.False(store.Current.SelectedModel.ReuseContext);
                Assert.False(store.Current.SelectedModel.PreloadOnPanelOpen);
                settings.ModelPath = second;
                Assert.False(settings.ReuseContext);
                Assert.False(settings.PreloadOnPanelOpen);
                settings.ReuseContext = !reuse;
                settings.PreloadOnPanelOpen = !preload;
                settings.ModelPath = first;
                Assert.Equal(reuse, settings.ReuseContext);
                Assert.Equal(preload, settings.PreloadOnPanelOpen);
                settings.Save.Execute(null);
                Assert.Single(assistant.Messages);
                Assert.Equal("kept draft", assistant.Prompt);
                Assert.Equal(0, runtime.LoadCount);
                var restored = new AiSettingsStore(Path.Combine(directory, "settings.json"));
                using var reopened = new AiModelSettingsViewModel(restored, runtime);
                Assert.Equal(reuse, reopened.ReuseContext);
                Assert.Equal(preload, reopened.PreloadOnPanelOpen);
                reopened.ModelPath = second;
                Assert.Equal(!reuse, reopened.ReuseContext);
                Assert.Equal(!preload, reopened.PreloadOnPanelOpen);
                runtime.CompleteImmediately = true;
                await assistant.Send.ExecuteAsync();
                Assert.Equal(reuse, runtime.LastSettings!.ReuseContext);
                Assert.Equal(preload, runtime.LastSettings.PreloadOnPanelOpen);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [WpfTheory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void PanelOpening_DoesNotLoadWhenOptedOutDisabledOrLocked(bool preload, bool enabled, bool locked)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, store, runtime, keyReset) = CreateAssistant(directory);
            using(assistant)
            {
                store.Save(store.Current with { Enabled = enabled, Models = [store.Current.SelectedModel with { PreloadOnPanelOpen = preload }] });
                if(locked) keyReset.SetState(KeyResetState.KeyReset, true);
                assistant.IsOpen = true;
                Assert.Equal(0, runtime.LoadCount);
                Assert.False(assistant.IsBusy);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task PanelOpening_LoadsWithoutChatMessage_AndManualUnloadWaitsForNextOpening()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, store, runtime, _) = CreateAssistant(directory);
            using(assistant)
            {
                store.Save(store.Current with { Models = [store.Current.SelectedModel with { PreloadOnPanelOpen = true, ReuseContext = true }] });
                assistant.Prompt = "draft";
                assistant.IsOpen = true;
                Assert.Equal(1, runtime.LoadCount);
                Assert.True(assistant.IsModelLoaded);
                Assert.Empty(runtime.LastConversation);
                Assert.Empty(assistant.Messages);
                Assert.Equal("draft", assistant.Prompt);
                Assert.True(runtime.LastSettings!.ReuseContext);
                Assert.Equal(LocalizationManager.GetString("AI.Ready"), assistant.Status);
                assistant.IsOpen = false;
                assistant.IsOpen = true;
                Assert.Equal(1, runtime.LoadCount);
                await assistant.Unload.ExecuteAsync();
                Assert.False(assistant.IsModelLoaded);
                Assert.Equal(1, runtime.LoadCount);
                assistant.IsOpen = false;
                assistant.IsOpen = true;
                Assert.Equal(2, runtime.LoadCount);
                await assistant.ClearChat.ExecuteAsync();
                Assert.True(assistant.IsModelLoaded);
                Assert.Equal(1, runtime.ResetContextCount);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PanelPreload_CanBeCancelledByStopOrClosing_AndRetried(bool closePanel)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, store, runtime, _) = CreateAssistant(directory);
            using(assistant)
            {
                store.Save(store.Current with { Models = [store.Current.SelectedModel with { PreloadOnPanelOpen = true }] });
                runtime.BeforeLoad = new TaskCompletionSource().Task;
                assistant.Prompt = "draft";
                assistant.IsOpen = true;
                assistant.IsOpen = true;
                Assert.Equal(1, runtime.LoadCount);
                Assert.True(assistant.IsBusy);
                Assert.False(assistant.Send.CanExecute(null));
                Assert.True(assistant.Stop.CanExecute(null));
                if(closePanel) assistant.IsOpen = false;
                else assistant.Stop.Execute(null);
                await WaitForSessionAsync(() => !assistant.IsBusy);
                Assert.False(runtime.IsLoaded);
                Assert.Empty(assistant.Messages);
                Assert.Equal("draft", assistant.Prompt);
                runtime.BeforeLoad = null;
                assistant.IsOpen = false;
                assistant.IsOpen = true;
                Assert.True(runtime.IsLoaded);
                Assert.Equal(2, runtime.LoadCount);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task PanelPreload_ErrorIsShown_AndSettingsCancelPendingLoadBeforeStartingNewModel()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, store, runtime, _) = CreateAssistant(directory);
            using(assistant)
            {
                store.Save(store.Current with { Models = [store.Current.SelectedModel with { PreloadOnPanelOpen = true }] });
                runtime.LoadError = new AiException("AI.LoadFailed");
                assistant.IsOpen = true;
                Assert.False(assistant.IsBusy);
                Assert.Equal(LocalizationManager.GetString("AI.LoadFailed"), assistant.Status);
                runtime.LoadError = null;
                runtime.BeforeLoad = new TaskCompletionSource().Task;
                assistant.IsOpen = false;
                assistant.IsOpen = true;
                Assert.True(assistant.IsBusy);
                string second = Path.Combine(directory, "second.gguf");
                File.WriteAllBytes(second, "GGUF"u8.ToArray());
                runtime.BeforeLoad = null;
                store.Save(store.Current with { SelectedModelPath = second, Models = [new() { ModelPath = second, PreloadOnPanelOpen = true }] });
                await WaitForSessionAsync(() => !assistant.IsBusy && runtime.IsLoaded);
                Assert.Equal(second, runtime.LastSettings!.ModelPath);
                Assert.Equal(3, runtime.LoadCount);
                Assert.Empty(assistant.Messages);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PanelPreload_LockCancelsLoadingAndClearsSensitiveState(bool pending)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, store, runtime, keyReset) = CreateAssistant(directory);
            using(assistant)
            {
                store.Save(store.Current with { Models = [store.Current.SelectedModel with { PreloadOnPanelOpen = true, ReuseContext = true }] });
                if(pending) runtime.BeforeLoad = new TaskCompletionSource().Task;
                assistant.IsOpen = true;
                assistant.Messages.Add(new("user", "private history"));
                assistant.Prompt = "private draft";
                keyReset.SetState(KeyResetState.KeyReset, true);
                await WaitForSessionAsync(() => !assistant.IsBusy && !runtime.IsLoaded && assistant.Messages.Count == 0);
                Assert.Empty(assistant.Prompt);
                Assert.False(assistant.Send.CanExecute(null));
                assistant.IsOpen = false;
                assistant.IsOpen = true;
                Assert.Equal(1, runtime.LoadCount);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static async Task WaitForSessionAsync(Func<bool> completed)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while(!completed()) await Task.Delay(10, timeout.Token);
    }

    [Theory]
    [InlineData(new int[] { 1, 2, 3 }, new int[] { 1, 2, 3, 4 }, 3)]
    [InlineData(new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, 2)]
    [InlineData(new int[] { 1, 2, 3 }, new int[] { 1, 2 }, 1)]
    [InlineData(new int[] { 1, 2, 3 }, new int[] { 1, 9, 3, 4 }, 1)]
    [InlineData(new int[] { 1, 2, 3 }, new int[] { 7, 2, 3, 4 }, 0)]
    [InlineData(new int[] { }, new int[] { 1, 2, 3 }, 0)]
    public void ContextReuse_OnlyKeepsExactTokenPrefix_AndAlwaysRefreshesLastInputLogits(int[] cached, int[] prompt, int expected)
    {
        Assert.Equal(expected, AiTextInferenceContext.CommonPrefix(cached.Select(token => (LLamaToken)token).ToArray(),
            prompt.Select(token => (LLamaToken)token).ToArray()));
    }
}
