using CryptoBook.Views;

namespace CryptoBook.AI;

public sealed class AiSettingsWindowService(AiSettingsStore store, IAiInferenceService inference)
{
    private AiModelSettingsWindow? window;
    public void Open()
    {
        if(window is not null) { window.Activate(); return; }
        var viewModel = new AiModelSettingsViewModel(store, inference);
        window = new AiModelSettingsWindow(viewModel)
        {
            Owner = System.Windows.Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault()
        };
        window.Closed += (_, _) => { viewModel.Dispose(); window = null; };
        window.Show();
    }
}
