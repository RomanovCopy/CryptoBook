# Реестр пользовательских функций CryptoBook

Дополнение этапа 7, 31 октября 2026 года: 40 ready/6 sample/30 planned, 1101 UI-ключей. **Л7** — тексты/термины/связи и конкретные доступные локальные сценарии на четырёх языках; О7 — указанная аппаратная/внешняя часть открыта. Для F090–F102 подробные независимые отметки — [VALIDATION_STAGE7.md](VALIDATION_STAGE7.md). После отдельной поправки медиаплеер переносит исходный файл в корзину Windows после предупреждения и подтверждения; при ошибке корзины файл сохраняется. Удаление из поиска содержимого по-прежнему выполняется напрямую. Этап 8 не начат.

Историческое дополнение этапа 5: 24 готовые темы, 1101 UI-ключей. Метки Л5 у редактора/содержимого/Markdown означают готовность четырёх текстов и конкретные проверки [VALIDATION_STAGE5.md](VALIDATION_STAGE5.md). Найдены и описаны Q12 (отмена изображения) и Q13 (сохранение якорей закладок); исправления этих механизмов не вносились. Следующий этап — 6.

Дополнение этапа 4, 30 октября 2026 года: восемь тем начала работы и документов готовы в четырёх культурах. **Л4** — пользовательские тексты, термины, связи, изображения и основные локальные сценарии проверены в изолированном WPF; точные проверки и ограничения (включая подстановку решений нативных диалогов, внешние приложения и защищённые форматы) — [VALIDATION_STAGE4.md](VALIDATION_STAGE4.md). Это не подтверждение всех сочетаний источников/оборудования. Исходные `К/Л0` и число ключей ниже относятся к инвентаризации; на конец этапа 4 было по 1150 UI-ключей.

Уточнения F005/F012: при конфликте создание подбирает свободное имя; `.xaml` обрабатывается как простой текст, `.XamlPackage` — как оформленный документ. Обычный текстовый экспорт — UTF-8 без BOM; новый Markdown — UTF-8 без BOM, открытый Markdown сохраняет распознанную кодировку/BOM/переводы строк. Отдельного гарантированного предупреждения о потере оформления нет. При повторном открытии уже активного пути приложение может только вернуть существующую сессию; проверка записи требует закрытия или открытия другого документа.

Инвентаризация этапа 1 от 29 октября 2026 года. База: `updates`, `933e3b04ee5b361d185221aed71ca2066b6cd1af`, версия `1.1.5.2`. Код приложения не изменялся.

Связанные материалы: [темы](TOPICS.md), [снимки](SCREENSHOTS.md), [проверки и прогресс](PROGRESS.md). Это рабочий реестр для написания справки, а не пользовательские статьи.

Обновление этапов 2–3: принята [спецификация](SPECIFICATION.md), реализовано окно, подключены два образца на четырёх языках и 28 настоящих кадров. Их отдельные статусы — в [SAMPLES.md](SAMPLES.md), фактические проверки — в [VALIDATION_STAGE3.md](VALIDATION_STAGE3.md). К исходным 154 функциям добавлены F155–F158. Исходное `Л0` ниже описывает состояние инвентаризации; для образцов его уточняет SAMPLES. Полной тематической готовности всех 158 функций нет.

## Уровень подтверждения и языки

- **К** — вход в интерфейсе и соответствующий обработчик/условия найдены в текущих исходниках; выполнение в работающем приложении в этом запуске **не проверено**. Указание существующего теста не означает его запуск.
- **П** — наличие подтверждено исходниками и проверкой пользователя в этом чате 2026-10-29; собственный аппаратный прогон на этапе 1 не выполнялся. Так отмечен перенос зашифрованного файла на Android и расшифрование прямо там с заменой исходника.
- **Д** — поведение поставки описано документацией/сценариями упаковки; свежая установка и запуск в этом запуске **не проверены**.
- **Л0** — для этой функции обязательны `en-US / ru-RU / uk-UA / de-DE`; по каждой локали текст статьи, точные термины, ссылки, изображения и сценарий имеют статус **не начат / не проверен**. Русское название в реестре не считается написанной русской статьёй. Матрица тем развёрнута в TOPICS.md.
- **Л3** — новые элементы справки проверены в изолированном видимом WPF на четырёх языках, включая сценарии и ресурсы; это не физический ввод и не готовая статья `help.use`, которая остаётся этапу 10.

Обозначение `К; Л0` в каждой строке наследует именно эти четыре языковых статуса и ограничение проверки. Колонка «Снимки» содержит ID сценариев, а не пути существующих файлов. Требуемые состояния и четыре языковых варианта перечислены в SCREENSHOTS.md.

Поддерживаемые культуры установлены по `LocalizationManager.DefaultCultureName`, `LocalizationCatalog.Create(["de", "ru", "uk"])` и преобразованию нейтральных культур в конкретные. Во всех четырёх `Resources*.resx` по 1134 ключа, наборы совпадают, пустых значений нет. Семантическая точность и полная локализация UI этим не установлены; обнаруженные жёстко заданные строки перечислены ниже.

## Источники и охват

В таблицах Rxx сокращает путь к текущим представлениям и логике. Проверялись XAML, ViewModel/Model/Behavior и регистрации, а не только README. Повтор одной команды в панели, меню и контекстном меню не требует отдельной статьи, но входы перечислены.

| Код | Источники относительно корня репозитория |
| --- | --- |
| R01 | `CryptoBook/Views/MainWindow.xaml`, `MyControls/TitleBar.xaml`, `ViewModels/TitleBarViewModel.cs`, `Models/TitleBarModel.cs`, `Services/PageNavigationService.cs` |
| R02 | `CryptoBook/Models/SideMenuModel.cs`, `MyControls/SideMenu.xaml`, `ViewModels/MenuFileViewModel.cs`, `Models/MenuFileModel.cs`, `MyPages/Home.xaml` |
| R03 | `CryptoBook/Views/NewFileDialog.xaml`, `ViewModels/NewFileDialogViewModel.cs`, `Services/DocumentTemplateCatalog.cs`, `FileTemplates/`, `Injections/Startup.cs` |
| R04 | `CryptoBook/MyControls/FontFormatBar.xaml`, `TextFormatBar.xaml`, `ListFormatBar.xaml`, соответствующие ViewModel и сервисы форматирования |
| R05 | `CryptoBook/MyControls/RichTextEditorContextMenu.xaml`, `ViewModels/RichTextContextMenuViewModel.cs`, `Services/RichTextBoxService.cs` |
| R06 | `CryptoBook/MyControls/Richtextbox.xaml`, `ViewModels/RichtextboxViewModel.cs`, `Behaviors/DocumentPageBorderBehavior.cs`, `DocumentPageKeyboardNavigationBehavior.cs` |
| R07 | `CryptoBook/MyControls/InkPalette.xaml`, `ViewModels/InkPaletteViewModel.cs`, `InkPaletteSlotViewModel.cs` |
| R08 | `CryptoBook/Behaviors/DocumentImageResizeBehavior.cs`, `Adorners/ImageResizeAdorner.cs`, `Services/EmbeddedImageEditor.cs`, `ViewModels/MenuContentViewModel.cs` |
| R09 | `CryptoBook/Behaviors/DocumentBackgroundImageBehavior.cs`, `MyControls/FontFormatBar.xaml`, сервисы фона и сохранения |
| R10 | `CryptoBook/MyControls/BookmarksBar.xaml`, `Views/BookmarksEditor.xaml`, `ViewModels/BookmarksViewModel.cs`, `BookmarksEditorViewModel.cs` |
| R11 | `CryptoBook/MyPages/Home.xaml`, `ViewModels/DocumentStructureViewModel.cs`, `Behaviors/DocumentStructureDragDropBehavior.cs` |
| R12 | `CryptoBook/MyPages/MarkdownEditor.xaml`, `ViewModels/MarkdownEditorViewModel.cs`, `Services/MarkdownDocumentState.cs`, `MarkdownFlowDocumentRenderer.cs`, `Help/HelpContentService.cs`, `docs/help/content/*/md.*.md`, `docs/MARKDOWN.md` |
| R13 | `CryptoBook/Views/FileExplorer.xaml`, `ViewModels/FileExplorerViewModel.cs`, `Models/FileExplorerModel.cs`, `Behaviors/FileExplorerDragDropBehavior.cs`, `ListViewSortedBehavior.cs` |
| R14 | `CryptoBook/Views/FileConflictDialog.xaml`, `ProgressWindow.xaml`, `DirectoryNameDialog.xaml`, `TextInputDialog.xaml`, соответствующие ViewModel/обработчики |
| R15 | `CryptoBook/ViewModels/FavoriteDirectoriesViewModel.cs`, `PinnedDocumentsViewModel.cs`, `RecentDocumentsViewModel.cs`, `MyControls/SideMenu.xaml`, `MyPages/Home.xaml` |
| R16 | `CryptoBook/Views/BatchRenameWindow.xaml`, `ViewModels/BatchRenameViewModel.cs`, `Behaviors/TextInsertionBehavior.cs`, `docs/BATCH_RENAME.md` |
| R17 | `CryptoBook/Views/FileDetailsWindow.xaml` и `.xaml.cs`, `ViewModels/FileDetailsViewModel.cs`, `docs/FILE_DETAILS.md` |
| R18 | `CryptoBook/Views/SettingsWindow.xaml`, `ViewModels/SettingsViewModel.cs`, `Models/SettingsModel.cs`, `Services/SettingsWindowService.cs` |
| R19 | `CryptoBook/MyPages/WorkspaceSearch.xaml`, `ViewModels/WorkspaceSearchViewModel.cs`; `MyControls/DocumentSearchBar.xaml`, `ViewModels/DocumentSearchViewModel.cs`, `Behaviors/DocumentSearchBehavior.cs` |
| R20 | `CryptoBook/Services/LocalStorageProvider.cs`, `WpdStorageProvider.cs`, `AndroidStorageProvider.cs`, `TransferEngine.cs`, `WindowsPortableDeviceBridge.cs`, `docs/ANDROID_STORAGE.md` |
| R21 | `CryptoBook/MyControls/ImageViewer.xaml`, `Views/MediaPlayer.xaml`, `ViewModels/MediaPlayerViewModel.cs`, `FilePreviewViewModel.cs`, `Behaviors/ImageInteractionBehavior.cs`, `VideoNavigationBehavior.cs` |
| R22 | `CryptoBook/Views/KeyInputWindow.xaml`, `EncryptionModeWindow.xaml`, `DecryptionOptionsWindow.xaml`, соответствующие ViewModel, `Behaviors/KeyInputWindowBehavior.cs`, `Models/FileExplorerModel.cs` |
| R23 | `CryptoBook/Services/AutoKeyResetService.cs`, `LockSnapshotService.cs`, `DocumentRecoveryService.cs`, `DocumentBackupRecoveryService.cs`, `Views/UnlockWindow.xaml`, `Behaviors/WorkspaceLockPresentation.cs` |
| R24 | `CryptoBook/MyControls/AiAssistantPanel.xaml`, `AiRuntimeDetails.xaml`, `AI/AiAssistantViewModel.cs`, `Views/AiModelSettingsWindow.xaml` и `.xaml.cs`, `AI/AiModelSettingsViewModel.cs`, `AiPromptTemplatesViewModel.cs`, `docs/LOCAL_AI.md`, `docs/AI_TABLE_EDITING.md` |
| R25 | `CryptoBook/Views/AiChatAppearanceWindow.xaml`, `AI/AiChatAppearanceViewModel.cs`, `AI/AiSettingsStore.cs`, `AI/PromptTemplates.json` |
| R26 | `CryptoBook/ViewModels/UpdateNotificationViewModel.cs`, `Services/ApplicationUpdateInstaller.cs`, `Views/MainWindow.xaml`, `SettingsWindow.xaml`, `README.ru.md`, `README.md`, `CHANGELOG.md`, `installer/` |
| R27 | `CryptoBook/Views/HelpWindow.xaml`, `Help/HelpWindowViewModel.cs`, `Help/HelpContentService.cs`, `Help/HelpWindowService.cs`, `Help/HelpWindowBehavior.cs`, `Help/HelpShortcutBehavior.cs`, `Help/HelpPreferences.cs`, `Properties/HelpSettings.cs`; `docs/help/content/` |

