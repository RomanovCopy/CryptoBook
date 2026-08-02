using System;
using System.IO;

namespace CryptoBook.Services
{
    /// <summary>
    /// Публикует полностью записанный временный файл атомарной заменой целевого.
    /// </summary>
    internal static class AtomicFileCommit
    {
        internal static void CommitWithBackup(
            string temporaryPath,
            string targetPath,
            Action<string, string, string>? replaceFile = null)
        {
            if(!File.Exists(targetPath))
            {
                File.Move(temporaryPath, targetPath);
                return;
            }

            replaceFile ??= static (source, destination, backup) =>
                File.Replace(
                    source,
                    destination,
                    backup,
                    ignoreMetadataErrors: true);

            string backupPath = targetPath + ".bak";
            string preservedBackupPath =
                $"{backupPath}.{Guid.NewGuid():N}.tmp";
            bool preservedBackup = false;

            // Существующую .bak временно отодвигаем: File.Replace перезапишет её.
            // При ошибке прежняя резервная копия должна вернуться на место.
            try
            {
                if(File.Exists(backupPath))
                {
                    File.Move(backupPath, preservedBackupPath);
                    preservedBackup = true;
                }

                replaceFile(temporaryPath, targetPath, backupPath);
            }
            catch
            {
                if(preservedBackup)
                    File.Move(
                        preservedBackupPath,
                        backupPath,
                        overwrite: true);

                throw;
            }

            if(preservedBackup)
                TryDelete(preservedBackupPath);
        }

        internal static void CommitWithoutBackup(
            string temporaryPath,
            string targetPath)
        {
            if(!File.Exists(targetPath))
            {
                File.Move(temporaryPath, targetPath);
                return;
            }

            File.Replace(
                temporaryPath,
                targetPath,
                destinationBackupFileName: null,
                ignoreMetadataErrors: true);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if(File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Неудачная очистка старой копии не должна превращать
                // уже завершённое сохранение в ошибку.
            }
        }
    }
}
