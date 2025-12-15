using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    public class NewFileDialogModel: ViewModelBase, INewFileDialogModel, IWindowWithId
    {
        private readonly IFileTemplateRegistry _registry;
        private readonly IFileCreationService _creator;
        private readonly IFileManagerService _fileManager;
        private readonly IFolderPickerService _folderPicker;
        private readonly ICommandService _commandService;
        private readonly IWindowManager _windowManager;

        private CancellationTokenSource? _cts;

        /// <summary>
        /// предыдущая рабочая директория
        /// </summary>
        private string _lastTargetDirection { get; set; }


        /// <summary>
        /// Событие запроса на закрытие окна диалога.
        /// Вызывается из VM → View с результатом операции (успех/ошибка).
        /// </summary>
        public event Func<FileOperationResult, Task>? CloseRequested; // VM → View (успех/ошибка)

        /// <summary>
        /// Уникальный идентификатор окна (используется менеджером окон для управления конкретным экземпляром).
        /// </summary>
        public Guid WindowId { get => windowId; private set => SetProperty(ref windowId, value); }
        Guid windowId;

        /// <summary>
        /// Список доступных шаблонов файлов, полученных из реестра шаблонов.
        /// Используется для заполнения UI (выпадающий список выбора типа создаваемого файла).
        /// </summary>
        public IReadOnlyList<IFileTemplate> Templates { get => templates; private set => SetProperty(ref templates, value); }
        IReadOnlyList<IFileTemplate> templates;

        /// <summary>
        /// Текущий выбранный шаблон файла.
        /// От выбора зависит предлагаемое расширение и логика создания файла.
        /// </summary>
        public IFileTemplate? SelectedTemplate { get => template; set => SetProperty(ref template, value); }
        IFileTemplate? template;

        /// <summary>
        /// Имя создаваемого файла (включая расширение, если указано).
        /// Свойство связывается с UI и используется при создании файла.
        /// </summary>
        public string FileName { get => fileName ?? string.Empty; set => SetProperty(ref fileName, value ?? string.Empty); }
        string? fileName;

        /// <summary>
        /// Режим обработки конфликта, если файл с таким именем уже существует
        /// (например, AutoRename, Overwrite и т.п.).
        /// </summary>
        public IfExistsMode IfExists { get => ifExist = IfExistsMode.AutoRename; set => SetProperty(ref ifExist, value); }
        IfExistsMode ifExist;

        /// <summary>
        /// Флаг: при отсутствии целевой директории — создать её автоматически при подтверждении.
        /// </summary>
        public bool CreateDirectoryIfMissing { get => createDirectoryIfMissing; set => SetProperty(ref createDirectoryIfMissing, value); }
        bool createDirectoryIfMissing;


        /// <summary>
        /// Целевая директория для создаваемого файла.
        /// Связывается с UI и используется для проверки прав и генерации уникального имени.
        /// </summary>
        public string TargetDirectory { get => targetDirectory ?? string.Empty; set => SetProperty(ref targetDirectory, value); }
        string? targetDirectory;

        /// <summary>
        /// Текст последней ошибки, обнаруженной при проверках (например, при проверке директории).
        /// Используется для отображения сообщения об ошибке в UI.
        /// </summary>
        public string? ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }
        string? errorMessage;

        /// <summary>
        /// Атрибут создаваемого файла — только для чтения.
        /// Управляет установкой соответствующего флага при создании.
        /// </summary>
        public bool IsReadOnly { get => isReadOnly; set => SetProperty(ref isReadOnly, value); }
        bool isReadOnly;

        /// <summary>
        /// Атрибут создаваемого файла — скрытый.
        /// Управляет установкой соответствующего флага при создании.
        /// </summary>
        public bool IsHidden { get => isHidden; set => SetProperty(ref isHidden, value); }
        bool isHidden;

        /// <summary>
        /// Признак наличия прав на запись в целевой директории.
        /// Устанавливается в результате проверки директории через FileManager.
        /// </summary>
        public bool CanWrite { get => canWrite; set => SetProperty(ref canWrite, value); }
        bool canWrite;

        /// <summary>
        /// Флаг отображения скрытых файлов при просмотре содержимого директории.
        /// Влияет на поведение Browse и DirectoryExistsAsync.
        /// </summary>
        public bool ShowHiddenFiles { get => showHiddenFiles; set => SetProperty(ref showHiddenFiles, value); }
        bool showHiddenFiles;

        /// <summary>
        /// Признак того, что выполняется фоновая операция (проверка директории, генерация имени и т.п.).
        /// При изменении этого флага обновляется состояние команд (CanExecute).
        /// </summary>
        public bool IsBusy
        {
            get => isBusy;
            private set
            {
                SetProperty(ref isBusy, value);
                (_commandService.GetCommand(CommandKey.NewFileDialog_Create) as IRaiseCanExecuteChanged)?.RaiseCanExecuteChanged();
            }
        }
        bool isBusy;

        /// <summary>
        /// Инициализирует модель диалога создания нового файла.
        /// Загружает доступные шаблоны и выбирает первый по умолчанию.
        /// </summary>
        public NewFileDialogModel(IFileTemplateRegistry registry, IFileCreationService creator, IFileManagerService fileManager, IFolderPickerService folderPicker, ICommandService commandService, IWindowManager windowManager)
        {
            WindowId = Guid.NewGuid();
            _registry = registry;
            _creator = creator;
            _fileManager = fileManager;
            _folderPicker = folderPicker;
            _fileManager = fileManager;
            _commandService = commandService;
            _windowManager = windowManager;
            Templates = _registry.GetAll();
            SelectedTemplate = Templates.FirstOrDefault();
        }

        /// <summary>
        /// Проверяет возможность запуска инициализации предполагаемого имени файла.
        /// Возвращает true, если выбран шаблон.
        /// </summary>
        public bool CanExceute_InitSuggested(object? obj)
        {
            return SelectedTemplate is not null;
        }

        /// <summary>
        /// Запускает асинхронную инициализацию предполагаемого имени файла для переданной директории.
        /// Отменяет предыдущую операцию, если была.
        /// </summary>
        /// <param name="obj">Ожидается строка с целевой директорией.</param>
        public void Execute_InitSuggested(object? obj)
        {
            if(obj is string targetDirectory)
            {
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                var ct = _cts.Token;
                _ = InitSuggestedAsync(targetDirectory, ct);
            }
        }

        /// <summary>
        /// Возвращает true, если можно открыть браузер выбора папки (не в состоянии занятости).
        /// </summary>
        public bool CanExecute_Browse(object? obj)
        {
            return !IsBusy;
        }
        /// <summary>
        /// Открывает диалог выбора папки асинхронно.
        /// Отменяет предыдущую операцию и запускает новую с токеном отмены.
        /// </summary>
        public void Execute_Browse(object? obj)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            _ = BrowseAsync(ct);
        }


        /// <summary>
        /// Проверяет возможность применения выбранного шаблона к текущему имени файла.
        /// Требуется длина имени > 3 и выбранный шаблон.
        /// </summary>
        public bool CanExecute_SelectedNewTemplate(object? obj)
        {
            return FileName.Length > 3 && SelectedTemplate is not null;
        }
        /// <summary>
        /// Применяет расширение выбранного шаблона к текущему имени файла,
        /// сохраняя базовую часть имени до точки.
        /// </summary>
        public void Execute_SelectedNewTemplate(object? obj)
        {
            FileName = FileName.Split('.')[0].Trim() + SelectedTemplate?.DefaultExtension;
        }

        /// <summary>
        /// Возвращает true — доступна команда создания директории (всегда).
        /// </summary>
        public bool CanExecute_CreateDirectory(object? obj)
        {
            return true;
        }
        /// <summary>
        /// Заглушка для команды создания директории — при необходимости реализует логику создания папки.
        /// </summary>
        public void Execute_CreateDirectory(object? obj)
        {
        }

        /// <summary>
        /// Определяет, можно ли создать файл (учитывает состояние Busy, выбранный шаблон,
        /// заполненные TargetDirectory/FileName и права записи или флаг создания директории).
        /// </summary>
        public bool CanExecute_Create(object? obj)
        {
            return CanCreate();
        }

        /// <summary>
        /// Запускает асинхронное создание файла и закрывает окно диалога.
        /// Отменяет предыдущие операции и использует новый токен отмены.
        /// </summary>
        public void Execute_Create(object? obj)
        {
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                var ct = _cts.Token;
                _ = CreateAsync(TargetDirectory, ct);
                _windowManager.CloseWindow(WindowId);
        }

        /// <summary>
        /// Возвращает true, если существует активный токен отмены (операция в процессе).
        /// </summary>
        public bool CanExecute_Cancel(object? obj)
        {
            return _cts is not null;
        }

        /// <summary>
        /// Отменяет текущую операцию и закрывает окно диалога.
        /// </summary>
        public void Execute_Cancel(object? obj)
        {
            _cts?.Cancel();
            TargetDirectory=_lastTargetDirection;
            _windowManager.CloseWindow(WindowId);
        }

        /// <summary>
        /// Команда Close — доступна всегда (хук для View).
        /// </summary>
        public bool CanExecute_Close(object? obj)
        {
            return true;
        }

        /// <summary>
        /// Обработчик Close — заглушка (оставлен для соответствия жизненному циклу окна).
        /// </summary>
        public void Execute_Close(object? obj)
        {
        }

        /// <summary>
        /// Команда Loaded — доступна всегда (хук для View при загрузке).
        /// </summary>
        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }

        /// <summary>
        /// Обработчик Loaded — заглушка (можно разместить логику инициализации UI).
        /// </summary>
        public void Execute_Loaded(object? obj)
        {
        }

        /// <summary>
        /// Команда Closing — доступна всегда (хук для View при закрытии).
        /// </summary>
        public bool CanExecute_Closing(object? obj)
        {
            return true;
        }

        /// <summary>
        /// Обрабатывает начало закрытия окна: освобождает и сбрасывает токен отмены.
        /// </summary>
        public void Execute_Closing(object? obj)
        {
            _cts?.Dispose();
            _cts = null;
        }

        /// <summary>
        /// Команда Closed — доступна всегда (хук после закрытия).
        /// </summary>
        public bool CanExecute_Closed(object? obj)
        {
            return true;
        }

        /// <summary>
        /// Обработчик события Closed — заглушка (оставлен для расширения).
        /// </summary>
        public void Execute_Closed(object? obj)
        {
        }

        /// <summary>
        /// Нормализует путь через FileManager (удаляет лишние разделители, приводит к провайдер-специфицчной форме).
        /// </summary>
        /// <param name="path">Исходный путь.</param>
        /// <returns>Нормализованный путь.</returns>
        private string Normalize(string path) => _fileManager.NormalizePath(path);

        /// <summary>
        /// Асинхронно получает уникальное имя файла на основе выбранного шаблона для указанной директории.
        /// Если шаблон не выбран — ничего не делает.
        /// </summary>
        /// <param name="targetDirectory">Целевая директория для предложения имени.</param>
        /// <param name="ct">Токен отмены.</param>
        private async Task InitSuggestedAsync(string targetDirectory, CancellationToken ct)
        {
            if(SelectedTemplate is null)
                return;
            FileName = await _creator.SuggestUniqueNameAsync(targetDirectory, SelectedTemplate, ct);
        }

        /// <summary>
        /// Выполняет создание файла через сервис создания файлов.
        /// Возвращает результат операции или ошибку, если шаблон не выбран.
        /// </summary>
        /// <param name="targetDirectory">Целевая директория.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Результат операции создания файла.</returns>
        private async Task<FileOperationResult> CreateAsync(string targetDirectory, CancellationToken ct)
        {
            if(SelectedTemplate is null)
                return FileOperationResult.Fail("Не выбран тип файла.");

            return await _creator.CreateAsync(targetDirectory, FileName, SelectedTemplate, IfExists, IsHidden, IsReadOnly, ct);
        }

        // Инициализация из панели (перед показом диалога)

        /// <summary>
        /// Инициализирует модель перед показом диалога: устанавливает целевую директорию,
        /// проверяет доступность/права на запись и предлагает уникальное имя файла по шаблону.
        /// </summary>
        /// <param name="initialDirectory">Начальная директория.</param>
        /// <param name="ct">Токен отмены.</param>
        public async Task InitializeAsync(string initialDirectory, CancellationToken ct)
        {
            TargetDirectory = initialDirectory;
            await CheckDirectoryAsync(ct);

            if(SelectedTemplate != null)
                FileName = await _creator.SuggestUniqueNameAsync(Normalize(TargetDirectory), SelectedTemplate, ct);
        }

        /// <summary>
        /// Проверяет, достаточно ли условий для создания файла:
        /// не в состоянии Busy, выбран шаблон, заполнены директория и имя, и есть права на запись или включена опция создания каталога.
        /// </summary>
        private bool CanCreate() =>
        !IsBusy &&
        SelectedTemplate != null &&
        !string.IsNullOrWhiteSpace(TargetDirectory) &&
        !string.IsNullOrWhiteSpace(FileName) &&
        (CanWrite || CreateDirectoryIfMissing);


        /// <summary>
        /// Открывает диалог выбора папки, при выборе обновляет TargetDirectory,
        /// выполняет проверку директории и предлагает имя файла при необходимости.
        /// </summary>
        /// <param name="ct">Токен отмены.</param>
        private async Task BrowseAsync(CancellationToken ct)
        {
            var chosen = await _folderPicker.PickFolderAsync(TargetDirectory, ct);
            if(chosen != null)
            {
                _lastTargetDirection = TargetDirectory;
                TargetDirectory = chosen;
                await CheckDirectoryAsync(ct);

                if(SelectedTemplate != null && string.IsNullOrWhiteSpace(FileName))
                    FileName = await _creator.SuggestUniqueNameAsync(Normalize(TargetDirectory), SelectedTemplate, ct);
            }
        }

        /// <summary>
        /// Проверяет существование директории и права на запись.
        /// Устанавливает свойства IsBusy, ErrorMessage и CanWrite в процессе проверки.
        /// </summary>
        /// <param name="ct">Токен отмены.</param>
        private async Task CheckDirectoryAsync(CancellationToken ct)
        {
            try
            {
                IsBusy = true;
                ErrorMessage = null;                    
                CanWrite = false;

                var normalized = Normalize(TargetDirectory);
                // если директории ещё нет — CanWrite=false, но можно создать при OK
                bool exists = await DirectoryExistsAsync(normalized, ct);
                CanWrite = exists && await _fileManager.CanWriteAsync(normalized, ct);
            } catch(Exception ex)
            {
                ErrorMessage = ex.Message;
            } finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Нежёсткая проверка существования директории: пытается просмотреть содержимое через FileManager.
        /// Возвращает false при DirectoryNotFoundException, при IO с сообщением "not found" или при любой другой ошибке.
        /// </summary>
        /// <param name="normalizedPath">Нормализованный путь.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>true, если директория доступна для чтения.</returns>
        private async Task<bool> DirectoryExistsAsync(string normalizedPath, CancellationToken ct)
        {
            // мягкая проверка: попытаться прочитать содержимое
            try
            {
                _ = await _fileManager.BrowseAsync(normalizedPath, ct, ShowHiddenFiles);
                return true;
            } catch(DirectoryNotFoundException) { return false; } catch(IOException io) when(io.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)) { return false; } catch { return false; }
        }

        /// <summary>
        /// Пытается создать директорию через FileManager и возвращает результат операции.
        /// Ловит исключения и преобразует их в FileOperationResult.Fail.
        /// </summary>
        /// <param name="normalizedPath">Нормализованный путь создаваемой директории.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Результат создания директории.</returns>
        private async Task<FileOperationResult> EnsureDirectoryAsync(string normalizedPath, CancellationToken ct)
        {
            // создадим «как есть» (для local это просто CreateDirectory); для zip/ssh будет своя логика в провайдере
            try
            {
                return await _fileManager.CreateDirectoryAsync(normalizedPath, string.Empty, ct);
            } catch(Exception ex)
            {
                return FileOperationResult.Fail(ex.Message);
            }
        }

    }
}