Префикс `CryptoBook/` в строке источника применяется также к следующим сокращённым путям той же строки. Для криптографии/обновления это инвентаризация пользовательских входов, не аудит безопасности или проверка текущих внешних сервисов.

## Начало работы и документы

| ID | Пользовательское действие | Точный вход / источник | Условия, форматы и источники | Тема | Снимки | Проверка; языки |
| --- | --- | --- | --- | --- | --- | --- |
| F001 | Установить или запустить portable | Установщик либо exe распакованной поставки; R26 | Windows x64, целевая Windows 10 1809+; запуск/ассоциации требуют отдельной тестовой среды | `start.install` | S01 | Д; Л0 |
| F002 | Найти основные области окна | Начальный экран, заголовок, меню, область редактора, нижняя строка; R01/R02/R06 | Начальный экран и редактор различаются по состоянию документов; общей справки пока нет | `start.interface` | S02 | К; Л0 |
| F003 | Меню, история страниц и управление окном | Заголовок: ToggleMenu_Click, ButtonBack_Click, ButtonForward_Click, ButtonSettingsClick, MinButtonClick, MaxButtonClick, GoToWindow, CloseButtonClick; перетаскивание/двойной щелчок MouseLeftButtonDown/TitleBarDoubleClick; R01 | Назад/Вперёд — навигация страниц приложения; отдельная история файлового менеджера | `start.interface` | S02 | К; Л0 |
| F004 | Выбрать рабочую директорию | Начальный экран → «Выбрать рабочее пространство»; Настройки → Рабочее пространство → «Выбрать...» (ChooseWorkspace); R02/R18 | Локальная папка для поиска; не синоним текущей папки FileExplorer | `start.workspace` | S03 | К; Л4 |
| F005 | Создать файл с нужным именем/типом | Начальный экран «Новый документ» (NewDocument), быстрый «Создать» меню, FileExplorer «Создать файл» → NewFileDialog; R02/R03/R13 | Папка/Browse, тип/SelectedNewTemplate, расширение/SelectedNewExtension, имя, Create/Cancel; только CanCreate-типы | `doc.create` | S04 | К; Л4 |
| F006 | Выбрать содержимое нового документа | Диалог создания → «Шаблон документа»; Настройки → Оформление → шаблон по умолчанию; R03/R18 | Шесть ID: empty, notes, plan, meeting, journal, report; применимость к типу проверять в диалоге | `doc.templates` | S04,S46 | К; Л4 |
| F007 | Выбрать размер листа и ориентацию | NewFileDialog → «Формат листа», «Ориентация»; R03 | Книжная/альбомная, непрерывная высота. Не найдена отдельная команда изменения параметров уже открытого документа | `edit.page` | S04 | К; Л0 |
| F008 | Назначить атрибуты создаваемого файла | NewFileDialog → «Только для чтения», «Скрытый»; R03 | Условия и поддержка назначения зависят от источника; не обещать одинаковые атрибуты MTP и NTFS | `doc.create` | S04 | К; Л4 |
| F009 | Открыть документ | Начальный экран «Открыть файл», быстрый «Открыть» → FileExplorer; файловая ассоциация; R02/R03/R13 | Текст/RTF/XAML/XamlPackage/cbook/Markdown; медиа и PDF маршрутизируются отдельно; замена только соответствующего редактора | `doc.open` | S05,S07 | К; Л4 |
| F010 | Сохранить изменения | Быстрый «Сохранить», Файл → «Сохранить», Ctrl+S в редакторе; SaveFile/SaveDocument; R02/R06/R12 | Активный документ, допустимость сохранения/занятость; защищённому нужен ключ; кодировка определяется форматом | `doc.save` | S06 | К; Л4 |
| F011 | Сохранить под другим именем/форматом | Файл → «Сохранить как»; Ctrl+Shift+S; кнопка Markdown; SaveAsFile/SaveDocumentAs; R02/R06/R12 | Системный диалог, подтверждение перезаписи/потери оформления; отмена сохраняет текущую сессию | `doc.save` | S06 | К; Л4 |
| F012 | Выбрать подходящий формат и понимать кодировку | Поля типа/расширения создания и Save As; R03/R12 | TXT и другие текстовые расширения не сохраняют оформление; отдельной доступной команды выбора произвольной кодировки не найдено | `doc.formats` | S04,S06 | К; Л4 |
| F013 | Переключить обычный и Markdown-документ | Меню → Рабочее пространство → начальная страница/Markdown; Назад/Вперёд заголовка; R01/R02/R12 | Документы независимы; вход Markdown доступен при его наличии; сохранение относится к выбранному | `doc.switch` | S07 | К; Л4 |
| F014 | Переименовать текущую книгу | Меню → Файл → «Переименовать книгу», renameBookCommand → TextInputDialog; R02/R14 | Для несохранённого меняется имя сессии; для сохранённого — файл и связанные пути/история; расширение сохраняется | `doc.switch` | S07 | К; Л4 |
| F015 | Закрыть документ или приложение | Быстрый «Закрыть» / CloseFile; крестик окна; R01/R02 | Сохранить/не сохранять/отмена при изменениях; закрытие программы проверяет оба редактора | `doc.switch` | S06,S07 | К; Л4 |

## Обычный редактор, содержимое и Markdown

