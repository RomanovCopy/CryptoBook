using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public sealed class FileCreationService: IFileCreationService
    {
        private readonly IFileManagerService _fs;

        public FileCreationService(IFileManagerService fs)
        {
            _fs = fs;
        }

        public async Task<string> SuggestUniqueNameAsync(
            string targetDirectory,
            IFileTemplate template,
            CancellationToken ct)
        {
            // Базовое имя + расширение
            string baseName = template.SuggestedBaseName;
            string ext = template.DefaultExtension; // ".txt"
            string candidate = EnsureExtension(baseName, ext);

            // Перебираем: "New file.txt", "New file (2).txt", ...
            for(int i = 0; i < 10_000; i++)
            {
                ct.ThrowIfCancellationRequested();

                string name = i == 0 ? candidate : InsertSuffix(candidate, $" ({i + 1})");
                string fullPath = Combine(targetDirectory, name);

                // можем ли читать/существует ли? — используем пробный open на чтение
                bool exists;
                try
                {
                    using var s = await _fs.OpenReadAsync(fullPath, ct);
                    exists = true;
                } catch(FileNotFoundException)
                {
                    exists = false;
                } catch(DirectoryNotFoundException)
                {
                    // Если папки нет — подскажем исходное имя; создание папки — ответственность выше
                    exists = false;
                } catch
                {
                    // Любая другая ошибка — считаем, что имя занято/проблемное, ищем дальше
                    exists = true;
                }

                if(!exists)
                    return name;
            }

            // Падать не хочется — вернем базовое имя (пусть дальше покажет ошибку при создании)
            return candidate;
        }

        public async Task<FileOperationResult> CreateAsync(
            string targetDirectory,
            string fileNameWithOrWithoutExt,
            IFileTemplate template,
            IfExistsMode ifExists,
            bool isHidden,
            bool isReadOnly,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // 1) Нормализуем имя и расширение
            string ext = template.DefaultExtension;
            string fileName = EnsureExtension(fileNameWithOrWithoutExt, ext);

            // 2) Сформируем полный путь
            string fullPath = Combine(targetDirectory, fileName);

            // 3) Существование
            bool exists = await ExistsAsync(fullPath, ct);

            if(exists && ifExists == IfExistsMode.FailIfExists)
                return FileOperationResult.Fail("Файл уже существует.");

            if(exists && ifExists == IfExistsMode.AutoRename)
            {
                // AutoRename: "name.txt" → "name (2).txt" → ...
                fileName = await SuggestUniqueNameAsync(targetDirectory, template, ct);
                fullPath = Combine(targetDirectory, fileName);
            }

            // 4) Получаем контент шаблона
            byte[] content = await template.GetInitialContentAsync(ct);
            if(template.DefaultEncoding is { } enc && content.Length > 0)
            {
                // Если шаблон вернул строку не в этой кодировке — обычно контент уже в UTF-8.
                // Здесь ничего специально перекодировать не надо.
                // Важно: если нужен BOM — подготовить контент из шаблона как надо.
            }

            // 5) Создаем файл через фасад
            try
            {
                await using var stream = await _fs.OpenWriteAsync(fullPath,
                    overwrite: exists && ifExists == IfExistsMode.Overwrite,
                    cancellationToken: ct);

                if(content.Length > 0)
                    await stream.WriteAsync(content, 0, content.Length, ct);

                return FileOperationResult.Ok();
            } catch(OperationCanceledException)
            {
                return FileOperationResult.Fail("Операция отменена.");
            } catch(Exception ex)
            {
                return FileOperationResult.Fail(ex.Message);
            }
        }

        // ----------------- helpers -----------------

        private static string EnsureExtension(string nameOrFull, string dotExt)
        {
            if(string.IsNullOrWhiteSpace(dotExt) || dotExt[0] != '.')
                dotExt = "." + dotExt;

            // Если уже есть такое же расширение — не добавляем второй раз.
            if(nameOrFull.EndsWith(dotExt, StringComparison.OrdinalIgnoreCase))
                return nameOrFull;

            // Если в имени есть другое расширение — оставляем как есть (юзер ввел явно).
            // Иначе добавим дефолтное.
            if(Path.HasExtension(nameOrFull))
                return nameOrFull;

            return nameOrFull + dotExt;
        }

        private string Combine(string directoryPath, string childName)
        {
            // Нормализуем и склеиваем через FileManagerService, чтобы сохранить схему
            string normalized = _fs.NormalizePath(directoryPath); // "<scheme>://<native>"
            int idx = normalized.IndexOf("://", StringComparison.Ordinal);
            string scheme = idx > 0 ? normalized.Substring(0, idx) : "local";
            string native = idx > 0 ? normalized[(idx + 3)..] : normalized;

            // Нейтральная склейка: добавим "/" — провайдер потом разрулит как надо
            string combinedNative = native.EndsWith("/") || native.EndsWith("\\")
                ? native + childName
                : native + "/" + childName;

            return $"{scheme}://{combinedNative}";
        }

        private async Task<bool> ExistsAsync(string fullPath, CancellationToken ct)
        {
            try
            {
                using var s = await _fs.OpenReadAsync(fullPath, ct);
                return true;
            } catch(FileNotFoundException) { return false; } catch(DirectoryNotFoundException) { return false; } catch { return true; } // прочие ошибки трактуем как «занято»
        }

        private static string InsertSuffix(string fileNameWithExt, string suffix)
        {
            string name = Path.GetFileNameWithoutExtension(fileNameWithExt);
            string ext = Path.GetExtension(fileNameWithExt);
            return name + suffix + ext;
        }
    }

}
