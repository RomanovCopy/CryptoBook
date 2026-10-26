using Autofac;
using CryptoBook.AI;
using CryptoBook.FileTemplates;
using CryptoBook.Injections;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.Security;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Windows.Data;
using Xunit;

namespace CryptoBook.Tests;

public sealed partial class AiIntegrationTests
{
    [WpfTheory]
    [InlineData(KeyResetState.Inactive, false, true)]
    [InlineData(KeyResetState.Active, false, true)]
    [InlineData(KeyResetState.KeyReset, false, true)]
    [InlineData(KeyResetState.Unlocking, false, true)]
    [InlineData(KeyResetState.Restoring, false, true)]
    [InlineData(KeyResetState.Resetting, false, false)]
    [InlineData(KeyResetState.KeyReset, true, false)]
    [InlineData(KeyResetState.Unlocking, true, false)]
    public async Task Send_PendingProtectedSnapshotDoesNotLockNormalWorkspace(
        KeyResetState state, bool retainedDocument, bool expectedEnabled)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, _, _, keyReset) = CreateAssistant(directory);
            using(vm)
            {
                var input = new TextBox();
                input.SetBinding(TextBox.TextProperty, new Binding(nameof(vm.Prompt))
                    { Source = vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                input.Text = "test";
                keyReset.SetState(state, retainedDocument);
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                // A reset clears sensitive drafts; enter a new draft to test the current lock state.
                input.Text = "new question";
                Assert.Equal("new question", vm.Prompt);
                Assert.Equal(expectedEnabled, vm.Send.CanExecute(null));
                Assert.Equal(expectedEnabled, vm.CanAttach);
                Assert.Equal(expectedEnabled ? string.Empty : LocalizationManager.GetString("AI.SendLocked"), vm.SendUnavailableReason);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task Send_AvailabilityAndExplanationUpdateAfterTypingAttachmentsSettingsAndStop()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, store, _, _) = CreateAssistant(directory);
            using(vm)
            {
                var input = new TextBox();
                input.SetBinding(TextBox.TextProperty, new Binding(nameof(vm.Prompt))
                    { Source = vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                var send = vm.Send;
                var hint = new TextBlock();
                hint.SetBinding(TextBlock.TextProperty, new Binding(nameof(vm.SendUnavailableReason)) { Source = vm });
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                Assert.False(send.CanExecute(null));
                Assert.Equal(LocalizationManager.GetString("AI.SendEmpty"), hint.Text);
                input.Text = "test";
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                Assert.True(send.CanExecute(null));
                Assert.Empty(hint.Text);
                input.Text = "   ";
                Assert.False(send.CanExecute(null));
                vm.Attachments.Add(AiAttachmentService.Document("file.txt", "text"));
                Assert.True(send.CanExecute(null));
                vm.Attachments.Clear();
                Assert.False(send.CanExecute(null));
                store.Save(store.Current with { Enabled = false });
                input.Text = "question";
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                Assert.False(send.CanExecute(null));
                Assert.Equal(LocalizationManager.GetString("AI.DisabledHint"), hint.Text);
                store.Save(store.Current with { Enabled = true });
                input.Text = "question";
                Assert.True(send.CanExecute(null));
                Task sending = vm.Send.ExecuteAsync();
                input.Text = "next question";
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                Assert.False(send.CanExecute(null));
                Assert.Equal(LocalizationManager.GetString("AI.SendBusy"), hint.Text);
                vm.Stop.Execute(null);
                await sending;
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                Assert.True(send.CanExecute(null));
                Assert.Empty(hint.Text);
                Assert.Equal("next question", vm.Prompt);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("gemma4", "256", 1, false)]
    [InlineData("gemma4", "256", 16, false)]
    [InlineData("gemma4", "256", 42, false)]
    [InlineData("gemma4", "256", 0, null)]
    [InlineData("gemma4", "256", -1, null)]
    [InlineData("gemma4", "256", 43, null)]
    [InlineData("gemma4", "0", 16, null)]
    [InlineData("gemma4", null, 16, null)]
    [InlineData("qwen3", "256", 16, null)]
    public void ContextCompatibility_Gemma4EdgePartialOffload_AvoidsFatalFlashAttentionGraph(
        string architecture, string? perLayerEmbedding, int gpuLayers, bool? expectedFlashAttention)
    {
        var metadata = new Dictionary<string, string> { ["general.architecture"] = architecture };
        if(perLayerEmbedding is not null) metadata[architecture + ".embedding_length_per_layer_input"] = perLayerEmbedding;
        var parameters = new LLama.Common.ModelParams("model.gguf")
        {
            GpuLayerCount = gpuLayers, ContextSize = 4096, BatchSize = 256, UBatchSize = 128
        };
        var safe = LocalAiInferenceService.ApplyContextCompatibility(parameters, metadata, 42);
        Assert.Equal(expectedFlashAttention, safe.FlashAttention);
        Assert.Equal(gpuLayers, safe.GpuLayerCount);
        Assert.Equal(parameters.ContextSize, safe.ContextSize);
        Assert.Equal(parameters.BatchSize, safe.BatchSize);
        Assert.Equal(parameters.UBatchSize, safe.UBatchSize);
        Assert.Null(parameters.FlashAttention);
    }

    [Fact]
    public void VisionReplyStart_ExcludesAllStopTokensForOneSampleThenRestoresNormalTermination()
    {
        float[] scores = [14, 12, 15, -2];
        var original = scores.ToArray();
        var first = AiReplyStartSampler.Sample(scores, [0, 2], () =>
        {
            Assert.True(float.IsNegativeInfinity(scores[0]));
            Assert.True(float.IsNegativeInfinity(scores[2]));
            Assert.Equal(12, scores[1]);
            return (LLama.Native.LLamaToken)Array.IndexOf(scores, scores.Max());
        });
        Assert.Equal((LLama.Native.LLamaToken)1, first);
        Assert.Equal(original, scores);
        Assert.Equal(2, Array.IndexOf(scores, scores.Max()));
    }

    [Fact]
    public void VisionReplyStart_RestoresNativeScoresWhenSamplingThrows()
    {
        float[] scores = [14, 12];
        Assert.Throws<InvalidOperationException>(() => AiReplyStartSampler.Sample(scores, [0],
            () => throw new InvalidOperationException("sampling failed")));
        Assert.Equal(new float[] { 14, 12 }, scores);
    }

    [WpfFact]
    public async Task ImageRequest_ShowsProcessingBeforeTextAndGeneratingWhileStreaming()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, _, runtime, _) = CreateAssistant(directory);
            using(vm)
            {
                File.WriteAllBytes(Path.Combine(directory, "mmproj-model.gguf"), "GGUF"u8.ToArray());
                var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                runtime.BeforeText = start.Task;
                vm.Attachments.Add(new("image.png", "", new byte[] { 1 }, null, 1));
                vm.Prompt = "Describe the image";
                var sending = vm.Send.ExecuteAsync();
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                Assert.Equal(LocalizationManager.GetString("AI.ProcessingImage"), vm.Status);
                Assert.True(vm.IsBusy);
                var generating = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                vm.PropertyChanged += (_, args) =>
                {
                    if(args.PropertyName == nameof(vm.Status) && vm.Status == LocalizationManager.GetString("AI.Generating"))
                        generating.TrySetResult();
                };
                start.SetResult();
                await generating.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(LocalizationManager.GetString("AI.Generating"), vm.Status);
                vm.Stop.Execute(null);
                await sending;
                Assert.False(vm.IsBusy);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void ImageHistory_ReplaysSnapshotsWithLatestQuestionWithoutMutatingVisibleMessages()
    {
        var first = new AiAttachment("first.png", "", new byte[] { 1 }, null, 1);
        var second = new AiAttachment("second.png", "", new byte[] { 2 }, null, 1);
        var document = AiAttachmentService.Document("notes.txt", "original notes");
        AiTurn[] original = [new("user", "first question", [document, first]), new("assistant", "answer"),
            new("user", "follow-up", [second])];
        var formatted = AiChatFormat.WithImagesInLatestTurn(original);
        Assert.Equal(2, original[0].Attachments!.Count);
        Assert.Same(document, Assert.Single(formatted[0].Attachments!));
        Assert.Equal(new[] { first, second }, formatted[2].Attachments);
        Assert.Equal("follow-up", formatted[2].Content);
        Assert.DoesNotContain("<media>", AiChatFormat.Content(formatted[0]));
        Assert.Contains("first.png", AiChatFormat.Content(formatted[2]));
        Assert.Contains("second.png", AiChatFormat.Content(formatted[2]));
    }

    [Fact]
    public async Task FileAttachments_ReadDocxTextAndRejectUnsupportedOrOversizedFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string document = Path.Combine(directory, "sample.DOCX");
            using(var zip = ZipFile.Open(document, ZipArchiveMode.Create))
            using(var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open()))
                writer.Write("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>Budget 128450</w:t></w:r></w:p><w:p><w:r><w:t>ORBIT-731</w:t></w:r></w:p></w:body></w:document>");
            var service = new AiAttachmentService([]);
            var file = await service.ReadAsync(document);
            Assert.Equal("sample.DOCX", file.Name);
            Assert.Equal("Budget 128450\nORBIT-731", file.Text);
            Assert.False(file.IsImage);
            var error = await Assert.ThrowsAsync<AiException>(() => service.ReadAsync(Path.Combine(directory, "unsupported.exe")));
            Assert.Equal("AI.UnsupportedAttachment", error.ResourceKey);
            string large = Path.Combine(directory, "large.txt");
            using(var stream = File.Create(large)) stream.SetLength(AiAttachmentService.MaxFileBytes + 1L);
            error = await Assert.ThrowsAsync<AiException>(() => service.ReadAsync(large));
            Assert.Equal("AI.AttachmentTooLarge", error.ResourceKey);
        }
        finally { Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task FileAttachments_ImageGetsFrozenPreviewAndKeepsOriginalFileUnchanged()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "sample.png");
            var image = System.Windows.Media.Imaging.BitmapSource.Create(2, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32,
                null, new byte[] { 0, 0, 255, 255, 255, 0, 0, 255 }, 8);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
            using(var output = File.Create(path)) encoder.Save(output);
            byte[] original = await File.ReadAllBytesAsync(path);
            var file = await new AiAttachmentService([]).ReadAsync(path);
            Assert.True(file.IsImage);
            Assert.NotNull(file.Preview);
            Assert.True(file.Preview.IsFrozen);
            Assert.Equal(2, file.Preview.PixelWidth);
            Assert.True(file.ImageData.Span.StartsWith(new byte[] { 137, 80, 78, 71 }));
            Assert.Equal(original, await File.ReadAllBytesAsync(path));
        }
        finally { Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task FileAttachments_SendKeepsSnapshotInHistoryAndDoesNotExposeDocumentAsMessageText()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, _, runtime, _) = CreateAssistant(directory);
            using(vm)
            {
                runtime.CompleteImmediately = true;
                string path = Path.Combine(directory, "source.txt");
                await File.WriteAllTextAsync(path, "Original private document");
                await vm.AttachPathsAsync([path]);
                await File.WriteAllTextAsync(path, "Changed file on disk");
                vm.Prompt = "Summarize the file";
                await vm.Send.ExecuteAsync();
                Assert.Empty(vm.Attachments);
                var message = vm.Messages.First();
                Assert.Equal("Summarize the file", message.Content);
                Assert.Equal("Original private document", Assert.Single(message.Attachments).Text);
                var turn = Assert.Single(runtime.LastConversation);
                Assert.Contains("Original private document", AiChatFormat.Content(turn));
                Assert.DoesNotContain("Changed file on disk", AiChatFormat.Content(turn));
                vm.Prompt = "Follow-up";
                await vm.Send.ExecuteAsync();
                Assert.Single(runtime.LastConversation[0].Attachments!);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task FileAttachments_RequireVisionWithoutDroppingPendingImage_AndLimitPendingFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, _, runtime, _) = CreateAssistant(directory);
            using(vm)
            {
                vm.Attachments.Add(new("image.png", "", new byte[] { 1 }, null, 1));
                Assert.True(vm.Send.CanExecute(null));
                await vm.Send.ExecuteAsync();
                Assert.Null(runtime.LastSettings);
                Assert.Empty(vm.Messages);
                Assert.Single(vm.Attachments);
                Assert.Equal(AiAssistantViewModel.ErrorText(new AiException("AI.ImageNeedsVisionModel")), vm.Status);
                vm.RemoveAttachment.Execute(vm.Attachments[0]);
                var paths = Enumerable.Range(0, 5).Select(index => Path.Combine(directory, $"{index}.txt")).ToArray();
                foreach(string path in paths) await File.WriteAllTextAsync(path, "file text");
                await vm.AttachPathsAsync(paths);
                Assert.Equal(4, vm.Attachments.Count);
                Assert.False(vm.CanAttach);
                Assert.Equal(AiAssistantViewModel.ErrorText(new AiException("AI.AttachmentLimit")), vm.Status);
                vm.RemoveAttachment.Execute(vm.Attachments[0]);
                Assert.True(vm.CanAttach);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task AttachmentCommands_PickerAndDropReadFilesThroughTheAttachmentService()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            string first = Path.Combine(directory, "selected.txt"), second = Path.Combine(directory, "dropped.txt");
            var picker = new AttachmentPickerStub { Paths = [first] };
            var (vm, _, _, _) = CreateAssistant(directory, attachmentPicker: picker);
            using(vm)
            {
                await File.WriteAllTextAsync(first, "selected content");
                await File.WriteAllTextAsync(second, "dropped content");
                Assert.False(vm.AttachPaths.CanExecute(null));
                Assert.False(vm.AttachPaths.CanExecute(Array.Empty<string>()));
                await vm.AttachFiles.ExecuteAsync();
                await vm.AttachPaths.ExecuteAsync(new[] { second });
                Assert.Equal(new[] { "selected content", "dropped content" }, vm.Attachments.Select(file => file.Text));
                vm.Attachments.Add(AiAttachmentService.Document("third.txt", "third"));
                vm.Attachments.Add(AiAttachmentService.Document("fourth.txt", "fourth"));
                Assert.False(vm.AttachFiles.CanExecute(null));
                Assert.False(vm.AttachPaths.CanExecute(new[] { first }));
                vm.RemoveAttachment.Execute(vm.Attachments[0]);
                Assert.True(vm.AttachFiles.CanExecute(null));
                Assert.True(vm.AttachPaths.CanExecute(new[] { first }));
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttachmentCommands_ClearAndDisposeCancelDroppedFileReading(bool dispose)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var reader = new BlockingAttachmentService();
            var (vm, _, _, _) = CreateAssistant(directory, attachmentService: reader);
            using(vm)
            {
                Task reading = vm.AttachPaths.ExecuteAsync(new[] { "pending.txt" });
                Assert.True(vm.AttachPaths.IsRunning);
                Assert.False(vm.AttachFiles.CanExecute(null));
                Assert.False(vm.AttachDocument.CanExecute(null));
                if(dispose) vm.Dispose();
                else vm.ClearChat.Execute(null);
                await reading.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.True(reader.Token.IsCancellationRequested);
                Assert.Empty(vm.Attachments);
                Assert.Empty(vm.Status);
                Assert.False(vm.AttachPaths.IsRunning);
                Assert.Equal(!dispose, vm.CanAttach);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task AttachmentCommands_ClearDiscardsLatePickerResults()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var selection = new TaskCompletionSource<IReadOnlyList<string>>();
            var picker = new AttachmentPickerStub { PendingSelection = selection.Task };
            var (vm, _, _, _) = CreateAssistant(directory, attachmentPicker: picker);
            using(vm)
            {
                Task picking = vm.AttachFiles.ExecuteAsync();
                vm.ClearChat.Execute(null);
                Assert.True(picker.Token.IsCancellationRequested);
                selection.SetResult([Path.Combine(directory, "late.txt")]);
                await picking;
                Assert.Empty(vm.Attachments);
                Assert.Empty(vm.Status);
                Assert.True(vm.AttachFiles.CanExecute(null));
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void CopyResponse_UsesInjectedClipboardAndReportsFailure(bool succeed)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var clipboard = new TextClipboardStub { Succeed = succeed };
            var (vm, _, _, _) = CreateAssistant(directory, textClipboard: clipboard);
            using(vm)
            {
                vm.Messages.Add(new("assistant", "**selected answer**") { IsSelected = true });
                Assert.True(vm.Copy.CanExecute(null));
                vm.Copy.Execute(null);
                Assert.Equal("**selected answer**", clipboard.Text);
                Assert.Equal(succeed ? string.Empty : LocalizationManager.GetString("AI.CopyFailed"), vm.Status);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public void FileAttachments_CurrentDocumentUsesWholeTextInsteadOfSelection()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, _, _, _) = CreateAssistant(directory, "SELECTED and the rest of the document");
            using(vm)
            {
                vm.AttachDocument.Execute(null);
                var file = Assert.Single(vm.Attachments);
                Assert.Equal("source.txt", file.Name);
                Assert.Contains("SELECTED and the rest of the document", file.Text);
                Assert.False(file.IsImage);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(10000, 256, 3839)]
    [InlineData(512, 256, 512)]
    [InlineData(512, 4000, 95)]
    [InlineData(10000, 4094, 1)]
    public void ReplyLimit_UsesRemainingContextInsteadOfRejectingLargeRequestedLimit(int requested, int input, int expected)
    {
        var settings = new AiModelSettings { ContextLength = 4096, MaxTokens = requested };
        Assert.Equal(expected, settings.OutputTokenLimit(input));
    }

    [Theory]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(5000)]
    public void ReplyLimit_RejectsPromptOnlyWhenNoRoomForOutputRemains(int input)
    {
        var error = Assert.Throws<AiException>(() => new AiModelSettings { ContextLength = 4096 }.OutputTokenLimit(input));
        Assert.Equal("AI.ContextFull", error.ResourceKey);
    }

    [WpfFact]
    public async Task ModelSettings_TestThenSave_AllowsReplyLimitLargerThanContext()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, store, runtime, _) = CreateAssistant(directory);
            using(assistant)
            using(var settings = new AiModelSettingsViewModel(store, runtime))
            {
                runtime.CompleteImmediately = true;
                settings.ContextLength = 4096;
                settings.MaxTokens = 10000;
                await settings.Test.ExecuteAsync();
                Assert.NotNull(runtime.LastSettings);
                Assert.Equal(128, runtime.LastSettings.MaxTokens);
                bool saved = false;
                settings.Saved += (_, _) => saved = true;
                settings.Save.Execute(null);
                Assert.True(saved);
                Assert.Equal(10000, new AiSettingsStore(Path.Combine(directory, "settings.json")).Current.SelectedModel.MaxTokens);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfTheory]
    [InlineData(4096, -1, "AI.InvalidMaxTokens")]
    [InlineData(0, 512, "AI.InvalidContextLength")]
    [InlineData(4096, 131073, "AI.InvalidMaxTokens")]
    public async Task ModelSettings_TestAndSave_ValidateTheSameOriginalValues(int context, int reply, string key)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, store, runtime, _) = CreateAssistant(directory);
            using(assistant)
            using(var settings = new AiModelSettingsViewModel(store, runtime))
            {
                settings.ContextLength = context;
                settings.MaxTokens = reply;
                await settings.Test.ExecuteAsync();
                string testError = settings.Status;
                Assert.Equal(AiAssistantViewModel.ErrorText(new AiException(key)), testError);
                Assert.Null(runtime.LastSettings);
                bool saved = false;
                settings.Saved += (_, _) => saved = true;
                settings.Save.Execute(null);
                Assert.False(saved);
                Assert.Equal(testError, settings.Status);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("plain text", "plain text")]
    [InlineData("a<think>private reasoning</think>b", "ab")]
    [InlineData("<think>unfinished", "")]
    [InlineData("<think>x</think><think>y</think>готово", "готово")]
    [InlineData("answer < tag", "answer < tag")]
    [InlineData("<|channel|>analysis<|message|>private<|end|><|start|>assistant<|channel|>final<|message|>ready", "ready")]
    [InlineData("<|channel>thought\nprivate<channel|>ready<turn|>", "ready")]
    public void ThinkingFilter_RemovesReasoningAcrossEveryChunkBoundary(string input, string expected)
    {
        for(int chunkSize = 1; chunkSize <= Math.Max(1, input.Length); chunkSize++)
        {
            var filter = new AiThinkingFilter();
            var output = new StringBuilder();
            for(int i = 0; i < input.Length; i += chunkSize)
                output.Append(filter.Append(input.Substring(i, Math.Min(chunkSize, input.Length - i))));
            output.Append(filter.Flush());
            Assert.Equal(expected, output.ToString());
        }
    }

    [Theory]
    [InlineData("a<think>analysis</think>b", false, "ab", "analysis")]
    [InlineData("<think>unfinished analysis", false, "", "unfinished analysis")]
    [InlineData("analysis</think>answer", true, "answer", "analysis")]
    [InlineData("<|channel>thought\nanalysis<channel|>answer", false, "answer", "analysis")]
    [InlineData("<|channel|>analysis<|message|>analysis<|end|><|start|>assistant<|channel|>final<|message|>answer", false, "answer", "analysis")]
    public void ThinkingFilter_StreamsReasoningSeparatelyAcrossEveryChunkBoundary(
        string input, bool alreadyThinking, string expectedAnswer, string expectedReasoning)
    {
        for(int chunkSize = 1; chunkSize <= input.Length; chunkSize++)
        {
            var reasoning = new StringBuilder();
            var answer = new StringBuilder();
            var filter = new AiThinkingFilter(text => reasoning.Append(text), alreadyThinking);
            for(int i = 0; i < input.Length; i += chunkSize)
                answer.Append(filter.Append(input.Substring(i, Math.Min(chunkSize, input.Length - i))));
            answer.Append(filter.Flush());
            Assert.Equal(expectedReasoning, reasoning.ToString());
            Assert.Equal(expectedAnswer, answer.ToString());
        }
    }

    [Theory]
    [InlineData("<|im_start|>assistant\n<think>\n", true)]
    [InlineData("<|turn>model\n<|channel>thought\n", true)]
    [InlineData("<|start|>assistant<|channel|>analysis<|message|>", true)]
    [InlineData("<|turn>model\n", false)]
    [InlineData("<think>\n\n</think>\n\n", false)]
    public void ThinkingFilter_RecognizesAnAlreadyOpenedReasoningChannel(string prompt, bool thinking) =>
        Assert.Equal(thinking, AiThinkingFilter.StartsInReasoning(prompt));

    [WpfFact]
    public async Task Chat_UnexpectedReasoningIsSeparate_AndOnlyAnswerIsInsertedOrSentAsHistory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            IRichTextBoxService editor = new RichTextBoxService(new ParagraphFactory(), new UriNavigation(), new DocumentAppearanceDefaults());
            var (vm, _, runtime, _) = CreateAssistant(directory, "original document", editor);
            using(vm)
            {
                runtime.CompleteImmediately = true;
                runtime.ReasoningToEmit = "separate model analysis";
                vm.Prompt = "question";
                await vm.Send.ExecuteAsync();
                var answer = vm.Messages.Last();
                Assert.Equal("separate model analysis", answer.Reasoning);
                Assert.True(answer.HasReasoning);
                Assert.Equal("partial answer", answer.Content);
                Assert.True(vm.Insert.CanExecute(null));
                vm.Insert.Execute(null);
                string document = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;
                Assert.Contains("partial answer", document);
                Assert.DoesNotContain("separate model analysis", document);
                vm.Prompt = "follow-up";
                await vm.Send.ExecuteAsync();
                Assert.Equal("partial answer", Assert.Single(runtime.LastConversation, turn => turn.Role == "assistant").Content);
                Assert.All(runtime.LastConversation, turn => Assert.DoesNotContain("separate model analysis", turn.Content));
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task Chat_ReasoningWithoutAnswerIsKeptAndExplained_ButExcludedFromHistory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, _, runtime, keyReset) = CreateAssistant(directory);
            using(vm)
            {
                runtime.CompleteImmediately = true;
                runtime.ReasoningToEmit = "analysis without final answer";
                runtime.ReplyText = "";
                using(var settings = new AiModelSettingsViewModel(new AiSettingsStore(Path.Combine(directory, "settings.json")), runtime))
                {
                    await settings.Test.ExecuteAsync();
                    Assert.Equal(LocalizationManager.GetString("AI.TestReasoningOnly"), settings.Status);
                }
                vm.Prompt = "question";
                await vm.Send.ExecuteAsync();
                Assert.True(vm.Messages.Last().HasReasoning);
                Assert.Equal(LocalizationManager.GetString("AI.ReasoningOnly"), vm.Status);
                Assert.False(vm.Copy.CanExecute(null));
                Assert.False(vm.Insert.CanExecute(null));
                runtime.ReplyText = "answer";
                vm.Prompt = "follow-up";
                await vm.Send.ExecuteAsync();
                Assert.DoesNotContain(runtime.LastConversation, turn => turn.Role == "assistant");
                keyReset.Reset();
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                Assert.Empty(vm.Messages);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task Chat_CancellationWhileThinkingKeepsPartialAnalysisAndStopsTheRequest()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, _, runtime, _) = CreateAssistant(directory);
            using(vm)
            {
                runtime.ReasoningToEmit = "partial analysis";
                runtime.BeforeText = new TaskCompletionSource().Task;
                vm.Prompt = "question";
                var sending = vm.Send.ExecuteAsync();
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                Assert.Equal(LocalizationManager.GetString("AI.Thinking"), vm.Status);
                vm.Stop.Execute(null);
                await sending;
                Assert.False(vm.IsBusy);
                Assert.Equal("partial analysis", vm.Messages.Last().Reasoning);
                Assert.Empty(vm.Messages.Last().Content);
                Assert.Equal(LocalizationManager.GetString("AI.Stopped"), vm.Status);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(512L * 1024 * 1024)]
    public void AutomaticMemory_NoUsableGpuBudget_UsesRam(long gpuBudget)
    {
        const long size = 20L * 1024 * 1024 * 1024;
        var plan = AiMemoryPlanner.Calculate(AiMemoryMode.Automatic, -1, size, 40, 16 * 1024 * 1024,
            gpuBudget, 24L * 1024 * 1024 * 1024);
        Assert.Equal(0, plan.GpuLayers);
        Assert.Equal(size, plan.EstimatedRamWeightBytes);
    }

    [Fact]
    public void AutomaticMemory_LargeModel_SplitsAcrossRamAndGpuWithHeadroom()
    {
        const long gib = 1024L * 1024 * 1024, size = 23 * gib, gpuBudget = 8 * gib;
        var plan = AiMemoryPlanner.Calculate(AiMemoryMode.Automatic, -1, size, 40, 32 * 1024 * 1024,
            gpuBudget, 24 * gib);
        Assert.InRange(plan.GpuLayers, 1, 39);
        Assert.InRange(plan.EstimatedGpuWeightBytes + plan.GpuLayers * 32L * 1024 * 1024, 1, 7 * gib);
        Assert.True(plan.EstimatedRamWeightBytes > 0);
        Assert.Equal(size, plan.EstimatedRamWeightBytes + plan.EstimatedGpuWeightBytes);
        Assert.Equal(0, AiMemoryPlanner.Calculate(AiMemoryMode.CpuOnly, -1, size, 40, 0, gpuBudget, 24 * gib).GpuLayers);
        Assert.Equal(7, AiMemoryPlanner.Calculate(AiMemoryMode.Manual, 7, size, 40, 0, gpuBudget, 24 * gib).GpuLayers);
    }

    [Fact]
    public void Gemma4_TextChat_UsesNativeTurnsAndThinkingPrefix()
    {
        var settings = new AiModelSettings { SystemPrompt = "Be brief" };
        var prompt = AiChatFormat.Gemma4(settings, [new("user", "one"), new("assistant", "two"), new("user", "three")]);
        Assert.Equal("<|turn>system\nBe brief<turn|>\n<|turn>user\none<turn|>\n<|turn>model\ntwo<turn|>\n<|turn>user\nthree<turn|>\n<|turn>model\n<|channel>thought\n<channel|>", prompt);
        prompt = AiChatFormat.Gemma4(settings with { UseReasoning = true }, [new("user", "one")]);
        Assert.Contains("<|turn>system\n<|think|>\nBe brief", prompt);
        Assert.EndsWith("<|turn>model\n", prompt);
    }

    [Theory]
    [InlineData("256", false)]
    [InlineData("128", false)]
    [InlineData("0", true)]
    [InlineData(null, true)]
    public void Gemma4_DisabledThinking_UsesTheModelFamilyPrefix(string? perLayerEmbedding, bool emptyThought)
    {
        var metadata = new Dictionary<string, string>();
        if(perLayerEmbedding is not null) metadata["gemma4.embedding_length_per_layer_input"] = perLayerEmbedding;
        var prompt = AiChatFormat.Gemma4(new(), [new("user", "17 + 25?")], metadata: metadata);
        string expected = "<|turn>user\n17 + 25?<turn|>\n<|turn>model\n";
        if(emptyThought) expected += "<|channel>thought\n<channel|>";
        Assert.Equal(expected, prompt);
        var enabled = AiChatFormat.Gemma4(new() { UseReasoning = true }, [new("user", "17 + 25?")], metadata: metadata);
        Assert.StartsWith("<|turn>system\n<|think|>\n<turn|>\n", enabled);
        Assert.EndsWith("<|turn>model\n", enabled);
        Assert.DoesNotContain("<|channel>thought\n<channel|>", enabled);
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReasoningSetting_TestSaveReloadAndChatUseTheSelectedValue(bool enabled)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, store, runtime, _) = CreateAssistant(directory);
            using(assistant)
            using(var settings = new AiModelSettingsViewModel(store, runtime))
            {
                runtime.CompleteImmediately = true;
                settings.UseReasoning = enabled;
                await settings.Test.ExecuteAsync();
                Assert.Equal(enabled, runtime.LastSettings!.UseReasoning);
                settings.Save.Execute(null);
                var restored = new AiSettingsStore(Path.Combine(directory, "settings.json"));
                Assert.Equal(enabled, restored.Current.SelectedModel.UseReasoning);
                using var reopened = new AiModelSettingsViewModel(restored, runtime);
                Assert.Equal(enabled, reopened.UseReasoning);
                assistant.Prompt = "test";
                await assistant.Send.ExecuteAsync();
                Assert.Equal(enabled, runtime.LastSettings.UseReasoning);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepetitionProtection_TestSaveReloadAndModelSwitchKeepTheSelectedValue(bool enabled)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, store, runtime, _) = CreateAssistant(directory);
            using(assistant)
            using(var settings = new AiModelSettingsViewModel(store, runtime))
            {
                runtime.CompleteImmediately = true;
                settings.RepetitionProtection = enabled;
                await settings.Test.ExecuteAsync();
                Assert.Equal(enabled, runtime.LastSettings!.RepetitionProtection);
                string first = settings.ModelPath;
                settings.ModelPath = Path.Combine(directory, "other.gguf");
                Assert.True(settings.RepetitionProtection);
                settings.ModelPath = first;
                Assert.Equal(enabled, settings.RepetitionProtection);
                settings.Save.Execute(null);
                var restored = new AiSettingsStore(Path.Combine(directory, "settings.json"));
                using var reopened = new AiModelSettingsViewModel(restored, runtime);
                Assert.Equal(enabled, reopened.RepetitionProtection);
                assistant.Prompt = "test";
                await assistant.Send.ExecuteAsync();
                Assert.Equal(enabled, runtime.LastSettings.RepetitionProtection);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfTheory]
    [InlineData(AiPerformanceMode.Balanced)]
    [InlineData(AiPerformanceMode.PowerSaving)]
    [InlineData(AiPerformanceMode.Optimal)]
    public async Task PerformanceMode_TestSaveReloadAndModelSwitchKeepTheSelectedValue(AiPerformanceMode mode)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, store, runtime, _) = CreateAssistant(directory);
            using(assistant)
            using(var settings = new AiModelSettingsViewModel(store, runtime))
            {
                runtime.CompleteImmediately = true;
                settings.PerformanceMode = mode;
                await settings.Test.ExecuteAsync();
                Assert.Equal(mode, runtime.LastSettings!.EffectivePerformanceMode);
                string first = settings.ModelPath;
                settings.ModelPath = Path.Combine(directory, "other.gguf");
                Assert.Equal(AiPerformanceMode.Balanced, settings.PerformanceMode);
                settings.ModelPath = first;
                Assert.Equal(mode, settings.PerformanceMode);
                settings.Save.Execute(null);
                var restored = new AiSettingsStore(Path.Combine(directory, "settings.json"));
                using var reopened = new AiModelSettingsViewModel(restored, runtime);
                Assert.Equal(mode, reopened.PerformanceMode);
                assistant.Prompt = "test";
                await assistant.Send.ExecuteAsync();
                Assert.Equal(mode, runtime.LastSettings.EffectivePerformanceMode);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepetitionStop_DrainsStreamAndTrimsOnlyTheAffectedChannelBeforeNextRequest(bool isReasoning)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (assistant, _, runtime, _) = CreateAssistant(directory);
            using(assistant)
            {
                runtime.ReplyText = isReasoning ? "useful final" : "useful final repeated tail";
                runtime.ReasoningToEmit = isReasoning ? "useful analysis repeated tail" : "useful analysis";
                runtime.ErrorAfterText = new AiRepetitionException(new(isReasoning, isReasoning ? 15 : 12));
                assistant.Prompt = "first question";
                await assistant.Send.ExecuteAsync();
                var answer = assistant.Messages.Last();
                Assert.Equal("useful final", answer.Content);
                Assert.Equal("useful analysis", answer.Reasoning);
                Assert.Equal(LocalizationManager.GetString("AI.RepetitionStopped"), assistant.Status);
                Assert.False(assistant.IsBusy);
                runtime.ErrorAfterText = null;
                runtime.CompleteImmediately = true;
                assistant.Prompt = "next question";
                await assistant.Send.ExecuteAsync();
                Assert.Equal("useful final", Assert.Single(runtime.LastConversation, turn => turn.Role == "assistant").Content);
                Assert.DoesNotContain(runtime.LastConversation, turn => turn.Content.Contains("repeated tail", StringComparison.Ordinal));
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Settings_RoundTripKeepsSeparateModelProfilesAndNoConversation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(directory, "settings.json");
            var store = new AiSettingsStore(path);
            store.Save(new() { Enabled = true, SelectedModelPath = "b.gguf", IdleMinutes = 3,
                Models = [new() { ModelPath = "a.gguf", Temperature = 0.1f }, new() { ModelPath = "b.gguf", ContextLength = 8192, MemoryMode = AiMemoryMode.CpuOnly }] });
            var restored = new AiSettingsStore(path);
            Assert.True(restored.Current.Enabled);
            Assert.Equal(3, restored.Current.IdleMinutes);
            Assert.Equal(8192, restored.Current.SelectedModel.ContextLength);
            Assert.Equal(AiMemoryMode.CpuOnly, restored.Current.SelectedModel.MemoryMode);
            Assert.Equal(0.1f, restored.Current.Models[0].Temperature);
            Assert.DoesNotContain("Messages", File.ReadAllText(path));
            Assert.DoesNotContain("SelectedModel\"", File.ReadAllText(path));
            File.WriteAllText(path, "{corrupt");
            Assert.False(new AiSettingsStore(path).Current.Enabled);
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task MissingModel_DoesNotLoadRuntime_AndUnloadIsIdempotent()
    {
        using var service = new LocalAiInferenceService();
        var error = await Assert.ThrowsAsync<AiException>(() => service.GenerateAsync(
            new() { ModelPath = "missing-" + Guid.NewGuid().ToString("N") + ".gguf" },
            [new("user", "test")], _ => { }, null, CancellationToken.None));
        Assert.Equal("AI.ModelMissing", error.ResourceKey);
        await service.UnloadAsync();
        await service.UnloadAsync();
        Assert.False(service.IsLoaded);
    }

    [Fact]
    public void ModelSettings_SwitchingFilesKeepsParameters_AndEmptyRecentSelectionKeepsTypedPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string first = Path.Combine(directory, "first.gguf"), second = Path.Combine(directory, "second.gguf"), third = Path.Combine(directory, "third.gguf");
            foreach(string path in new[] { first, second, third }) File.WriteAllBytes(path, "GGUF"u8.ToArray());
            var store = new AiSettingsStore(Path.Combine(directory, "settings.json"));
            store.Save(new() { Enabled = true, SelectedModelPath = first,
                Models = [new() { ModelPath = first }, new() { ModelPath = second, Temperature = 0.6f }] });
            using var vm = new AiModelSettingsViewModel(store, new RuntimeStub());
            vm.Temperature = 1.2f;
            vm.ModelPath = second;
            Assert.Equal(0.6f, vm.Temperature);
            vm.ModelPath = third;
            vm.SelectedRecentModel = null;
            Assert.Equal(third, vm.ModelPath);
            vm.SelectedRecentModel = first;
            Assert.Equal(1.2f, vm.Temperature);
            vm.Save.Execute(null);
            var restored = new AiSettingsStore(Path.Combine(directory, "settings.json"));
            Assert.Equal(1.2f, restored.Current.SelectedModel.Temperature);
            Assert.Equal(0.6f, restored.Current.Models.Single(m => m.ModelPath == second).Temperature);
        }
        finally { Directory.Delete(directory, true); }
    }

    [WpfFact]
    public void Editor_RejectsChangedOrReadOnlyDocument_AndReplacementIsUndoable()
    {
        IRichTextBoxService editor = new RichTextBoxService(new ParagraphFactory(), new UriNavigation(), new DocumentAppearanceDefaults());
        var markdown = new MarkdownDocumentState();
        var session = new DocumentSession(editor, markdown);
        var navigation = new Navigation();
        var context = new AiEditorContext(editor, markdown, session, navigation);
        var run = new Run("original text");
        var document = new FlowDocument(new Paragraph(run));
        session.Open("test.txt", new PlainTextTemplate(), document);
        var host = new Window { Content = editor.Service, Width = 400, Height = 250 };
        host.Show();
        try
        {
        editor.Service.UpdateLayout();
        editor.Focus();
        editor.Selection.Select(run.ContentStart, run.ContentEnd);
        var target = context.Capture()!;
        editor.Service.IsUndoEnabled = false;
        editor.Service.IsUndoEnabled = true;
        Assert.True(target.Apply("replacement", true));
        Assert.Contains("replacement", new TextRange(document.ContentStart, document.ContentEnd).Text);
        editor.Undo();
        Assert.Contains("original text", new TextRange(document.ContentStart, document.ContentEnd).Text);
        editor.Selection.Select(document.ContentStart, document.ContentEnd);
        target = context.Capture()!;
        session.MarkDirty();
        Assert.False(target.Apply("stale", true));
        target = context.Capture()!;
        editor.IsReadOnly = true;
        Assert.False(target.Apply("read only", false));
        }
        finally { host.Close(); }
    }

    private sealed class ParagraphFactory : IParagraphFactory
    {
        public IParagraphService Create(Inline? inline = null)
        {
            var paragraph = new ParagraphService();
            if(inline is not null) paragraph.Inlines.Add(inline);
            return paragraph;
        }
    }

    [WpfFact]
    public void Markdown_ReplacementSupportsUndo_AndRejectsReopenedDocument()
    {
        IRichTextBoxService rich = new RichTextBoxService(new ParagraphFactory(), new UriNavigation(), new DocumentAppearanceDefaults());
        var markdown = new MarkdownDocumentState();
        var session = new DocumentSession(rich, markdown);
        var textDocument = new MarkdownTextDocument("original text", Encoding.UTF8, []);
        markdown.Activate(textDocument, "sample.md");
        session.Open("sample.md", new MarkdownFileTemplate());
        var source = new TextBox { AcceptsReturn = true };
        source.SetBinding(TextBox.TextProperty, new Binding(nameof(IMarkdownDocumentState.Text))
            { Source = markdown, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        var page = new Page { Content = source };
        NameScope.SetNameScope(page, new NameScope());
        page.RegisterName("SourceEditor", source);
        var navigation = new Navigation { CurrentKey = "MarkdownEditor", CurrentPage = page };
        var context = new AiEditorContext(rich, markdown, session, navigation);
        var host = new Window { Content = page, Width = 400, Height = 250 };
        host.Show();
        try
        {
            source.UpdateLayout(); source.Focus(); source.Select(0, 8);
            var target = context.Capture()!;
            Assert.True(rich.IsReadOnly); // The hidden rich-text editor is read-only for Markdown.
            Assert.True(target.Apply("new", true));
            Assert.Equal("new text", markdown.Text);
            source.Undo();
            Assert.Equal("original text", markdown.Text);
            source.Select(0, 8);
            target = context.Capture()!;
            markdown.Activate(textDocument, "sample.md");
            Assert.False(target.Apply("stale", true));
        }
        finally { host.Close(); }
    }
    private sealed class UriNavigation : IUriNavigationService
    {
        public bool TryOpen(Uri uri) => false;
    }

    [WpfFact]
    public async Task Unload_StopsGenerationAndKeepsUnloadedStatus()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, _, runtime, _) = CreateAssistant(directory);
            using(vm)
            {
                vm.Prompt = "test";
                Task send = vm.Send.ExecuteAsync();
                Assert.True(vm.IsBusy);
                await vm.Unload.ExecuteAsync();
                await send;
                Assert.False(runtime.IsLoaded);
                Assert.False(vm.IsBusy);
                Assert.Equal(CryptoBook.Infrastructure.LocalizationManager.GetString("AI.Unloaded"), vm.Status);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task IdleUnload_PreservesChat_ButLockClearsChatAndDraft()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, store, runtime, keyReset) = CreateAssistant(directory);
            using(vm)
            {
                vm.Messages.Add(new("user", "private text"));
                vm.Attachments.Add(AiAttachmentService.Document("private.txt", "private file"));
                vm.Prompt = "private draft";
                runtime.IsLoaded = true;
                runtime.LastUsedUtc = DateTime.UtcNow.AddMinutes(-20);
                runtime.IsBusy = true;
                await vm.CheckIdleAsync(DateTime.UtcNow);
                Assert.True(runtime.IsLoaded);
                runtime.IsBusy = false;
                await vm.CheckIdleAsync(DateTime.UtcNow);
                Assert.False(runtime.IsLoaded);
                Assert.Single(vm.Messages);
                runtime.IsLoaded = true;
                keyReset.Reset();
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                Assert.Empty(vm.Messages);
                Assert.Empty(vm.Prompt);
                Assert.Empty(vm.Attachments);
                Assert.False(runtime.IsLoaded);
                store.Save(store.Current with { Enabled = false });
                Assert.False(vm.Enabled);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task ModelSettings_SwitchCancelsGenerationUnloadsAndUsesSavedParameters()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, store, runtime, _) = CreateAssistant(directory);
            using(vm)
            {
                string first = store.Current.SelectedModelPath, second = Path.Combine(directory, "second.gguf");
                File.WriteAllBytes(second, "GGUF"u8.ToArray());
                store.Save(store.Current with { Models = [.. store.Current.Models,
                    new() { ModelPath = second, Temperature = 0.8f, ContextLength = 4096, MaxTokens = 10000, MemoryMode = AiMemoryMode.CpuOnly }] });
                using var settings = new AiModelSettingsViewModel(store, runtime) { ModelPath = second };
                vm.Prompt = "first model";
                var generating = vm.Send.ExecuteAsync();
                Assert.True(runtime.IsBusy);
                settings.Save.Execute(null);
                await generating;
                Assert.False(runtime.IsLoaded);
                Assert.Empty(vm.Messages);
                Assert.Equal(second, store.Current.SelectedModelPath);
                vm.Prompt = "second model";
                generating = vm.Send.ExecuteAsync();
                Assert.Equal(0.8f, runtime.LastSettings!.Temperature);
                Assert.Equal(4096, runtime.LastSettings.ContextLength);
                Assert.Equal(10000, runtime.LastSettings.MaxTokens);
                Assert.Equal(AiMemoryMode.CpuOnly, runtime.LastSettings.MemoryMode);
                await vm.Unload.ExecuteAsync();
                await generating;
                Assert.Equal(second, new AiSettingsStore(Path.Combine(directory, "settings.json")).Current.SelectedModelPath);
                Assert.Equal(0.3f, store.Current.Models.Single(model => model.ModelPath == first).Temperature);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public void ModelSettings_SavePreservesProfilesAddedWhileWindowIsOpen()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, store, runtime, _) = CreateAssistant(directory);
            using(vm)
            using(var settings = new AiModelSettingsViewModel(store, runtime))
            {
                string second = Path.Combine(directory, "second.gguf");
                File.WriteAllBytes(second, "GGUF"u8.ToArray());
                store.Save(store.Current with { Models = [.. store.Current.Models, new() { ModelPath = second }] });
                Assert.Equal(AiMemoryMode.Automatic, store.Current.SelectedModel.MemoryMode);
                Assert.Null(runtime.LastSettings);
                settings.Save.Execute(null);
                var restored = new AiSettingsStore(Path.Combine(directory, "settings.json"));
                Assert.Contains(restored.Current.Models, model => model.ModelPath == second);
                Assert.Equal(2, restored.Current.Models.Count);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task PerformancePermissionChange_PreservesConversationAndDraft()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, store, runtime, _) = CreateAssistant(directory);
            using(vm)
            {
                vm.Messages.Add(new("user", "keep question"));
                vm.Messages.Add(new("assistant", "keep answer"));
                vm.Prompt = "keep draft";
                foreach(bool enabled in new[] { true, false })
                {
                    store.Save(store.Current with { Models = [store.Current.SelectedModel with { AllowPerformanceBoost = enabled }] });
                    await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    Assert.Equal(2, vm.Messages.Count);
                    Assert.Equal("keep draft", vm.Prompt);
                    Assert.Equal("keep answer", vm.Messages[1].Content);
                    Assert.False(runtime.IsLoaded);
                }
                foreach(var mode in Enum.GetValues<AiPerformanceMode>())
                {
                    store.Save(store.Current with { Models = [store.Current.SelectedModel with { PerformanceMode = mode }] });
                    await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    Assert.Equal(2, vm.Messages.Count);
                    Assert.Equal("keep draft", vm.Prompt);
                    Assert.Equal("keep answer", vm.Messages[1].Content);
                    Assert.False(runtime.IsLoaded);
                }
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task ModelSettings_InvalidFilePreservesSelectionAndConversation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, store, runtime, _) = CreateAssistant(directory);
            using(vm)
            {
                string selected = store.Current.SelectedModelPath;
                vm.Messages.Add(new("user", "keep this conversation"));
                runtime.IsLoaded = true;
                using var settings = new AiModelSettingsViewModel(store, runtime) { ModelPath = Path.Combine(directory, "missing.gguf") };
                await settings.Test.ExecuteAsync();
                settings.Save.Execute(null);
                Assert.Equal(selected, store.Current.SelectedModelPath);
                Assert.Single(vm.Messages);
                Assert.True(runtime.IsLoaded);
                Assert.Single(store.Current.Models);
                Assert.Equal(AiAssistantViewModel.ErrorText(new AiException("AI.ModelMissing")), settings.Status);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task ChatAppearance_SaveDuringGenerationPreservesChatDraftAttachmentsAndRuntime()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, store, runtime, _) = CreateAssistant(directory);
            using(vm)
            {
                vm.Prompt = "question";
                var generating = vm.Send.ExecuteAsync();
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                vm.Prompt = "next draft";
                vm.Attachments.Add(AiAttachmentService.Document("next.txt", "attachment"));
                var appearance = new AiChatAppearanceViewModel(store)
                {
                    FontFamily = "Consolas", FontSize = 20, UserColor = "#123abc", AssistantColor = "#456DEF", ShowMessageBorders = true,
                    RenderMarkdown = false, CodeFontFamily = "Courier New", CodeColor = "#ab1234"
                };
                appearance.Save.Execute(null);
                Assert.True(vm.IsBusy);
                Assert.True(runtime.IsLoaded);
                Assert.Equal(0, runtime.UnloadCount);
                Assert.Equal(2, vm.Messages.Count);
                Assert.Equal("next draft", vm.Prompt);
                Assert.Single(vm.Attachments);
                Assert.Equal("#123ABC", vm.Appearance.UserColor);
                Assert.Equal("Courier New", vm.Appearance.CodeFontFamily);
                Assert.Equal("#AB1234", vm.Appearance.CodeColor);
                Assert.False(vm.Appearance.RenderMarkdown);
                Assert.Equal(vm.Appearance, new AiSettingsStore(Path.Combine(directory, "settings.json")).Current.ChatAppearance);
                using var settings = new AiModelSettingsViewModel(store, runtime);
                settings.Save.Execute(null);
                Assert.Equal(vm.Appearance, store.Current.ChatAppearance);
                Assert.True(vm.IsBusy);
                vm.Stop.Execute(null);
                await generating;
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public void ChatAppearance_InvalidInputDoesNotOverwriteSettings_AndOldFilesGetDefaults()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, store, runtime, _) = CreateAssistant(directory);
            using(vm)
            {
                var original = store.Current;
                var appearance = new AiChatAppearanceViewModel(store) { UserColor = "Transparent", FontSize = 0 };
                appearance.Save.Execute(null);
                Assert.Same(original, store.Current);
                Assert.NotEmpty(appearance.Status);
                appearance = new AiChatAppearanceViewModel(store) { CodeColor = "invalid" };
                appearance.Save.Execute(null);
                Assert.Same(original, store.Current);
                appearance = new AiChatAppearanceViewModel(store) { CodeFontFamily = "" };
                appearance.Save.Execute(null);
                Assert.Same(original, store.Current);
                File.WriteAllText(Path.Combine(directory, "old.json"), "{\"Enabled\":true,\"IdleMinutes\":7}");
                var old = new AiSettingsStore(Path.Combine(directory, "old.json"));
                Assert.True(old.Current.Enabled);
                Assert.Equal(7, old.Current.IdleMinutes);
                Assert.Equal(new AiChatAppearance(), old.Current.ChatAppearance);
                File.WriteAllText(Path.Combine(directory, "old.json"), "{\"ChatAppearance\":{\"FontFamily\":null,\"UserColor\":null,\"AssistantColor\":\"bad\",\"FontSize\":999,\"CodeFontFamily\":null,\"CodeColor\":\"bad\"}}");
                Assert.Equal(new AiChatAppearance(), new AiSettingsStore(Path.Combine(directory, "old.json")).Current.ChatAppearance);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task ContextCounter_UsesActualReducedContextAndAllOutputTokens_ThenResetsForNewChat()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, _, runtime, _) = CreateAssistant(directory);
            using(vm)
            {
                vm.Prompt = "question";
                runtime.UsageToEmit = new(2048, 1900, 100);
                runtime.ReasoningToEmit = "hidden reasoning";
                var generating = vm.Send.ExecuteAsync();
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                vm.UpdateContextBudget();
                Assert.Equal(2048, vm.ContextLength);
                Assert.Equal(47, vm.RemainingTokens);
                Assert.Equal(47, vm.ReplyTokenLimit);
                Assert.True(vm.IsContextLow);
                Assert.DoesNotContain("≈", vm.ContextSummary);
                Assert.True(runtime.HasUsageSubscribers);
                vm.Stop.Execute(null);
                await generating;
                Assert.False(runtime.HasUsageSubscribers);
                vm.ClearChat.Execute(null);
                Assert.Equal(4096, vm.ContextLength);
                Assert.Equal(vm.ContextLength, vm.RemainingTokens);
                Assert.Equal(0, vm.ContextUsagePercent);
                Assert.False(vm.IsContextLow);
                Assert.Contains("≈", vm.ContextSummary);
                int empty = vm.RemainingTokens;
                vm.Prompt = new string('Ж', 900);
                vm.UpdateContextBudget();
                Assert.True(vm.RemainingTokens < empty);
                vm.Attachments.Add(AiAttachmentService.Document("document.txt", new string('文', 900)));
                int withoutFile = vm.RemainingTokens;
                vm.UpdateContextBudget();
                Assert.True(vm.RemainingTokens < withoutFile);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChatReset_RestoresFullBudgetImmediately_AndIgnoresPreviousChatUsage(bool newChat)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, _, runtime, _) = CreateAssistant(directory);
            using(vm)
            {
                runtime.UsageToEmit = new(4096, 3900, 100);
                runtime.CompleteImmediately = true;
                vm.Prompt = new string('Ж', 5000);
                await vm.Send.ExecuteAsync();
                var previousUsage = runtime.LastUsageCallback;
                Assert.NotNull(previousUsage);
                vm.Prompt = "old draft";
                vm.Attachments.Add(AiAttachmentService.Document("old.txt", "old attachment"));
                vm.UpdateContextBudget();
                Assert.True(vm.IsContextLow);
                var reset = newChat ? vm.NewChat : vm.ClearChat;
                Assert.True(reset.CanExecute(null));
                reset.Execute(null);
                Assert.Empty(vm.Messages);
                Assert.Empty(vm.Prompt);
                Assert.Empty(vm.Attachments);
                Assert.Equal(vm.ContextLength, vm.RemainingTokens);
                Assert.Equal(0, vm.ContextUsagePercent);
                Assert.Equal(512, vm.ReplyTokenLimit);
                Assert.False(vm.IsContextLow);
                Assert.True(runtime.IsLoaded);
                Assert.Equal(0, runtime.UnloadCount);
                Assert.Equal(1, runtime.ResetContextCount);

                previousUsage(new(4096, 4090, 5));
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                vm.UpdateContextBudget();
                Assert.Equal(vm.ContextLength, vm.RemainingTokens);

                runtime.UsageToEmit = new(4096, 15, 1);
                vm.Prompt = "fresh question";
                await vm.Send.ExecuteAsync();
                vm.UpdateContextBudget();
                Assert.Equal("fresh question", Assert.Single(runtime.LastConversation).Content);
                Assert.Equal(2, vm.Messages.Count);
                Assert.True(vm.RemainingTokens > 4000);
                Assert.False(vm.IsContextLow);
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void ContextEstimate_IncludesSystemPromptHistoryAndDocuments_AndClampsOverflow()
    {
        AiTurn[] turns = [new("user", "first"), new("assistant", "answer"),
            new("user", "next", [AiAttachmentService.Document("doc.txt", new string('Ж', 600))])];
        int full = AiContextBudget.Estimate(new() { SystemPrompt = new string('文', 600) }, turns);
        int withoutSystem = AiContextBudget.Estimate(new(), turns);
        int withoutHistory = AiContextBudget.Estimate(new(), turns.Skip(2));
        Assert.True(full > withoutSystem);
        Assert.True(withoutSystem > withoutHistory);
        Assert.Equal(0, new AiTokenUsage(512, 600, 1).RemainingTokens);
    }

    [WpfTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ApplyReply_UsesCurrentSelectionAfterAnswer_AndAllowsRepeatedInsertion(bool useMarkdownEditor, bool renderMarkdown)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        var rich = (IRichTextBoxService)new RichTextBoxService(new ParagraphFactory(), new UriNavigation(), new DocumentAppearanceDefaults());
        var markdown = new MarkdownDocumentState();
        var session = new DocumentSession(rich, markdown);
        var navigation = new Navigation();
        var run = new Run("alpha beta");
        var document = new FlowDocument(new Paragraph(run));
        TextBox? source = null;
        var host = new Window { Width = 500, Height = 300, ShowInTaskbar = false, ShowActivated = false };
        try
        {
            if(useMarkdownEditor)
            {
                markdown.Activate(new("alpha beta", Encoding.UTF8, []), "source.md");
                session.Open("source.md", new MarkdownFileTemplate());
                source = new TextBox { AcceptsReturn = true };
                source.SetBinding(TextBox.TextProperty, new Binding(nameof(IMarkdownDocumentState.Text))
                    { Source = markdown, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                var page = new Page { Content = source };
                NameScope.SetNameScope(page, new NameScope()); page.RegisterName("SourceEditor", source);
                navigation.CurrentKey = "MarkdownEditor"; navigation.CurrentPage = page; host.Content = page;
            }
            else { session.Open("source.txt", new PlainTextTemplate(), document); host.Content = rich.Service; }
            host.Show();
            var (vm, store, runtime, _) = CreateAssistant(directory, richTextEditor: rich, markdownState: markdown,
                documentSession: session, navigation: navigation);
            using(vm)
            {
                store.Save(store.Current with { ChatAppearance = store.Current.ChatAppearance with { RenderMarkdown = renderMarkdown } });
                runtime.CompleteImmediately = true; runtime.ReplyText = "**reply**";
                if(source is not null) source.Select(0, 0); else rich.Selection.Select(run.ContentStart, run.ContentStart);
                vm.Prompt = "question";
                await vm.Send.ExecuteAsync();
                Assert.True(vm.Insert.CanExecute(null));
                Assert.False(vm.Replace.CanExecute(null));
                int notifications = 0;
                vm.Replace.CanExecuteChanged += (_, _) => notifications++;
                if(source is not null) source.Select(6, 4);
                else rich.Selection.Select(run.ContentStart.GetPositionAtOffset(6)!, run.ContentEnd);
                Assert.True(vm.Replace.CanExecute(null));
                Assert.True(notifications > 0);
                vm.Replace.Execute(null);
                string Text() => source is not null ? markdown.Text : new TextRange(document.ContentStart, document.ContentEnd).Text.TrimEnd('\r', '\n');
                string reply = useMarkdownEditor ? "**reply**" : "reply";
                Assert.Equal("alpha " + reply, Text());
                Assert.True(vm.Insert.CanExecute(null));
                Assert.False(vm.Replace.CanExecute(null));
                if(source is not null) source.Undo(); else rich.Undo();
                Assert.Equal("alpha beta", Text());
                if(source is not null) source.Select(source.Text.Length, 0);
                else rich.Selection.Select(document.Blocks.LastBlock!.ContentEnd, document.Blocks.LastBlock.ContentEnd);
                vm.Insert.Execute(null);
                Assert.Equal("alpha beta" + reply, Text());
                Assert.True(vm.Insert.CanExecute(null));
                vm.Insert.Execute(null);
                Assert.Equal("alpha beta" + reply + reply, Text());
                if(source is not null) source.IsReadOnly = true; else rich.IsReadOnly = true;
                Assert.False(vm.Insert.CanExecute(null)); Assert.False(vm.Replace.CanExecute(null));
                Assert.NotEmpty(vm.InsertUnavailableReason);
            }
        }
        finally { host.Close(); if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task ApplyReply_BecomesAvailableWhenEditableSurfaceOpensAfterAnswer()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        var rich = (IRichTextBoxService)new RichTextBoxService(new ParagraphFactory(), new UriNavigation(), new DocumentAppearanceDefaults());
        var host = new Window { Width = 400, Height = 250, ShowInTaskbar = false, ShowActivated = false };
        try
        {
            var (vm, _, runtime, _) = CreateAssistant(directory, richTextEditor: rich);
            using(vm)
            {
                runtime.CompleteImmediately = true;
                vm.Prompt = "question"; await vm.Send.ExecuteAsync();
                Assert.True(vm.Copy.CanExecute(null)); Assert.False(vm.Insert.CanExecute(null));
                int notifications = 0; vm.Insert.CanExecuteChanged += (_, _) => notifications++;
                host.Content = rich.Service; host.Show();
                Assert.True(notifications > 0);
                Assert.True(vm.Insert.CanExecute(null));
                vm.Insert.Execute(null);
                Assert.Contains(runtime.ReplyText, new TextRange(rich.Document.ContentStart, rich.Document.ContentEnd).Text);
                Assert.True(vm.Insert.CanExecute(null));
            }
        }
        finally { host.Close(); if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Chat_SelectedMessagesTransferInConversationOrder_WithFormattingAndUndo(bool useMarkdownEditor)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        var rich = (IRichTextBoxService)new RichTextBoxService(new ParagraphFactory(), new UriNavigation(), new DocumentAppearanceDefaults());
        var markdown = new MarkdownDocumentState();
        var session = new DocumentSession(rich, markdown);
        var navigation = new Navigation();
        var document = new FlowDocument(new Paragraph(new Run("original document")));
        TextBox? source = null;
        var host = new Window { Width = 500, Height = 300, ShowInTaskbar = false, ShowActivated = false };
        try
        {
            if(useMarkdownEditor)
            {
                markdown.Activate(new("original document", Encoding.UTF8, []), "source.md");
                session.Open("source.md", new MarkdownFileTemplate());
                source = new TextBox { AcceptsReturn = true };
                source.SetBinding(TextBox.TextProperty, new Binding(nameof(IMarkdownDocumentState.Text))
                    { Source = markdown, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                var page = new Page { Content = source };
                NameScope.SetNameScope(page, new NameScope()); page.RegisterName("SourceEditor", source);
                navigation.CurrentKey = "MarkdownEditor"; navigation.CurrentPage = page; host.Content = page;
            }
            else { session.Open("source.txt", new PlainTextTemplate(), document); host.Content = rich.Service; }
            host.Show();
            var (vm, _, runtime, _) = CreateAssistant(directory, richTextEditor: rich, markdownState: markdown,
                documentSession: session, navigation: navigation);
            using(vm)
            {
                runtime.CompleteImmediately = true;
                runtime.ReasoningToEmit = "private analysis";
                runtime.ReplyText = "**earlier answer**";
                vm.Attachments.Add(AiAttachmentService.Document("attachment.txt", "private attachment"));
                vm.Prompt = "earlier question"; await vm.Send.ExecuteAsync();
                runtime.ReplyText = "later answer";
                vm.Prompt = "later question"; await vm.Send.ExecuteAsync();
                Assert.Equal(1, vm.SelectedMessageCount);
                Assert.Equal("later answer", vm.GetSelectedText());

                // Select an earlier reply after a later one has arrived.
                vm.ClearMessageSelection.Execute(null);
                vm.Messages[1].IsSelected = true;
                Assert.Equal("**earlier answer**", vm.GetSelectedText());
                if(source is not null) source.Select(source.Text.Length, 0);
                else rich.Selection.Select(document.Blocks.LastBlock!.ContentEnd, document.Blocks.LastBlock.ContentEnd);
                vm.Insert.Execute(null);
                string Text() => source is not null ? markdown.Text : new TextRange(document.ContentStart, document.ContentEnd).Text.TrimEnd('\r', '\n');
                Assert.Equal("original document" + (useMarkdownEditor ? "**earlier answer**" : "earlier answer"), Text());
                if(source is not null) source.Undo(); else rich.Undo();
                Assert.Equal("original document", Text());

                // Click in reverse order; the document must still follow the conversation.
                vm.ClearMessageSelection.Execute(null);
                vm.Messages[3].IsSelected = true;
                vm.Messages[0].IsSelected = true;
                string selected = $"**{vm.Messages[0].RoleLabel}:**\n\nearlier question\n\n---\n\n**{vm.Messages[3].RoleLabel}:**\n\nlater answer";
                Assert.Equal(selected, vm.GetSelectedText());
                if(source is not null) source.Select(0, source.Text.Length);
                else rich.Selection.Select(document.ContentStart, document.ContentEnd);
                Assert.True(vm.Replace.CanExecute(null));
                vm.Replace.Execute(null);
                if(useMarkdownEditor) Assert.Equal(selected, Text());
                else
                {
                    Assert.True(Text().IndexOf("earlier question", StringComparison.Ordinal) < Text().IndexOf("later answer", StringComparison.Ordinal));
                    Assert.DoesNotContain("earlier answer", Text());
                    Assert.DoesNotContain("later question", Text());
                }
                if(source is not null) source.Undo(); else rich.Undo();
                Assert.Equal("original document", Text());

                vm.SelectAllMessages.Execute(null);
                Assert.Equal(4, vm.SelectedMessageCount);
                Assert.False(vm.SelectAllMessages.CanExecute(null));
                Assert.True(vm.Copy.CanExecute(null));
                selected = vm.GetSelectedText();
                Assert.Contains("**earlier answer**", selected);
                Assert.Contains("later question", selected);
                Assert.DoesNotContain("private analysis", selected);
                Assert.DoesNotContain("private attachment", selected);
                if(source is not null) source.Select(source.Text.Length, 0);
                else rich.Selection.Select(document.Blocks.LastBlock!.ContentEnd, document.Blocks.LastBlock.ContentEnd);
                vm.Insert.Execute(null);
                if(useMarkdownEditor) Assert.Equal("original document" + selected, Text());
                else
                {
                    Assert.Contains("earlier question", Text()); Assert.Contains("later question", Text());
                    Assert.Contains("earlier answer", Text()); Assert.Contains("later answer", Text());
                    Assert.Contains(new CryptoBook.Infrastructure.FlowDocumentWalker().Traverse(document),
                        element => element is Run { Text: "earlier answer", FontWeight: var weight } && weight == FontWeights.Bold);
                }
                if(source is not null) source.Undo(); else rich.Undo();
                Assert.Equal("original document", Text());
            }
        }
        finally { host.Close(); if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task Chat_DefaultSelectionUsesLatestUsefulAnswer_AndManualSelectionSurvivesFollowUps()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, _, runtime, _) = CreateAssistant(directory, "original document");
            using(vm)
            {
                runtime.CompleteImmediately = true;
                runtime.ReplyText = "first answer";
                vm.Prompt = "first question"; await vm.Send.ExecuteAsync();
                runtime.ReplyText = "second answer";
                vm.Prompt = "second question"; await vm.Send.ExecuteAsync();
                Assert.Equal("second answer", vm.GetSelectedText());
                Assert.False(vm.Messages[1].IsSelected);

                runtime.ReplyText = ""; runtime.ReasoningToEmit = "analysis only";
                vm.Prompt = "no final answer"; await vm.Send.ExecuteAsync();
                Assert.Equal("second answer", vm.GetSelectedText());
                Assert.False(vm.Messages.Last().IsSelected);
                vm.ClearMessageSelection.Execute(null);
                Assert.Equal(0, vm.SelectedMessageCount);
                Assert.False(vm.Insert.CanExecute(null)); Assert.False(vm.Replace.CanExecute(null)); Assert.False(vm.Copy.CanExecute(null));
                Assert.Equal(LocalizationManager.GetString("AI.ApplyNeedsMessages"), vm.InsertUnavailableReason);

                runtime.ReplyText = "third answer"; runtime.ReasoningToEmit = "";
                vm.Prompt = "third question"; await vm.Send.ExecuteAsync();
                Assert.Equal(0, vm.SelectedMessageCount);
                vm.Messages[0].IsSelected = true;
                vm.UpdateContextBudget();
                int remaining = vm.RemainingTokens;
                vm.SelectAllMessages.Execute(null);
                Assert.False(vm.Messages[5].IsSelected); // Reasoning alone is never exported.
                vm.UpdateContextBudget(); Assert.Equal(remaining, vm.RemainingTokens);
                string selection = vm.GetSelectedText();
                vm.Prompt = "fourth question"; await vm.Send.ExecuteAsync();
                Assert.Equal(selection, vm.GetSelectedText());
                Assert.Contains(runtime.LastConversation, turn => turn.Content == "third answer");

                vm.NewChat.Execute(null);
                Assert.Equal(0, vm.SelectedMessageCount);
                Assert.False(vm.SelectAllMessages.CanExecute(null));
                Assert.False(vm.ClearMessageSelection.CanExecute(null));
                vm.Prompt = "new chat question"; await vm.Send.ExecuteAsync();
                Assert.Equal(1, vm.SelectedMessageCount);
                Assert.Equal("third answer", vm.GetSelectedText());
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WpfFact]
    public async Task Chat_SelectionIsDisabledDuringGeneration_AndClearedOnWorkspaceLock()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, _, runtime, keyReset) = CreateAssistant(directory, "original document");
            using(vm)
            {
                vm.Prompt = "question";
                var sending = vm.Send.ExecuteAsync();
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                Assert.False(vm.CanSelectMessages);
                Assert.False(vm.SelectAllMessages.CanExecute(null)); Assert.False(vm.ClearMessageSelection.CanExecute(null));
                Assert.False(vm.Insert.CanExecute(null)); Assert.False(vm.Copy.CanExecute(null));
                vm.Stop.Execute(null); await sending;
                Assert.True(vm.CanSelectMessages);
                Assert.Equal("partial answer", vm.GetSelectedText());
                vm.SelectAllMessages.Execute(null);
                Assert.Equal(2, vm.SelectedMessageCount);
                var removed = vm.Messages.Last();
                vm.Messages.Remove(removed);
                Assert.Equal(1, vm.SelectedMessageCount);
                vm.Messages[0].Content = " ";
                Assert.Equal(0, vm.SelectedMessageCount);
                Assert.False(vm.Copy.CanExecute(null));
                vm.Messages[0].Content = "question";
                Assert.True(vm.Copy.CanExecute(null));
                keyReset.Reset();
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                Assert.Empty(vm.Messages); Assert.Equal(0, vm.SelectedMessageCount);
                Assert.False(vm.CanSelectMessages);
                int notifications = 0;
                vm.Copy.CanExecuteChanged += (_, _) => notifications++;
                removed.IsSelected = false;
                Assert.Equal(0, notifications);
                Assert.Empty(vm.GetSelectedText());
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static (AiAssistantViewModel, AiSettingsStore, RuntimeStub, KeyResetStub) CreateAssistant(
        string directory, string? documentText = null, IRichTextBoxService? richTextEditor = null,
        MarkdownDocumentState? markdownState = null, IDocumentSession? documentSession = null, IPageNavigationService? navigation = null,
        IAiAttachmentPicker? attachmentPicker = null, ITextClipboardService? textClipboard = null,
        IAiAttachmentService? attachmentService = null)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "model.gguf");
        File.WriteAllBytes(path, "GGUF"u8.ToArray());
        var store = new AiSettingsStore(Path.Combine(directory, "settings.json"));
        store.Save(new() { Enabled = true, SelectedModelPath = path, Models = [new() { ModelPath = path }] });
        var runtime = new RuntimeStub();
        var keyReset = new KeyResetStub();
        IRichTextBoxService editor = richTextEditor ?? new RichTextBoxService(new ParagraphFactory(), new UriNavigation(), new DocumentAppearanceDefaults());
        var markdown = markdownState ?? new MarkdownDocumentState();
        var session = documentSession ?? new DocumentSession(editor, markdown);
        if(documentText is not null)
        {
            var document = new FlowDocument(new Paragraph(new Run(documentText)));
            session.Open("source.txt", new PlainTextTemplate(), document);
            editor.Selection.Select(document.ContentStart, document.ContentStart.GetPositionAtOffset(8)!);
        }
        var context = new AiEditorContext(editor, markdown, session, navigation ?? new Navigation());
        return (new AiAssistantViewModel(store, runtime, context, new AiSettingsWindowService(store, runtime), keyReset,
            Dispatcher.CurrentDispatcher, attachmentService ?? new AiAttachmentService([]),
            attachmentPicker ?? new AttachmentPickerStub(), textClipboard ?? new TextClipboardStub(), new UriNavigation()), store, runtime, keyReset);
    }

    private sealed class AttachmentPickerStub : IAiAttachmentPicker
    {
        public IReadOnlyList<string> Paths { get; set; } = [];
        public Task<IReadOnlyList<string>>? PendingSelection { get; set; }
        public CancellationToken Token { get; private set; }
        public Task<IReadOnlyList<string>> PickAttachmentsAsync(CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            return PendingSelection ?? Task.FromResult(Paths);
        }
    }

    private sealed class TextClipboardStub : ITextClipboardService
    {
        public string? Text { get; private set; }
        public bool Succeed { get; set; } = true;
        public bool TrySetText(string text) { Text = text; return Succeed; }
    }

    private sealed class BlockingAttachmentService : IAiAttachmentService
    {
        public CancellationToken Token { get; private set; }
        public async Task<AiAttachment> ReadAsync(string path, CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("The read must be cancelled.");
        }
    }

    private sealed class RuntimeStub : IAiInferenceService, IAiTokenUsageSource
    {
        public event Action<AiTokenUsage>? TokenUsageChanged;
        public bool HasUsageSubscribers => TokenUsageChanged is not null;
        public AiTokenUsage? UsageToEmit { get; set; }
        public Action<AiTokenUsage>? LastUsageCallback { get; private set; }
        public bool IsLoaded { get; set; }
        public int UnloadCount { get; private set; }
        public int ResetContextCount { get; private set; }
        public int LoadCount { get; private set; }
        public Task? BeforeLoad { get; set; }
        public Exception? LoadError { get; set; }
        public AiModelSettings? LastSettings { get; private set; }
        public IReadOnlyList<AiTurn> LastConversation { get; private set; } = [];
        public bool CompleteImmediately { get; set; }
        public Task? BeforeText { get; set; }
        public string ReasoningToEmit { get; set; } = "";
        public string ReplyText { get; set; } = "partial answer";
        public Exception? ErrorAfterText { get; set; }
        public bool IsBusy { get; set; }
        public DateTime LastUsedUtc { get; set; } = DateTime.UtcNow;
        public async Task GenerateAsync(AiModelSettings settings, IReadOnlyList<AiTurn> conversation,
            Action<string> onText, IProgress<float>? loadProgress, CancellationToken cancellationToken, Action<string>? onReasoning = null)
        {
            LastSettings = settings;
            LastConversation = conversation.ToArray();
            IsLoaded = IsBusy = true;
            try
            {
                loadProgress?.Report(1);
                LastUsageCallback = TokenUsageChanged;
                if(UsageToEmit is not null) TokenUsageChanged?.Invoke(UsageToEmit);
                if(ReasoningToEmit.Length > 0) onReasoning?.Invoke(ReasoningToEmit);
                if(BeforeText is not null) await BeforeText.WaitAsync(cancellationToken);
                if(ReplyText.Length > 0) onText(ReplyText);
                if(ErrorAfterText is not null) throw ErrorAfterText;
                if(!CompleteImmediately) await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            finally { IsBusy = false; }
        }
        public async Task LoadAsync(AiModelSettings settings, IProgress<float>? loadProgress, CancellationToken cancellationToken)
        {
            LoadCount++;
            LastSettings = settings;
            IsBusy = true;
            try
            {
                if(BeforeLoad is not null) await BeforeLoad.WaitAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if(LoadError is not null) throw LoadError;
                IsLoaded = true;
                loadProgress?.Report(1);
            }
            finally { IsBusy = false; }
        }
        public Task ResetContextAsync(CancellationToken cancellationToken = default) { ResetContextCount++; return Task.CompletedTask; }
        public Task UnloadAsync(CancellationToken cancellationToken = default) { UnloadCount++; IsLoaded = false; return Task.CompletedTask; }
        public void Dispose() { }
    }

    private sealed class KeyResetStub : IKeyResetService
    {
        public KeyResetState State { get; private set; } = KeyResetState.Active;
        public TimeSpan Timeout => TimeSpan.Zero;
        public bool IsPaused => false;
        public bool HasRetainedDocument { get; private set; }
        public event EventHandler<KeyResetStateChangedEventArgs>? StateChanged;
        public event EventHandler<Exception>? SnapshotFailed { add { } remove { } }
        public void Reset() => SetState(KeyResetState.Resetting);
        public void SetState(KeyResetState state, bool retainedDocument = false)
        {
            State = state; HasRetainedDocument = retainedDocument;
            StateChanged?.Invoke(this, new(State));
        }
        public void Start() { }
        public void Stop() { }
        public void NotifyActivity() { }
        public void UpdateTimeout(TimeSpan timeout) { }
        public IDisposable Pause() => this;
        public Task<bool> ResetAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> TryUnlockAsync(ReadOnlyMemory<char> key, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task RestoreSnapshotAsync(bool restoreAsUnsaved, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class Navigation : IPageNavigationService
    {
        public System.ComponentModel.PropertyChangedEventHandler? Handler;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged { add => Handler += value; remove => Handler -= value; }
        public Page? CurrentPage { get; set; }
        public string? CurrentKey { get; set; } = "Home";
        public bool CanGoBack => false;
        public bool CanGoForward => false;
        public IReadOnlyList<string>? Keys => ["Home"];
        public void Navigate(string key, object? args = null) { }
        public void GoBack() { }
        public void GoForward() { }
        public void Remove(string key) { }
    }
}