| ID | Пользовательское действие | Точный вход / источник | Условия, форматы и источники | Тема | Снимки | Проверка; языки |
| --- | --- | --- | --- | --- | --- | --- |
| F016 | Отменить/повторить | Контекстное меню → Отменить/Повторить; ApplicationCommands.Undo/Redo; Ctrl+Z/Y; R05 | Доступность по истории изменений; не обещать откат файловых операций через редактор | `edit.basic` | S08 | К; Л5 |
| F017 | Выделить всё, вырезать/копировать/вставить | Контекстное меню/стандартные клавиши; ApplicationCommands.SelectAll/Cut/Copy/Paste; R05 | Фокус редактора, выделение, буфер обмена и режим редактирования | `edit.basic` | S08 | К; Л5 |
| F018 | Вставить перенос строки или абзац | Контекстное меню, Enter/Control+Enter; EditingCommands.EnterLineBreak/EnterParagraphBreak; R05 | Обычный редактор: Enter — перенос; Ctrl+Enter — абзац. Это не команда отправки AI | `edit.basic` | S08 | К; Л5 |
| F019 | Очистить документ / удалить пустые абзацы | Контекстное меню → соответствующие пункты; ClearDocument/RemoveEmptyParagraphs; R05 | Редактируемый обычный документ; важность отмены и выделения проверить на примере | `edit.basic` | S08 | К; Л5 |
| F020 | Изменить шрифт | Панель «Шрифт»: SetFontFamilyCommand, SetFontStyleCommand, SetFontWeightCommand, SetTextDecorationCommand, SetFontStretchCommand, SetFontSizeCommand; R04 | Гарнитура/стиль/начертание/декор/ширина/размер; выделение либо ввод с каретки | `edit.font` | S08 | К; Л5 |
| F021 | Изменить цвет и выделение текста | «Шрифт» → Цвет/Выделение; SetFontColorCommand/SetFontBackgroundCommand; R04 | Для сохранения оформления нужен соответствующий формат | `edit.font` | S08 | К; Л5 |
| F022 | Быстрое жирное/курсив/подчёркивание, очистка оформления | Контекстное меню → Форматирование; FontFormatting и ClearFormattingCommand; Ctrl+B/I/U | Наличие сочетаний заявлено UI; маршрутизацию с реальным фокусом проверить на этапе 5 | `edit.font` | S08 | К; Л5 |
| F023 | Применить и настроить палитру чернил | Палитра над документом: ApplyCommand левой кнопкой, Choices/ChooseCommand правой; R07 | Цвет слота сохраняется отдельно; смена темы не перекрашивает содержимое документа | `edit.palette` | S09 | К; Л5 |
| F024 | Выровнять абзац | «Абзац» либо контекст → Выравнивание; SetTextAlignment Left/Center/Right/Justify; R04/R05 | По левому краю/центру/правому/ширине | `edit.paragraph` | S09 | К; Л5 |
| F025 | Задать отступ первой строки | «Абзац» → «Отступ первой строки», SetParagraphIndent; R04 | Относится к абзацу обычного документа | `edit.paragraph` | S09 | К; Л5 |
| F026 | Изменить межстрочный интервал | «Абзац» → увеличить/уменьшить, SetLineHeight; R04 | Кнопки используют один обработчик, параметры/результат проверить живым действием | `edit.paragraph` | S09 | К; Л5 |
| F027 | Маркированный/нумерованный/убрать список | Панель «Списки» и контекст → Списки; ToggleBulleted/ToggleNumbered/ClearLists; R04/R05 | Обычный редактор, актуальный абзац/выделение | `edit.paragraph` | S09 | К; Л5 |
| F028 | Вставить/открыть гиперссылку | «Абзац» → InsertHyperlink → HyperlinkDialog (InsertButton_Click/CancelButton_Click); OpenHyperlink; R04/R06 | URL и отображаемый текст; допустимость перехода зависит от рендерера, ссылки на закладки отдельно | `edit.links` | S10 | К; Л5 |
| F029 | Изменить цвет бумаги | «Шрифт» → «Бумага», SetDocumentBackgroundCommand; R04 | Цвет документа, не тема интерфейса | `edit.page` | S08,S13 | К; Л5 |
| F030 | Масштабировать и сбросить 100% | Нижняя строка редактора, ResetZoomCommand; Ctrl+колесо; R06 | Обычный редактор; просмотр имеет свои масштаб/вписывание | `edit.view` | S11 | К; Л5 |
| F031 | Читать документ по страницам | Переключатель режима над документом → ToggleView; ToggleFitToWindow в просмотре; R06 | Редактирование выключено; клавиши страниц применяются к viewer | `edit.view` | S11 | К; Л5 |
| F032 | Вставить изображение | Меню → Содержимое → «Вставить изображение»; контекст → Изображение; InsertImage; R02/R05/R08 | Обычный документ; прямой вход отключён при активном Markdown | `content.images` | S12 | К; Л5 |
| F033 | Выделить и изменить размеры изображения | Щелчок по встроенному изображению, маркеры ImageResizeAdorner; R08 | Доступно в редактировании; точный набор маркеров/пропорций проверить визуально | `content.images` | S12 | К; Л5 |
| F034 | Переместить изображение | Перетаскивание, кнопки/контекст «выше/ниже»; MoveImageUpCommand/MoveImageDownCommand, Alt+↑/↓; R08 | Выбрано изображение, оно ещё принадлежит документу | `content.images` | S12 | К; Л5 |
| F035 | Выбрать размещение изображения | Контекст → Изображение → SetImageLayoutCommand: Inline/CenteredBlock/FloatLeft/FloatRight; R05/R08 | В строке, блок по центру, обтекание слева/справа; ограничения контейнера проверять | `content.images` | S12 | К; Л5 |
| F036 | Удалить выбранное изображение / снять выбор | Delete / Esc обработчика DocumentImageResizeBehavior; R08 | Не путать с удалением файла на диске; отмена редактора | `content.images` | S12 | К; Л5 |
| F037 | Выбрать/удалить/переместить фон | «Шрифт» → ChooseDocumentBackgroundImageCommand/ClearDocumentBackgroundImageCommand; Ctrl+ЛКМ и перетаскивание; Esc отменяет; R09 | Обычный документ в редактировании, кадрирование фона | `content.background` | S13 | К; Л5 |
| F038 | Подтвердить фиксацию фона при сохранении | Запрос пути сохранения документа с редактируемым фоном; R09 | Отдельно от выбора фона; проверить точный диалог и поддержку формата | `content.background` | S13,S06 | К; Л5 |
| F039 | Добавить/удалить строку или столбец | Контекст → Таблица → EditTable: InsertRowAbove/Below, InsertColumnLeft/Right, DeleteRow/Column; R05 | Прямоугольная таблица без объединений, одна ячейка, редактирование; последняя строка/столбец удаляет таблицу; Undo/Redo | `content.tables` | S14 | К; Л5 |
| F040 | Создать закладку | Панель «Закладки» → имя → AddAtCaret; R10 | Позиция каретки обычного документа | `content.bookmarks` | S15 | К; Л5 |
| F041 | Выбрать/открыть соседнюю закладку | Панель → список, NavigateTo, PreviousBookmark/NextBookmark; R10 | Нужен существующий якорь | `content.bookmarks` | S15 | К; Л5 |
| F042 | Переименовать/удалить закладку | Панель либо диспетчер: RenameTo/Rename/Remove; R10 | Выбрана закладка; не удаляет файл | `content.bookmarks` | S15 | К; Л5 |
| F043 | Вставить ссылку на закладку | Панель/диспетчер → LinkText/InsertHyperlinkTo; R10 | Можно заранее выделить текст; результат — внутренняя ссылка | `content.bookmarks` | S10,S15 | К; Л5 |
| F044 | Открыть диспетчер и изменить заметку | Меню → Содержимое → Закладки; панель OpenManager → SelectedBookmark.Note; R02/R10 | Выбор, двойной щелчок/переход, свойства и Close | `content.bookmarks` | S15 | К; Л5 |
| F045 | Перестроить индекс закладок | Панель/диспетчер → «Обновить», RebuildIndexFromDocument; R10 | Сверка с текущим документом | `content.bookmarks` | S15 | К; Л5 |
| F046 | Показать/обновить дерево структуры | Над документом → «Структура», ToggleCommand/RefreshCommand, IncludeTextElements; R11 | Показывать/скрывать Run/Span; чтение ограничивает действия | `content.structure` | S16 | К; Л5 |
| F047 | Перейти к элементу структуры | Выбор дерева / контекст → NavigateCommand; R11 | Навигация и изменение отключены в режиме просмотра | `content.structure` | S16 | К; Л5 |
| F048 | Добавить абзац через структуру | Кнопка + / Ctrl+Enter, контекст → AddParagraphCommand/AddParagraphBeforeCommand/AddParagraphInsideCommand/AddParagraphAfterCommand; R11 | Допустимый родитель и выбранный узел | `content.structure` | S16 | К; Л5 |
| F049 | Переставить/удалить элемент структуры | Контекст → MoveUpCommand/MoveDownCommand/DeleteCommand; Alt+↑/↓; drag-and-drop → MoveCommand; R11 | Проверять допустимость вложенности/переноса; не обещать произвольное изменение дерева | `content.structure` | S16 | К; Л5 |
| F050 | Редактировать и просматривать Markdown | Отдельная страница MarkdownEditor, MarkdownText, ToggleView; R12 | .md и защищённый Markdown; просмотр не конвертируется обратно в исходник | `md.edit` | S17 | К; Л5 |
| F051 | Открыть справку Markdown | Кнопка с MarkdownHelp.OpenHint → OpenSyntaxHelp → IHelpWindowService.Open(md.syntax); R12/R27 | Отдельное немодальное окно, основная страница/оба документа/preview остаются; закрытие Alt+F4 при фокусе справки, Esc обслуживает изображение/поиск | `md.edit` | S17 | К; Л5 |
| F052 | Использовать поддерживаемый синтаксис | Markdown-источник и md.syntax (40 сохранённых примеров); R12 | ATX/Setext, списки, цитаты, разделители, bold/italic/strike, ссылки, inline/indented/fenced code, pipe/grid-таблицы | `md.syntax` | S17 | К; Л5 |
| F053 | Вставлять безопасные ссылки/изображения Markdown | Исходная разметка → просмотр; R12 | Активны http/https/mailto; изображения только локальные относительные, до 25 МиБ, декодирование до 1600 px; HTML неактивен | `md.preview` | S17 | К; Л5 |
| F054 | Сохранить Markdown без потери исходника | Кнопки SaveDocument/SaveDocumentAs, Ctrl+S/Shift+Ctrl+S; R12 | Строка/BOM/кодировка/переводы строк сохраняются; экспорт обычного FlowDocument в .md недоступен | `md.save` | S06,S17 | К; Л5 |

## Файловый менеджер и дополнительные операции

