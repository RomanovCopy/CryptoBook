# Образцы статей: состав и проверка

Образцы этапа 2 от 29 октября 2026 года, подключённые и проверенные на этапе 3. База `8617d991080b2d3b2b0f920b5a2c7bc741a555a7`, рабочие изменения поверх неё, версия `1.1.5.2`. Реализовано окно, подготовлены 28 настоящих WPF-кадров и выполнены изолированные сценарии. Точные способы/ограничения — [VALIDATION_STAGE3.md](VALIDATION_STAGE3.md).

## Что читать

| Язык | Основные области окна | Создать и сохранить документ |
| --- | --- | --- |
| en-US | [Main areas of the window](content/en-US/start.interface.md) | [Create and save a document](content/en-US/doc.create.md) |
| ru-RU | [Основные области окна](content/ru-RU/start.interface.md) | [Создать и сохранить документ](content/ru-RU/doc.create.md) |
| uk-UA | [Основні області вікна](content/uk-UA/start.interface.md) | [Створити та зберегти документ](content/uk-UA/doc.create.md) |
| de-DE | [Die wichtigsten Bereiche des Fensters](content/de-DE/start.interface.md) | [Ein Dokument erstellen und speichern](content/de-DE/doc.create.md) |

Markdown-файлы — единственные исходники статей. Во внешнем просмотрщике `help:` не маршрутизируется. На этапе 3 `doc.save` показывалась неактивным текстом; на этапе 4 написаны четыре полные версии и переход активен. Две исходные темы расширены и переведены из `sample` в `ready`; актуальная матрица восьми тем — в [VALIDATION_STAGE4.md](VALIDATION_STAGE4.md). Исходные 28 PNG сохранены, дополнительно сняты 48 кадров этапа 4. Историческая матрица ниже фиксирует именно приёмку образцов этапов 2–3.

## Матрица «тема × язык»

`Р` — текст содержательно подготовлен и сопоставлен агентом с переводами; это не независимая вычитка носителем. `К` — названия сверены с ресурсами и видимым WPF. `С` — постоянные цели/связи проверены; `doc.save` остаётся плановой целью. `И` — настоящие локализованные PNG, raw/аннотации/паспорта и визуальная проверка. `В` — сценарий выполнен в видимом изолированном WPF через производственные команды/сервисы; это не физический ввод. Статус темы остаётся `sample` до расширения тематического объёма этапа 4.

| Тема | Язык | Текст | UI-термины | Связи | Изображения | Живой сценарий |
| --- | --- | --- | --- | --- | --- | --- |
| `start.interface` | en-US | Р | К | С | И, 3 | В |
| `start.interface` | ru-RU | Р | К | С | И, 3 | В |
| `start.interface` | uk-UA | Р | К | С | И, 3 | В |
| `start.interface` | de-DE | Р | К | С | И, 3 | В |
| `doc.create` | en-US | Р | К | С | И, 4 | В |
| `doc.create` | ru-RU | Р | К | С | И, 4 | В |
| `doc.create` | uk-UA | Р | К | С | И, 4 | В |
| `doc.create` | de-DE | Р | К | С | И, 4 | В |

Связи фактического текста: `start.interface → doc.create`; `doc.create → start.interface, doc.save`. Последняя цель зарегистрирована как `planned`, имеет содержательный шаг сохранения в образце, но собственная статья будет написана на этапе 4. Переход в отсутствующую статью запрещён по спецификации. Остальные 74 темы не написаны; названия разделов переведены в четырёх `locale.json`.

## Термины, сверенные с ресурсами

Источник каждой ячейки — соответствующий `Resources.resx` / `.ru.resx` / `.uk.resx` / `.de.resx`. Это контрольный словарь, а не второй источник пользовательских статей. Двоеточие у поля сохраняется, когда цитируется подпись целиком.

