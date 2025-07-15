using CryptoBook.Interfaces;

namespace CryptoBook.Models
{
    internal class TestLongOperarion: IProgressOperation<double>
    {
        public TestLongOperarion()
        {
        }

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            const int totalSteps = 100;
            int i = 0;
            try
            {
                for(; i <= totalSteps; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested(); // Проверяем отмену
                    await Task.Delay(50, cancellationToken);          // Симулируем работу
                    progress?.Report(i);                              // Отчитываемся о прогрессе
                }

            } catch(OperationCanceledException)
            {
                MessageBox.Show($"Операция отменема.  value = {i}");
                await RollbackAsync();
                MessageBox.Show($"Данные восстановлены.");
            }

        }

        public async Task RollbackAsync()
        {
            await Task.Delay(500);
        }
    }
}