| ID | Пользовательское действие | Точный вход / источник | Условия, форматы и источники | Тема | Снимки | Проверка; языки |
| --- | --- | --- | --- | --- | --- | --- |
| F055 | Открыть FileExplorer в нужном режиме | «Открыть файл» и выбор папки из диалогов, команды файловых сервисов; R02/R03/R13 | Обычный режим/SelectFile/SelectFolder различаются; ConfirmSelectionCommand/CancelSelectionCommand вместо внешнего открытия | `files.create` | S05 | К; Л6 |
| F056 | Перейти по дискам и папкам | Дерево/двойной щелчок, BackCommand/ForwardCommand/UpCommand, TreeViewItemSelectedCommand/ListViewItemDoubleClickCommand; R13 | Локальные диски и корни подключённых провайдеров; история менеджера | `files.navigate` | S18 | К; Л6 |
| F057 | Ввести адрес и отменить ввод | Адресная строка AddressText, ApplyAddressCommand/Enter, CancelAddressCommand/Esc; R13 | Адрес обрабатывается текущим провайдером; не считать mtp locator обычным путём Windows | `files.navigate` | S18 | К; Л6 |
| F058 | Показать папку текущего документа | Панель → CurrentDocumentCommand; R13 | У документа должен быть путь с доступным родителем | `files.navigate` | S18 | К; Л6 |
| F059 | Обновить/повторить недоступную папку | RefreshCommand панели/контекстов; RetryNavigationCommand и «Вверх» в состоянии ошибки; R13 | Отключение устройства/недоступный каталог блокируют связанные операции | `files.navigate` | S18 | К; Л6 |
| F060 | Сортировать и менять размеры представления | Заголовки Имя/Дата/Тип/Размер и Папка в плоском виде; ListViewSortedBehavior/SortedCommand, ширины столбцов и разделители; R13 | Счётчик batch-rename использует порядок ChildrenView | `files.views` | S18 | К; Л6 |
| F061 | Показать все вложенные файлы | Переключатель «Все файлы» / IsFlatViewEnabled, CancelFlatViewScanCommand; R13 | Рекурсивное представление, состояние сканирования; в SelectFolder недоступно | `files.views` | S18 | К; Л6 |
| F062 | Отфильтровать список | Поле «Фильтр» / FilterText; R13 | Имя или папка текущего представления; не поиск содержимого | `files.views` | S18 | К; Л6 |
| F063 | Выбрать один/несколько файлов | Список FileExplorer, Ctrl/Shift/мышь; SelectedItemsSnapshot; R13 | Снимок выделения передаётся командам; дерево, фон и строка имеют разные контекстные меню | `files.views` | S19 | К; Л6 |
| F064 | Открыть выбранное | Контекст дерева/строки → OpenCommand, кнопка «Открыть», двойной щелчок; R13 | Один объект; внешнее открытие файла требует OpenExternally, режим выбора имеет отдельную логику | `files.manage` | S05,S19 | К; Л6 |
| F065 | Выбрать внешнее приложение | Контекст строки → OpenWithCommand; R13 | Один файл, поддержка внешнего открытия; у MTP такой возможности провайдера нет | `files.manage` | S19 | К; Л6 |
| F066 | Показать в Проводнике / скопировать путь | Контексты дерева/фона/строки → RevealInExplorerCommand / CopyPathCommand; R13 | Проводник только для локального объекта; несколько путей допускаются для копирования | `files.manage` | S19 | К; Л6 |
| F067 | Создать папку/файл в текущем источнике | Панель/контекст фона → CreateDirectoryCommand/CreateFileCommand; DirectoryNameDialog CreateButton_Click/CancelButton_Click; R13/R14 | Запись/CreateContainer, существующая выбранная папка; ошибки имени/дубликата | `files.create` | S04,S18 | К; Л6 |
| F068 | Переименовать один объект | Панель/контекст → RenameClickCommand, ввод имени → RenameCommand/Enter или CancelRenameCommand/Esc; R13 | Один объект, не корень диска, поддержка Rename, не уже редактируемый; сохранение прежнего расширения при вводе нового имени без него | `files.manage` | S21 | К; Л6 |
| F069 | Удалить выбранные объекты | Панель/контексты → DeleteCommand; R13 | Возможность Delete; подтверждение для всех источников; локально корзина Windows; RestoreFilesCommand активна только при остановленном журнале прерванной операции; удалённые — предупреждение о постоянном удалении | `files.manage` | S21 | К; Л6 |
| F070 | Копировать/вырезать файлы | Панель/контекст → CopyCommand/CutCommand; R13 | Copy требует Read, Cut — Read+Delete; несколько выбранных | `files.transfer` | S19 | К; Л6 |
| F071 | Вставить файлы | Панель/контекст фона → PasteCommand; R13 | В буфере есть источники, текущая папка доступна для записи | `files.transfer` | S19 | К; Л6 |
| F072 | Переместить через выбор папки | Контекст → MoveCommand → выбор назначения; R13 | Нужны Read+Delete и родитель; нельзя переносить корень/в себя/в потомка | `files.transfer` | S19 | К; Л6 |
| F073 | Перетащить файлы между папками | FileExplorerDragDropBehavior дерева/списка → DropCommand; R13 | Ctrl выбирает Copy, иначе Move; проверяется назначение и допустимость источников | `files.transfer` | S19 | К; Л6 |
| F074 | Разрешить конфликт назначения | FileConflictDialog → ReplaceCommand/SkipCommand/KeepBothCommand/CancelCommand, ApplyToAll; R14 | Заменить/пропустить/сохранить оба/отменить; флажок ко всем оставшимся конфликтам | `files.conflicts` | S20 | К; Л6 |
| F075 | Следить за операцией и отменить | ProgressWindow → Progress/StatusMessage/Canceled; закрытие окна; R14 | Кнопка доступна пока операция идёт; локальная отмена/ошибка возвращает весь пакет по журналу; для устройств действуют отдельные правила | `files.transfer` | S19,S20 | К; Л6 |
| F076 | Избранные папки | Звезда Favorites.AddCurrentDirectoryCommand; панель избранного OpenCommand/RenameCommand/RemoveCommand; R13/R15 | Переименовывается ссылка в избранном, удаление ссылки не удаляет папку; недоступность показывается | `files.favorites` | S22 | К; Л6 |
| F077 | Закрепить/открепить текущий документ | Боковое меню → PinnedDocuments.ToggleCurrentCommand, UnpinCommand; R15 | Нужен документ с пригодным путём; индикация текущего/несохранённого/недоступного | `files.pinned` | S23 | К; Л6 |
| F078 | Управлять закреплённой ссылкой | Боковое меню → OpenCommand/RefreshAvailabilityCommand/RevealCommand/RelocateCommand/MoveUpCommand/MoveDownCommand/UnpinCommand; Home → Открыть/Найти…; R15 | Проверить доступность, Проводник, новый путь, порядок; запрос несохранённого при переключении | `files.pinned` | S23 | К; Л6 |
| F079 | Недавние документы | Home → RecentDocuments.OpenCommand/RelocateCommand/RemoveCommand; R15 | История после успешного открытия/сохранения; Remove удаляет запись, не файл | `files.recent` | S23 | К; Л6 |
| F080 | Открыть групповое переименование | Ctrl+M, A→B панели или контекст → BatchRenameCommand; R13/R16 | Не менее двух локальных файлов, не папок/Android, не в редактировании; порядок видимого списка | `files.batch` | S24 | К; Л6 |
| F081 | Настроить маски имени и расширения | BatchRenameWindow → NameMask/ExtensionMask; кнопки TextInsertionBehavior [N]/[E]/[C]/[DMY]/[HMS]; R16 | Вставка в каретку/выделение; диапазоны N/E, расширение без точки; дата/время фиксируются предпросмотром | `files.batch` | S24 | К; Л6 |
| F082 | Настроить счётчик | CounterStart/CounterStep/CounterDigits; R16 | Начало/шаг/разрядность; счёт в порядке ChildrenView | `files.batch` | S24 | К; Л6 |
| F083 | Найти/заменить и изменить регистр имён | Search/Replacement/UseRegex/MatchCase/CaseIndex; R16 | Замена во всём новом имени с расширением, затем регистр; regex .NET с группами | `files.batch` | S24 | К; Л6 |
| F084 | Проверить будущие имена/конфликты | Предпросмотр Было/Станет/Ошибка, RefreshPreviewCommand; R16 | Дубликаты, Windows-имена, внешние конфликты блокируют Apply; обновление меняет время масок | `files.batch` | S24 | К; Л6 |
| F085 | Применить/отменить пакет имён | ApplyCommand/CancelCommand/Close; R16 | Журнал, временные файлы в скрытых .cryptobook-operation-*; возврат при отмене/ошибке, повтор через RestoreFilesCommand после перезапуска | `files.batch` | S24 | К; Л6 |
| F086 | Откатить последнее переименование | UndoCommand пока окно открыто; R16 | Последний успешный пакет, повторная проверка конфликтов; пути документа/закреплённых/недавних обновляются по результату | `files.batch` | S24 | К; Л6 |
| F087 | Обычные и подробные свойства | Контекст → PropertiesCommand/DetailedPropertiesCommand; Alt+Enter списка для подробных; R13/R17 | Один объект; Windows-свойства отдельны; локальный отчёт шире удалённого, .cbook не раскрывает содержимое | `files.properties` | S25 | К; Л6 |
| F088 | Рассчитать хеши/обновить/остановить | FileDetailsWindow → RefreshCommand/HashCommand/CancelCommand; R17 | SHA-256/SHA-512 за один проход; локальная копия; блокировка/изменение файла дают ошибку | `files.properties` | S25 | К; Л6 |
| F089 | Найти и скопировать свойства | SearchText, CopySelected_Click/CopyReport_Click/Close_Click; R17 | Фильтр по разделу/имени/значению/ключу; полный отчёт не сокращается фильтром; пропущенные поля отмечены | `files.properties` | S25 | К; Л6 |