| Ключ | en-US | ru-RU | uk-UA | de-DE |
| --- | --- | --- | --- | --- |
| `Home.CreateDocument` | New document | Новый документ | Новий документ | Neues Dokument |
| `Home.OpenDocument` | Open file | Открыть файл | Відкрити файл | Datei öffnen |
| `Home.ChooseWorkspace` | Choose workspace | Выбрать рабочее пространство | Вибрати робочий простір | Arbeitsbereich auswählen |
| `Settings.Title` | Settings | Настройки | Налаштування | Einstellungen |
| `SideMenu.Create` | Create | Создать | Створити | Erstellen |
| `Common.Open` | Open | Открыть | Відкрити | Öffnen |
| `Common.Save` | Save | Сохранить | Зберегти | Speichern |
| `Common.SaveAs` | Save as | Сохранить как | Зберегти як | Speichern unter |
| `Common.Close` | Close | Закрыть | Закрити | Schließen |
| `Common.Cancel` | Cancel | Отмена | Скасувати | Abbrechen |
| `Common.File` | File | Файл | Файл | Datei |
| `Common.Browse` | Browse... | Выбрать... | Огляд... | Durchsuchen... |
| `SideMenu.Workspace` | Workspace | Рабочее пространство | Робочий простір | Arbeitsbereich |
| `SideMenu.Content` | Content | Содержимое | Зміст | Inhalt |
| `AI.Assistant` | AI assistant | AI-помощник | AI-помічник | KI-Assistent |
| `File.Folder` | Folder: | Папка: | Папка: | Ordner: |
| `File.Type` | File type: | Тип файла: | Тип файлу: | Dateityp: |
| `File.Extension` | File extension: | Расширение файла: | Розширення файлу: | Dateierweiterung: |
| `File.Name` | File name: | Имя файла: | Ім'я файлу: | Dateiname: |
| `FileTemplate.RichText` | Rich Text Format | Документ RTF | Документ RTF | Rich-Text-Format |
| `File.DocumentTemplate` | Document template | Шаблон документа | Шаблон документа | Dokumentvorlage |
| `DocumentTemplate.empty.Name` | Empty document | Пустой документ | Порожній документ | Leeres Dokument |
| `File.PaperSize` | Paper size | Формат листа | Формат аркуша | Papierformat |
| `File.Orientation` | Orientation | Ориентация | Орієнтація | Ausrichtung |
| `File.Portrait` | Portrait | Книжная | Книжкова | Hochformat |
| `File.ReadOnlyAttribute` | Read-only | Только для чтения | Лише для читання | Schreibgeschützt |
| `File.HiddenAttribute` | Hidden | Скрытый | Прихований | Versteckt |
| `File.CreatedFileOpenFailed` | The file was created, but could not be opened. | Файл создан, но его не удалось открыть. | Файл створено, але його не вдалося відкрити. | Die Datei wurde erstellt, konnte aber nicht geöffnet werden. |

## Проверка содержания по владельцам поведения

| Образец | Реестр | Проверенный источник и редакционный вывод |
| --- | --- | --- |
| `start.interface` | F002,F003 | `TitleBar.xaml`, `MainWindow.xaml`, `Home.xaml`, `SideMenuModel.cs`: стрелки страниц, меню, имя документа, настройки и начальный экран. Не обещать стрелками историю файлового менеджера |
| `start.interface` | F013,F118 | `SideMenuModel.cs`, `Richtextbox.xaml`: отдельные редакторы; кнопка AI внизу обычного редактора. Не переносить ошибочное положение из LOCAL_AI.md |
| `doc.create` | F005–F008 | `NewFileDialog.xaml`, `NewFileDialogModel.cs`, `DocumentTemplateService.cs`, `RichTextFileTemplate.cs`: сначала папка и тип, затем имя; смена типа заменяет предложенное имя; пустой шаблон, размер/ориентация и атрибуты задаются явно |
| `doc.create` | F010–F012 | `MenuFileModel.cs`, `SideMenuModel.cs`, `Richtextbox.xaml`: создание записывает файл и открывает его; последующие изменения сохраняются отдельно. Save учитывает изменения/режим чтения; Ctrl+S — область редактора |
| `doc.create` | F005,F116 | `NewFileDialogModel.Execute_CreateAsync`: возможна ошибка открытия после успешной записи. `FileCreationService`: автоматическое свободное имя при совпадении; не обещать точный суффикс введённого имени или перезапись исходника |

Связи с Fxxx показывают затрагиваемые функции, а не полное покрытие всех их форматов и режимов двумя образцами. Полные темы `doc.templates`, `doc.formats`, `doc.save` и остальные условия остаются соответствующим этапам.

Во всех четырёх `doc.create` одинаковы девять действий, выбор локальной папки/RTF/пустого шаблона/A4, порядок «тип → имя», предупреждение об отсутствии парольной защиты до создания, результат повторного открытия, отмена до записи и предупреждение о уже созданном файле. Во всех `start.interface` пять одинаковых областей и осторожное описание независимости редакторов. Имена демонстрации и строка о выезде переведены, `09:00` и формат `.rtf` общие.

## Что остаётся до приёмки

Этап 3 проверил реальный вывод и исходные 28 снимков. Этап 4 расширил темы о документах, отрицательные сценарии и проверку форматов; результаты и ограничения — в [VALIDATION_STAGE4.md](VALIDATION_STAGE4.md). На этапе 11 остаются сводная визуальная проверка, физический ввод/мониторы и фактическое изменение Windows-масштабирования (144 DPI на этапе 3 проверены рендером). Полная локальная поставка/установленная пользовательская конфигурация относятся к этапу 12. Готовность отдельных тем не означает завершение всего руководства.
