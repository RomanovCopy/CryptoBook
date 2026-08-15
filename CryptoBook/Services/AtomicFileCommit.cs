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
            FileAttributes? originalTargetAttributes =
                PrepareTargetForReplacement(targetPath);

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
            finally
            {
                RestoreAttributes(targetPath, originalTargetAttributes);
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

            FileAttributes? originalTargetAttributes =
                PrepareTargetForReplacement(targetPath);
            try
            {
                File.Replace(
                    temporaryPath,
                    targetPath,
                    destinationBackupFileName: null,
                    ignoreMetadataErrors: true);
            }
            finally
            {
                RestoreAttributes(targetPath, originalTargetAttributes);
            }
        }

        internal static void DeleteIfExists(string path)
        {
            if(!File.Exists(path))
                return;

            FileAttributes originalAttributes = File.GetAttributes(path);
            if((originalAttributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(
                    path,
                    originalAttributes & ~FileAttributes.ReadOnly);
            }

            try
            {
                File.Delete(path);
            }
            catch
            {
                RestoreAttributes(path, originalAttributes);
                throw;
            }
        }

        private static FileAttributes? PrepareTargetForReplacement(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            if((attributes & FileAttributes.ReadOnly) == 0)
                return null;

            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            return attributes;
        }

        private static void RestoreAttributes(
            string path,
            FileAttributes? attributes)
        {
            if(attributes is not null && File.Exists(path))
                File.SetAttributes(path, attributes.Value);
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