## Поиск, устройства, просмотр и печать

| ID | Пользовательское действие | Точный вход / источник | Условия, форматы и источники | Тема | Снимки | Проверка; языки |
| --- | --- | --- | --- | --- | --- | --- |
| F090 | Искать по имени | Настройки → Рабочее пространство → SearchQuery/Search/Enter; OpenSearchResult/RevealSearchResult; R18 | Выбранная рабочая папка; повторный поиск отменяет предыдущий; отдельной кнопки отмены здесь нет | `search.name` | S03 | К; Л7 |
| F091 | Искать по содержимому | Боковое меню → Рабочее пространство → «Поиск в документах», WorkspaceSearch.Search/CancelSearch; R19 | Поддерживаемые локальные документы рабочего пространства; защищённые при доступном ключе; статус/текущий файл | `search.workspace` | S26 | К; Л7 |
| F092 | Действия с результатами содержимого | OpenResult/DeleteResult/ClearPage/ClosePage; возврат заголовком; R19 | Удаление результата — прямое удаление файла после подтверждения, без корзины; очистка страницы и удаление файла различаются | `search.workspace` | S26 | К; Л7 |
| F093 | Найти текст текущего документа | Кнопка «Найти в документе»/Ctrl+F → Query/MatchCase/Previous/Next/Close; R06/R12/R19 | Обычный: поиск из чтения возвращает в редактор; Markdown: source/preview; Enter/F3, Shift+Enter/Shift+F3, Esc в области поиска | `search.document` | S27 | К; Л7 |
| F094 | Подключить Android как дополнительный источник | Дерево FileExplorer, корни WPD/MTP; необязательный ADB-провайдер; R20 | Общая память, доступная Windows MTP; ADB — отдельная конфигурация, не требование обычного подключения | `devices.android` | S28 | К; Л7; О7 |
| F095 | Передавать/перемещать файлы Android | Обычные команды FileExplorer и TransferEngine; R13/R20 | Локально↔устройство, внутри устройства, между устройствами; копирование/проверка перед удалением при move; отмена/обрыв ещё требуют собственного прогона | `devices.transfers` | S19,S29 | П (помещение файла); Л7; О7 |
| F096 | Расшифровать файл прямо на Android | Контекст → «Расшифровать выбранные элементы» → формат/«Заменить исходник»; CanExecute_DecryptCommand, ExecuteSingleFileDecryptionAsync; R13/R20/R22 | Удалённый файл Read, ключ; для замены Write+Delete. Пользователь проверил перенос зашифрованного файла и расшифрование на месте. Encrypt capability провайдера для этого пути не требуется | `devices.limits` | S29,S35 | П; Л7; О7 |
| F097 | Просмотреть файл без внешнего открытия | Правая панель FileExplorer, FilePreviewViewModel; R13/R20/R21 | Ограниченные текст/поддерживаемые изображения через поток провайдера; неподдерживаемые/слишком большие — явное ограничение | `view.preview` | S30 | К; Л7 |
| F098 | Просматривать изображения | Медиаплеер/ImageViewer → PreviousImageCommand/NextImageCommand/RotateImageCommand/ResetImageTransformCommand/DeleteCurrentImageCommand; R21 | Стрелки ←/→, ↑/↓ масштаб, отдельная кнопка поворота, Delete; колесо — масштаб, мышь — смещение. Удаление после предупреждения и подтверждения переносит исходный файл в корзину Windows; при ошибке файл сохраняется. Восстановление — из корзины Windows | `view.images` | S30 | К; Л7 |
| F099 | Воспроизводить видео и переходить к соседним | Home → OpenMediaPlayer, меню → Содержимое → MediaPlayer; в плеере OpenFileCommand, Flyleaf VideoView, PreviousVideoCommand/NextVideoCommand; VideoNavigationBehavior; R02/R21 | ←/→ перемотка, Alt+←/→ соседний файл; список контейнеров не гарантирует любой кодек | `view.video` | S31 | К; Л7; О7 |
| F100 | Несколько плееров | OpenFileInNewWindowCommand/PauseAllCommand/ToggleSynchronizationCommand; R21 | Пауза всех, экспериментальная синхронизация; обычные элементы Flyleaf требуют проверки реального проигрывания | `view.video` | S31 | К; Л7; О7 |
| F101 | Открыть PDF | Маршрутизация PdfFileTemplate с внешним открытием; R03 | PDF не создаётся как редактируемый документ; зависит от внешнего приложения/ассоциации | `view.pdf` | S32 | К; Л7; О7 |
| F102 | Напечатать текущий документ | Меню → Файл → PrintFile → системный диалог; R02 | Непустое печатаемое содержимое; активный редактор, включая подготовку Markdown; физическая печать не проверена | `view.print` | S32 | К; Л7; О7 |

## Защита и восстановление

| ID | Пользовательское действие | Точный вход / источник | Условия, форматы и источники | Тема | Снимки | Проверка; языки |
| --- | --- | --- | --- | --- | --- | --- |
| F103 | Ввести/изменить ключ | Настройки → Безопасность → OpenEncryptionKeyDialog; FileExplorer → EncryptingKeyCommand; автоматический запрос при открытии; R18/R22 | Ввод существующего ключа и создание нового имеют разные проверки; показ/скрытие/повтор, OK/Cancel/Enter через KeyInputWindowBehavior | `security.password` | S33 | К; Л0 |
| F104 | Понять ошибки ввода пароля | KeyInputWindowBehavior: пусто/повтор/несовпадение/длина/сила; R22 | В XAML MinLength=1 и AllowWhiteSpace=True; фактические требования зависят от режима. Не писать единую придуманную минимальную длину | `security.password` | S33 | К; Л0 |
| F105 | Создать проверенные защищённые копии | Панель/контекст FileExplorer → CreateProtectedCopyCommand; R13/R22 | Ключ, выбранные файлы с Encrypt; результат .cbook отдельно от исходника; папки не этот вход | `security.protect` | S34 | К; Л0 |
| F106 | Зашифровать файл/папку с выбором назначения | Контекст → EncryptCommand → EncryptionModeWindow SaveAs/ReplaceSource, ButtonOk/ButtonCancel; R13/R22 | Возможность Encrypt; целый диск запрещён; перед заменой подтверждение | `security.protect` | S34 | К; Л0 |
| F107 | Обработать несколько объектов защитой | EncryptCommand/DecryptCommand для выделения; R13/R22 | Шифрование пакета заменяет источники после отдельного подтверждения; расшифрование имеет собственный путь выбора назначения; прогресс/отмена | `security.protect` | S34,S35 | К; Л0 |
| F108 | Расшифровать с выбором формата/назначения | DecryptCommand → DecryptionOptionsWindow: SelectedOutputFormat Rtf/PlainText/Original, SelectedTargetMode SaveAs/ReplaceSource, ButtonOk/Cancel; R22 | Набор форматов зависит от исходного содержимого; Write+Delete для замены, предупреждение TXT; поддержан удалённый файл, см. F096 | `security.decrypt` | S35,S29 | К; Л0 |
| F109 | Немедленно сбросить ключ | Ctrl+L главного окна/настроек; Настройки → Безопасность → ResetEncryptionKey; R18/R23 | Снимок/проверка перед очисткой, блокировка пространства, прекращение AI; ошибки нельзя маскировать успехом | `security.lock` | S36 | К; Л0 |
| F110 | Настроить автоматический сброс | Настройки → Безопасность → SelectedKeyResetInterval; R18/R23 | 1/5/10/15/30 минут или «Никогда»; таймер при наличии ключа; Windows lock/сон — отдельные причины сброса | `security.lock` | S36 | К; Л0 |
| F111 | Разблокировать рабочее пространство | Блокирующий слой MainWindow → разблокировать/закрыть; ввод ключа; R23 | Не путать с восстановлением забытого пароля; нужный ключ от снимка, не пароль Windows | `recovery.lock` | S36 | К; Л0 |
| F112 | Открывать защищённые и старые форматы | Открытие/сохранение SecureFileTemplate и валидаторы содержимого; R03/R22 | Совместимость проверять на конкретных тестовых файлах, не по расширению; весь набор старых версий в этом запуске не проверен | `security.compatibility` | S34,S35 | К; Л0 |
| F113 | Восстановить предыдущую сохранённую версию | Меню → Файл → «Восстановить резервную копию», restoreBackupCommand; R02/R23 | Нужен файл `<путь>.bak`; подтверждение, при защите — ключ; открывается как изменённый, сохранить явно | `recovery.backup` | S37 | К; Л0 |
| F114 | Восстановить аварийный снимок | Обычный запуск и диалог обнаруженного recovery; R23 | Отдельный DPAPI-защищённый механизм; восстановление/отложить/отказ — по фактическому диалогу, не команда .bak | `recovery.crash` | S37 | К; Л0 |
| F115 | Восстановить lock-снимок | Ввод ключа/UnlockWindow (OnOpen) при наличии снимка; R23 | Оба документа, включая скрытый; ошибочный ключ не объявляет содержимое утраченным; поведение повторного запуска проверить | `recovery.lock` | S36,S37 | К; Л0 |
| F116 | Продолжить после ошибки записи/отмены | Сообщения сохранения, операции и восстановления; R14/R23 | Блокировка, нет места, отмена, отказ доступа; различать файл на диске, текущий текст и снимки | `recovery.write` | S06,S37 | К; Л0 |

## Локальный AI: команды и все видимые настройки

| ID | Пользовательское действие | Точный вход / источник | Условия, форматы и источники | Тема | Снимки | Проверка; языки |
| --- | --- | --- | --- | --- | --- | --- |
| F117 | Включить AI | Настройки → AI-помощник / Настройки модели → Enabled; R18/R24 | GGUF не входит в поставку; включение не означает, что модель уже загружена | `ai.start` | S38 | К; Л0 |
| F118 | Открыть/закрыть/расширить панель | Кнопка AiAssistantButton внизу обычного редактора; Настройки → AI → OpenPanel; панель Toggle и левая граница PanelResizeBehavior; R06/R18/R24 | В TitleBar текущей кнопки AI нет; видимость/блокировка зависят от состояния; глобальная ViewModel | `ai.start` | S02,S38 | К; Л0 |
| F119 | Выбрать модель | Настройки/панель AI → OpenSettings → ModelPath/Browse/RecentModels/SelectedRecentModel; R24 | Языковой GGUF; mmproj не выбирается как языковая модель; параметры запоминаются по файлу модели | `ai.models` | S38 | К; Л0 |
| F120 | Проверить и подобрать модель | «Проверить модель»/«Подобрать заново» → TestWindow/Test и RetuneWindow/Retune; StopTest; R24 | Проверка текущих вводимых значений после валидации; результат/ошибка/отмена показаны | `ai.models` | S38,S44 | К; Л0 |
| F121 | Применить/отменить настройки модели | SaveWindow/Save, CloseWindow/Отмена; R24 | CommitInputs/BindingGroup, недопустимые значения не применяются; изменение ряда параметров может выгрузить модель/очистить чат | `ai.models` | S38 | К; Л0 |
| F122 | Узнать состояние загрузки | Панель: ModelName/Status/Runtime, «Работа модели»; R24 | Загружается/загружена/выгружена; обычное окончание ответа сохраняет загруженные веса | `ai.lifecycle` | S39 | К; Л0 |
| F123 | Остановить генерацию/загрузку | Панель Stop; тест настроек StopTest; R24 | Кооперативный Stop сохраняет частичный текст/веса; неответивший worker завершается по таймауту; отмена начальной загрузки отличается | `ai.lifecycle` | S39 | К; Л0 |
| F124 | Явно выгрузить / настроить простой | Панель Unload; настройки IdleMinutes; R24 | 0 выключает автопростой; закрытие панели само по себе не равнозначно выгрузке, кроме отмены предзагрузки; блокировка/выход освобождают ресурсы | `ai.lifecycle` | S39 | К; Л0 |
| F125 | Написать и отправить вопрос | Prompt/Send, Ctrl+Enter; R24 | Причина недоступности Send показана; поток ответа и сворачиваемые рассуждения отдельно; чат не сохраняется на диск | `ai.chat` | S40 | К; Л0 |
| F127 | Начать новый чат | NewChat панели; R24 | Очистка истории/прикреплённого контекста и кеша, не обещать сохранение истории между запусками | `ai.chat` | S40 | К; Л0 |
| F127 | Выбрать сообщения и скопировать | IsSelected, SelectAllMessages/ClearMessageSelection/Copy; R24 | Копия исходного Markdown выбранных сообщений; рассуждения не входят в итоговый ответ | `ai.chat` | S40 | К; Л0 |
| F128 | Выбрать общий шаблон общения | SelectedPromptTemplate в панели; R24/R25 | Следующие ответы, все модели; во время ответа выбор панели недоступен; смена не очищает переписку/веса | `ai.templates` | S40 | К; Л0 |
| F129 | Создать/изменить/удалить шаблон | Настройки модели → Шаблоны общения: Items/SelectedTemplate/Add/Remove/Name/SystemPrompt, Save/Cancel; R24/R25 | Семь начальных шаблонов; уникальное непустое имя, пустой prompt разрешён, последний удалить нельзя; общий набор, не настройка модели | `ai.templates` | S41 | К; Л0 |
| F130 | Прикрепить/перетащить/убрать файл | AttachFiles/AttachPaths, FileDropCommandBehavior, RemoveAttachment; R24 | Picker включает «Все файлы»; карточки и ограничения; контекст вложений в памяти до очистки/блокировки | `ai.attachments` | S42 | К; Л0 |
| F131 | Приложить текущий документ | AttachDocument; R24 | Снимок всего открытого документа на момент действия; дальнейшие изменения не делают его автоматически живой ссылкой | `ai.attachments` | S42 | К; Л0 |
| F132 | Спросить об изображении | Прикрепить изображение; автоматический поиск совместимого mmproj рядом с GGUF; R24 | Нужны совместимые модель/проектор; vision работает отдельно, text-only модель не получает зрение от одного вложения | `ai.attachments` | S42 | К; Л0 |
| F133 | Приложить неизвестный двоичный файл | «Все файлы»/drag-and-drop → бинарная карточка; R24 | Модели доступны имя/размер/ограниченный HEX (256 байт), не полноценное содержимое; лимиты ошибок описать | `ai.attachments` | S42 | К; Л0 |
| F134 | Вставить или заменить выбранное | Insert/Replace панели, актуальные документ/выделение; R24 | Несколько выбранных сообщений; обычный редактор — оформленный фрагмент/таблицы, Markdown — исходник; из preview вернуться к редактированию; Undo | `ai.apply` | S43,S14 | К; Л0 |
| F135 | Следить за контекстом | ContextSummary/ContextUsagePercent и предупреждение панели; R24 | Вопрос/история/вложения/рассуждения делят одно окно; ввод — оценка, измерение движком может отличаться | `ai.context` | S40 | К; Л0 |
| F136 | Задать параметры ответа | Настройки модели → ContextLength/MaxTokens/Temperature; R24 | Контекст, верхний лимит ответа, креативность 0–2; отдельно на модель; не обещать ответ длиннее остатка контекста | `ai.context` | S38 | К; Л0 |
| F137 | Повторно использовать вычисления | ReuseContext в «Производительность AI»; R24 | Отдельный opt-in модели после Save; точный текстовый префикс; изображения заново, сброс при новом чате/выгрузке | `ai.context` | S38 | К; Л0 |
| F138 | Предзагрузить при открытии панели | PreloadOnPanelOpen; R24 | Отдельный opt-in модели после Save; без сообщения; загрузку можно отменить, память занята до выгрузки | `ai.context` | S38,S39 | К; Л0 |
| F139 | Выбрать режим памяти | Дополнительные параметры → MemoryMode/AutoReduceContext; R24 | Автоуменьшение доступно в автоматическом режиме; фактический контекст виден в runtime | `ai.memory` | S44 | К; Л0 |
| F140 | Выбрать слои GPU | Дополнительные параметры → GpuLayers; R24 | 0 CPU, -1 все слои; нагрузка/доступность зависят от памяти/драйвера; fallback требует фактической проверки | `ai.memory` | S44 | К; Л0 |
| F141 | Управлять защитой повторов | Дополнительные параметры → RepetitionProtection; R24 | Может остановить устойчивые повторы с сохранением полезного начала; отключение для намеренного повторения | `ai.memory` | S44 | К; Л0 |
| F142 | Управлять рассуждениями | Дополнительные параметры → UseReasoning; R24 | Qwen/Gemma и ограничения конкретной модели; тест использует текущий флажок, чат после Save | `ai.memory` | S44 | К; Л0 |
| F143 | Выбрать производительность | PerformanceMode/BoostOnlyOnAc; R24 | Сбалансированный/экономичный/оптимальный по замеру, отдельно на модель; повышенная нагрузка добровольная, другие программы не закрываются | `ai.performance` | S44 | К; Л0 |
| F144 | Измерить/повторить/остановить тест производительности | «Подобрать оптимальный режим» → PerformanceTestWindow/PerformanceTest; «Подобрать заново»/Retune; StopTest; R24 | До 6 минут, повышенная нагрузка, фактический профиль/питание/ресурсы; обещаний ускорения нет | `ai.performance` | S44 | К; Л0 |
| F145 | Оформить чат | OpenAppearance → FontFamily/FontSize/UserColor/AssistantColor/CodeFontFamily/CodeColor/ShowMessageBorders/RenderMarkdown; ChooseUserColor/ChooseAssistantColor/ChooseCodeColor/Reset/Save; R25 | Пустой цвет наследует тему; Markdown меняет отображение, Copy использует исходник; параметры общие | `ai.appearance` | S45 | К; Л0 |
| F146 | Прочитать причину ограничения/сбоя | «Работа модели» и вложенная диагностика Runtime.Stage/Summary/Warnings/Details, Status/SendUnavailableReason; R24 | Отсутствующая/несовместимая модель, vision, память, worker и частичный ответ различаются | `ai.troubleshoot` | S39,S44 | К; Л0 |

## Настройки и обслуживание

| ID | Пользовательское действие | Точный вход / источник | Условия, форматы и источники | Тема | Снимки | Проверка; языки |
| --- | --- | --- | --- | --- | --- | --- |
| F147 | Выбрать тему интерфейса | Заголовок → Настройки → Оформление → SelectedTheme; R18 | Light/Dark/System/Sepia; не меняет сохранённые цвета документа | `settings.appearance` | S46 | К; Л0 |
| F148 | Выбрать начальный экран | Настройки → Оформление → SelectedStartupScreen; R18 | Начало работы/редактор, применяется при следующем запуске; открытие файла ассоциацией имеет приоритет | `settings.appearance` | S46 | К; Л0 |
| F149 | Сменить язык | Настройки → Язык → SelectedLanguage; R18 | en-US/ru-RU/uk-UA/de-DE, применяется сразу и сохраняется; полноту живого UI проверить по языкам | `settings.language` | S46 | К; Л0 |
| F150 | Прочитать версию и сведения | Настройки → О программе / Для разработчиков, ApplicationVersion; R18 | Текущая версия/платформа/сведения; внешние страницы не считать встроенными командами без входа | `settings.about` | S46 | К; Л0 |
| F151 | Проверить/отложить/пропустить обновление | Настройки → О программе → CheckForUpdates; баннер настроек/главного окна → RemindLater/SkipVersion; R26 | Сеть/результат/ошибки; состояния пропуска версии; проверка в этом запуске не обращалась в сеть | `settings.updates` | S47 | К; Л0 |
| F152 | Скачать и установить обновление | Баннер → OpenRelease с подписью «Скачать и установить»; R26 | Это путь установщика, не только просмотр страницы. Не запускать на рабочей установке ради инвентаризации | `settings.updates` | S47 | К; Л0 |
| F153 | Понимать сохранение пользовательских данных | Настройки, история/избранное, AI JSON, recovery/lock; R15/R18/R23/R25/R26 | Не все параметры имеют видимый отдельный UI; миграцию/сохранность при обновлении проверять в изолированной среде | `settings.data` | S01,S46 | К; Л0 |
| F154 | Использовать клавиатуру и решать типичные проблемы | Области интерфейса и состояния команд; R01–R27 | Сочетания ниже; добавленные на этапе 3 F1 и клавиатура справки описаны отдельно | `settings.hotkeys`, `settings.troubleshoot` | S48 | К; Л0 |

## Встроенная справка: добавлено на этапе 3

| ID | Пользовательское действие | Точный вход / источник | Условия, форматы и источники | Тема | Снимки | Проверка; языки |
| --- | --- | --- | --- | --- | --- | --- |
| F155 | Открыть, активировать и закрыть справку | Боковое меню → «Справка»; F1 основного окна; переход из MarkdownSyntaxHelp; R27 | Одно немодальное окно, владелец MainWindow, последняя тема/геометрия; F1 других модальных окон не назначен | `help.use` | S48 | К; Л3 |
| F156 | Читать темы и искать по справке | Дерево слева, поле поиска, Enter/щелчок результата, «Оглавление»/очистка; R27 | Только доступные статьи текущей культуры; 2 образца, остальные темы скрыты; запрос очищается при смене языка | `help.use` | S48 | К; Л3 |
| F157 | Вернуться к прочитанной теме | «Назад»/«Вперёд», Alt+←/→ в справке; R27 | Отдельная история ID/прокрутки до закрытия окна; смена языка сохраняет ID, сбрасывает прокрутку | `help.use` | S48 | К; Л3 |
| F158 | Увеличить иллюстрацию и вернуться | Щелчок/Enter на иллюстрации → просмотр; Esc/кнопка закрытия; R27 | Только локальный ресурс статьи выбранного языка; возврат к месту чтения | `help.use` | S48 | К; Л3 |

## Форматы и границы источников

В Startup зарегистрированы девять IFileTemplate: PlainText, Markdown, Image, Video, Pdf, Secure, XamlPackage, RichText, Xaml. Image/Video/Pdf имеют CanCreate=false. Текстовый шаблон перечисляет .txt/.log/.cs/.xaml/.json/.xml; отдельный XamlFileTemplate обслуживает собственный формат — одно расширение нельзя автоматически считать одним режимом. Не обещать отдельного редактора исходного кода, JSON-структуры или обработки EXIF только из наличия расширения.

Markdown .md — отдельный редактор; RTF, XAML, XamlPackage и защищённый документ отличаются сохранением содержимого. Кодировки — не найденный переключатель пользователя: для .md подтверждены BOM UTF-8/16/32 и сохранение исходного формата строки; для остальных форматов требуется тематическая проверка. PDF открывается внешне. Поддерживаемые расширения видео — контейнеры, не гарантия любого кодека.

| Действие | Локальный источник | Android WPD/MTP / отдельный ADB |
| --- | --- | --- |
| Навигация, чтение/запись, создание, rename/delete, copy/move | По возможностям объекта/правам | По опубликованным возможностям объекта; устройство — дополнительный источник/диск для пользователя |
| Удаление | Поведение сервиса нужно проверить отдельно; не обещать корзину | Постоянное с обязательным подтверждением |
| Предпросмотр текста/изображения | Встроенная панель | Поддерживается через ограниченный поток |
| «Открыть с помощью», Проводник | Для локального объекта/ассоциации | Возможность OpenExternally отсутствует; сначала локальная копия для внешнего приложения |
| Шифрование / защищённая копия через EncryptCommand/CreateProtectedCopyCommand | При ключе и Encrypt | Encrypt capability не опубликована; не приписывать этот вход устройству без дополнительной проверки |
| Расшифрование существующего файла | При ключе и подходящем содержимом | Доступно через Read; замена прямо на месте через Write+Delete. Подтверждено пользователем, согласуется с текущим обработчиком |
| Групповое переименование | Два и более локальных файла | Не поддерживается этим инструментом |
| Подробные свойства | Заголовок/Windows/NTFS/подписи/хеши по формату/правам | Доступные метаданные объекта; полный анализ/хеши требуют локальной копии |
| Поиск рабочего пространства и мониторинг | Локальные источники по реализации | Эти возможности провайдеров не опубликованы; фильтр загруженного представления — отдельная функция |

Не переносить ограничение одной команды на все операции с устройством: пользователь проверил помещение зашифрованного файла на Android и расшифрование прямо там. Абстракция провайдера важна для внутренних условий, но статьи должны объяснять обычные действия с дополнительным диском.

## Сочетания: найденный вход и область действия

Это исходный список для статьи `settings.hotkeys`; большинство старых сочетаний ждёт тематической проверки. F1 добавлен на этапе 3 и проверен маршрутизированным событием в видимом редакторе. Ctrl+N/O/P и Delete/F2 для файлового списка по-прежнему не приписываются приложению по привычке.

| Клавиши/жест | Действие и область | Функции |
| --- | --- | --- |
| F1 | Главное окно — открыть/активировать справку; в справке — активировать без смены темы | F155 |
| Ctrl+F; Alt+←/→; Esc | В справке — фокус поиска; история; закрыть увеличение либо очистить поиск по области фокуса | F156–F158 |
| Ctrl+S / Ctrl+Shift+S | Сохранить / сохранить как в обычном и Markdown-редакторах | F010,F011,F054 |
| Ctrl+F | Поиск в текущем обычном/Markdown-документе | F093 |
| Enter/F3, Shift+Enter/Shift+F3, Esc | Следующее/предыдущее/закрыть поиск; Enter в поле запроса | F093 |
| Ctrl+Z/Y, Ctrl+X/C/V/A | История и буфер текста в фокусе редактора; стандартные команды | F016,F017 |
| Ctrl+B/I/U | Жирный/курсив/подчёркивание заявлены в меню; проверить реальную маршрутизацию | F022 |
| Enter / Ctrl+Enter | Перенос строки / абзац в обычном редакторе; в дереве Ctrl+Enter добавляет абзац | F018,F048 |
| Ctrl+Enter | Отправить вопрос при фокусе ввода AI | F125 |
| Ctrl+L | Сброс ключа в основном окне и настройках | F109 |
| Ctrl+M | Batch rename в FileExplorer при 2+ подходящих файлах | F080 |
| Alt+Enter | Подробные свойства выбранного в списке FileExplorer | F087 |
| Enter / Esc | Применить/отменить адрес; отдельно подтвердить/отменить редактирование имени | F057,F068 |
| Enter | Поиск по имени в настройках; ввод ключа по правилам диалога | F090,F103 |
| Alt+↑/↓ | Переместить выбранную картинку; отдельно — узел дерева структуры | F034,F049 |
| Delete / Esc | Удалить выбранную встроенную картинку / снять выбор | F036 |
| Ctrl+колесо, кнопка 100% | Масштаб редактора и сброс | F030 |
| Ctrl+ЛКМ с перетаскиванием, Esc | Двигать фон / отменить сдвиг | F037 |
| ←/PageUp, →/PageDown, Home/End | Навигация страниц в режиме чтения | F031 |
| ←/→, ↑/↓, Delete | В просмотре изображения: соседнее/масштаб/перенести файл в корзину после подтверждения | F098 |
| ←/→, Alt+←/→ | В видео: перемотка / соседнее видео | F099 |
| Esc | В окне справки закрыть увеличение/очистить поиск по фокусу; в batch-окне учесть отмену операции. Markdown-справка закрывается Alt+F4 | F051,F085,F156 |
| Ctrl при drag-and-drop файлов | Копировать; обычное перетаскивание выбирает перемещение | F073 |

## Сверка точек входа и неучитываемые заготовки

Проверены все активные XAML представлений `Views`, `MyPages`, `MyControls`, включая команды в Setter, присоединённых Behavior и Click. Следующая таблица связывает поверхности с записями выше; повторяющиеся технические события Loaded/Closing/Closed/PageLoaded/Activated/Deactivated, WindowSizeChanged и SelectionChanged не объявляются пользовательскими функциями. Они проверяются в жизненном цикле соответствующего окна.

| Поверхность | Покрывающие функции |
| --- | --- |
| MainWindow, TitleBar, SideMenu, Home | F002–F004,F009–F015,F046–F049,F077–F079,F091,F102,F109,F111,F151–F152 |
| HelpWindow, HelpWindowBehavior/ShortcutBehavior и переход из MarkdownSyntaxHelp | F155–F158 |
| NewFileDialog, DirectoryNameDialog, TextInputDialog | F005–F008,F014,F055,F067,F068; AcceptCommand/CancelCommand — подтверждение соответствующего диалога |
| FontFormatBar, TextFormatBar, ListFormatBar, InkPalette, RichTextEditorContextMenu | F016–F029,F032,F034–F039 |
| Richtextbox, DocumentSearchBar | F010–F011,F028,F030–F031,F093,F118 |
| BookmarksBar, BookmarksEditor | F040–F045 |
| MarkdownEditor, MarkdownSyntaxHelp | F010–F011,F028,F050–F054,F093 |
| FileExplorer | F055–F080,F087,F094–F097,F103,F105–F108; команды выбранного/дерева/фона и режима выбора учтены раздельно |
| BatchRenameWindow | F080–F086 |
| FileConflictDialog, ProgressWindow | F074–F075, также F085,F088,F095,F107–F108,F120,F144,F152 |
| FileDetailsWindow | F087–F089 |
| WorkspaceSearch | F091–F092 |
| ImageViewer, MediaPlayer, встроенный Flyleaf VideoView | F097–F100; точный живой набор управления Flyleaf остаётся проверкой этапа 7 |
| SettingsWindow | F004,F006,F090,F103,F109–F110,F117–F118,F147–F152 |
| KeyInputWindow, EncryptionModeWindow, DecryptionOptionsWindow, UnlockWindow | F103–F115 |
| AiAssistantPanel, AiRuntimeDetails, AiModelSettingsWindow, AiChatAppearanceWindow | F117–F146; ссылки OpenHyperlink в чате относятся к F028/F053 |
| HyperlinkDialog | F028 |
| MyMessageBox, MessageWindow | ButtonLeftClick/ButtonRightClick/OkCommand/CancelCommand: подтверждения и ошибки соответствующих операций F014,F015,F069,F074,F104,F106–F116,F152 |
| MyFrame, App, Themes, Styles | Хост/ресурсы и состояние, не отдельные прикладные команды. AiAssistantToggleStyle связывает кнопку с Toggle/IsOpen (F118) |

Не включены как готовые функции: исключённые csproj `TimeWebAIPage/Control` и `TitleBarTimeWebAIControl`, `TitleBarRB`, `SystemItemName_Editor`; пустые Reading/InsertText/OpenDocumentTree в MenuContentViewModel; старые заглушки SetEncoding и форматирования MenuSettingsModel; зарегистрированные FileOverview/OpenDirectory/WorkingDirectorySynchronization/UpdateFile без актуального рабочего UI/содержательной реализации. Часть старых полей настроек служит геометрии и совместимости, а не видимым пользовательским настройкам.

Найденные варианты UI со строками, а не ICommand, не потеряны: настройки модели Test/Retune/PerformanceTest/Save/Cancel, свойства CopySelected/CopyReport/Close, ввод ключа, гиперссылка, создание папки, Unlock и палитра через Setter. Возможные стандартные команды стороннего Flyleaf нужно завершить живой инвентаризацией на этапе 7; не перечислять их по предположению о версии библиотеки.

## Расхождения, ограничения и следующие проверки

| ID | Установленный факт / неизвестное | Что требуется и когда |
| --- | --- | --- |
| Q01 | `docs/LOCAL_AI.md` в русском шаге 1 и английском описании говорит о кнопке AI в заголовке; текущая AiAssistantButton расположена внизу Richtextbox | Новые статьи используют нижнюю кнопку/путь через настройки. Проверить видимый путь из Markdown и исправить связанную документацию при тематической работе; на этапе 1 код/старые документы не менялись |
| Q02 | `docs/ANDROID_STORAGE.md` обобщает отсутствие encryption; текущий DecryptCommand принимает удалённый читаемый файл, ReplaceSource требует Write+Delete. Пользователь подтвердил расшифрование на Android на месте | Не считать этот сценарий неподдерживаемым или сомнительным. Уточнить старый документ на этапе 7/8; получить четыре языковых снимка. Собственный аппаратный прогон здесь не выполнен |
| Q03 | 1134 ключа одинаковы, но есть жёстко заданные строки: `KeyResetIntervalOption` (минуты/«Никогда»), `UnlockWindow` («Ключ шифрования», «Открыть», «Отмена») | Это обнаруженное препятствие точному совпадению языка изображений/статей, не завершённый аудит локализации. Проверить весь соответствующий UI в тематическом этапе; исправление кода требует соответствующего этапа |
| Q04 | На этапе 3 реализованы общая справка, меню/F1, поиск/история/языки; старая Markdown-страница сохранена с переходом в новое окно | Этап 5 переносит все примеры и переключает прямой вход на md.syntax; до переноса старую страницу не удалять |
| Q05 | Старые кадры 1.1.4.7 не приняты; создан отдельный HelpValidation с памятью для настроек и переназначенными хранилищами | 28 кадров образцов сняты и проверены на этапе 3. Методы/ограничения подтверждения и оставшиеся кадры — SAMPLE_SCREENSHOTS/VALIDATION_STAGE3; старая утилита не запускалась |
| Q06 | Модель/vision, GPU/память/питание, MTP/cross-device, кодеки и физический принтер в этом запуске не проверялись; доступность оборудования не устанавливалась | Тематические этапы 7–9: реальные демонстрационные прогоны, с отдельным статусом недоступных проверок. Не утверждать, что оборудования нет |
| Q07 | Полный живой набор стандартных элементов Flyleaf не установлен статическим обзором оболочки | На этапе 7 открыть видео, перечислить видимые play/pause/seek/volume/fullscreen и сочетания по фактической версии; не обещать функции до проверки |
| Q08 | Локальное удаление подтверждается и отправляет объекты в корзину Windows, без соседнего хранилища и без постоянного удаления при ошибке корзины. Удалённые источники сохраняют отдельное предупреждение | Возврат локальных удалений проверен через настоящую корзину. RestoreFilesCommand условно доступна только для остановленных непустых журналов передачи/переименования; старые журналы удаления поддержаны |
| Q09 | OpenRelease ведёт к установке, а не просто странице релиза; свежая поставка и миграция настроек не проверялись | Этапы 10/12 в отдельной среде. Не нажимать установку поверх рабочего приложения ради снимка |
| Q10 | Реестр покрывает найденные активные входы исходников; не подтверждает живую доступность каждой команды и полноту внешних/динамических меню | Дополнять при тематической проверке, не объявлять полную работоспособность по этому документу. Для этапа 1 пробелы явно учтены и имеют темы/снимки |
| Q11 | Исправлено: недопустимые/зарезервированные имена, путь вместо имени и точка/пробел в конце отклоняются до чтения/записи | FileNameValidation применяется диалогом и FileCreationService; нет файла-подстановки. Проверки и новые creation-*.json — VALIDATION_STAGE6 |

Неразрешённого продуктового вопроса, требующего немедленного ответа пользователя для завершения инвентаризации, не было. Этапы 2 и 3 выполнены по отдельным поручениям; последующие проверки остаются задачами соответствующих этапов. Актуальная точка продолжения — PROGRESS.

Для `help.use` теперь существуют команды F155–F158; сама подробная статья о справке запланирована на этап 10 и пока скрыта как `planned`.

Для повторной сверки изменяемых полей XAML: F005 — TargetDirectory/SelectedTemplate/DefaultExtension/FileName; F006 — SelectedDocumentTemplate/SelectedDefaultDocumentTemplate; F007 — PaperSize/IsLandscape; F008 — IsReadOnly/IsHidden; F028 — Url/DisplayText; F031/F050 — IsPreviewMode; F040 — NewBookmarkName; F090 — WorkspaceDirectory; F100 — IsSynchronizationEnabled; F106 — SelectedMode. SelectedSectionIndex — выбор раздела настроек; IsExpanded/IsDropDownOpen — стандартное раскрытие элемента; DisplayPath отчёта доступен только для чтения. Value в TextInputDialog — ввод имени F014, с AcceptCommand/CancelCommand. Эти свойства не добавляют самостоятельных функций сверх указанных строк.


Дополнение этапа 5: **Л5** — четыре текста, термины, связи и снимки соответствующей темы готовы; выполнены конкретные изолированные сценарии и целевые тесты из [VALIDATION_STAGE5.md](VALIDATION_STAGE5.md). Физический ввод/жесты не объявляются проверенными. Актуально 1101 UI-ключей после удаления 58 ключей старой Markdown-страницы. Enter/Ctrl+Enter обычного редактора и ограничения таблиц описаны по действующим обработчикам. Готовность текста не означает исправление ограничений продукта:

| Вопрос | Подтверждение тематического этапа | Следующее действие |
| --- | --- | --- |
| Q12 | FloatRight bitmap после перемещения/удаления и Undo в проверенном сценарии не восстановился; осталась пустая Figure. Отчёт image-undo-<culture>.json | В content.images убрана гарантия возврата картинки; сохранить исходник/копию и вставить повторно. Для исправления нужен отдельный регрессионный сценарий всех размещений и WPF Undo; механизм не менялся |
| Q13 | XamlPackage roundtrip одной закладки с заметкой теряет якорь; проверены варианты без фона и с его фиксацией, после индексации 0 вместо 1. Name/Tag отсутствуют в сохранённом TextRange-пакете | В content.bookmarks предупреждение до действий. Нужны отдельные совместимое сохранение метаданных и регрессионные тесты; сериализация в этом этапе не менялась |
| Q14 | Исправлено: одиночное переименование без явно заданного расширения сохраняет прежнее; явно введённое заменяет его | FileExplorerModel обновляет полный путь и отдельные столбцы. Для удаления расширения остаётся пустая маска в групповом переименовании; rename-extension-*.json |
