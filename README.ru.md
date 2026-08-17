# CryptoBook

[English](README.md)

[![CI](https://github.com/RomanovCopy/CryptoBook/actions/workflows/ci.yml/badge.svg)](https://github.com/RomanovCopy/CryptoBook/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/RomanovCopy/CryptoBook)](https://github.com/RomanovCopy/CryptoBook/releases/latest)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D4)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-GPL--3.0--only-blue)](LICENSE)

**Локальное рабочее пространство для документов с шифрованием под Windows.**

CryptoBook объединяет форматированный текстовый редактор, файловый менеджер, полнотекстовый
поиск, предпросмотр медиафайлов и защищённые паролем документы в одном WPF-приложении.
Данные остаются на компьютере пользователя, пока он сам не переместит или не передаст их.

[![Скачать последнюю версию](https://img.shields.io/badge/Скачать-Последний%20релиз-2ea44f?style=for-the-badge&logo=github)](https://github.com/RomanovCopy/CryptoBook/releases/latest)

> Проект находится в активной разработке. Перед работой с важными данными сохраняйте отдельные
> резервные копии. Текущие Windows-сборки могут выпускаться без Authenticode-подписи; в релизы
> входят SHA-256-контрольные суммы и явный статус подписи.

![Редактор CryptoBook](docs/screenshots/editor.png)

## Зачем CryptoBook?

- **Local-first** — документы и состояние приложения хранятся локально.
- **Зашифрованные документы** — собственный формат `.cbook` с аутентифицированным шифрованием.
- **Rich-text редактор** — форматирование, списки, ссылки, изображения, закладки и печать.
- **Файловое рабочее пространство** — навигация, избранное, Quick Access, сортировка и буфер обмена.
- **Поиск** — по имени и содержимому, включая поддерживаемые зашифрованные документы.
- **Восстановление** — crash recovery, `.bak` и атомарная замена файлов.
- **Медиа** — предпросмотр текста и изображений, видео через Flyleaf/FFmpeg.

## Скриншоты

| Боковое меню | Настройки тем |
| --- | --- |
| ![Боковое меню CryptoBook](docs/screenshots/side-menu.png) | ![Настройки тем CryptoBook](docs/screenshots/settings-themes.png) |
| **Режим чтения Sepia** | **Редактор** |
| ![CryptoBook в теме Sepia](docs/screenshots/sepia-reading.png) | ![Редактор CryptoBook](docs/screenshots/editor.png) |

## Основные возможности

- создание и редактирование TXT, RTF, XAML и XamlPackage-документов;
- форматирование текста, абзацев и списков, вставка ссылок и изображений;
- изменение размера и размещения изображений внутри документа;
- файловый менеджер с избранными каталогами, сортировкой, буфером обмена и мониторингом;
- Quick Access для закрепления часто используемых документов;
- поиск файлов по имени внутри выбранной рабочей директории;
- полнотекстовый поиск по поддерживаемым документам, включая `.cbook` после разблокировки;
- предпросмотр текста и изображений, воспроизведение видео через Flyleaf/FFmpeg;
- закладки и навигация по документу;
- печать через системный диалог Windows;
- системная, светлая, тёмная и Sepia-темы;
- шифрование отдельных файлов и каталогов;
- автоматический сброс ключа после заданного периода бездействия;
- восстановление несохранённого документа после сбоя;
- атомарное сохранение с резервной копией предыдущей версии в `.bak`;
- проверка стабильных релизов на GitHub и запуск загруженного установщика.

## Поддерживаемые форматы

| Назначение | Форматы |
| --- | --- |
| Редактирование документов | `.txt`, `.log`, `.md`, `.cs`, `.json`, `.xml`, `.rtf`, `.xaml`, `.XamlPackage` |
| Изображения | `.png`, `.jpg`, `.jpeg`, `.bmp`, `.gif`, `.webp` |
| Видео | `.mp4`, `.mkv`, `.avi`, `.mov`, `.webm`, `.wmv` и другие контейнеры Flyleaf/FFmpeg |
| Защищённые файлы | `.cbook`, устаревший `.cbox` |
| Внешнее открытие | `.pdf` |

PDF открывается системным приложением и не редактируется внутри CryptoBook. Поддержка конкретного
видеокодека зависит от медиадвижка.

## Защита данных

Актуальный формат `.cbook` использует:

- Argon2id для получения 256-битного ключа из пароля;
- AES-256-GCM для аутентифицированного шифрования;
- случайные salt и nonce;
- атомарную замену файла после успешного завершения операции.

Ключ хранится только в памяти процесса во время активной сессии и может автоматически очищаться
после бездействия. Снимки аварийного восстановления защищаются Windows DPAPI для текущего
пользователя. Старый формат `.cbox` поддерживается для обратной совместимости.

Шифрование снижает риск чтения защищённого файла без пароля, но не заменяет резервное копирование.
Утраченный пароль восстановить нельзя.

Порядок сообщения об уязвимостях и границы security scope описаны в [SECURITY.md](SECURITY.md).

## Системные требования

Для готовой сборки:

- Windows 10 версии 1809 или новее;
- 64-разрядная система.

Официальный установщик self-contained и не требует отдельной установки .NET Desktop Runtime.

## Скачать и запустить

Для большинства пользователей подходит установщик из
[последнего релиза](https://github.com/RomanovCopy/CryptoBook/releases/latest).
Там же публикуется portable ZIP для `win-x64`.

В релизы входят SHA-256-контрольные суммы, SPDX SBOM и информация о цифровой подписи.

## Сборка из исходников

Для разработки требуется .NET 8 SDK. В Visual Studio установите workload
**.NET desktop development**.

```powershell
git clone https://github.com/RomanovCopy/CryptoBook.git
cd CryptoBook
dotnet restore CryptoBook/CryptoBook.sln --locked-mode
dotnet build CryptoBook/CryptoBook.sln -c Release --no-restore
dotnet test CryptoBook/CryptoBook.sln -c Release --no-restore
```

Для self-contained x64-сборки и установщика требуется Inno Setup 6:

```powershell
./installer/Build-Installer.ps1 -Version 1.2.3
```

В проекте используются xUnit и STA-тесты для WPF. Предупреждения компилятора и обнаруженные
NuGet-уязвимости считаются ошибками в release workflow.

## Структура проекта

```text
CryptoBook/
├── CryptoBook/          # WPF-приложение
│   ├── Views/
│   ├── ViewModels/
│   ├── Models/
│   ├── Services/
│   ├── Security/
│   ├── FileTemplates/
│   └── Themes/
├── CryptoBook.Tests/
├── CryptoBook.Performance/
├── docs/
├── installer/
├── compliance/
└── .github/workflows/
```

CryptoBook построен на WPF и .NET 8, использует MVVM и Autofac для внедрения зависимостей.

## CI и релизы

GitHub Actions восстанавливает зафиксированные зависимости, запускает Release-тесты и собирает
Windows x64 installer. Производственные релизы создаются из тегов вида `v1.2.3` или `v1.2.3.4`
и включают установщик, portable ZIP, SHA-256, SPDX SBOM и статус подписи.

Подробности выпуска описаны в [docs/PRODUCTION.md](docs/PRODUCTION.md).

## Участие в разработке

Правила сборки, тестирования и pull request workflow вынесены в
[CONTRIBUTING.md](CONTRIBUTING.md). Изменения криптографии, восстановления и release automation
требуют особенно внимательного ревью.

## Лицензия

CryptoBook распространяется на условиях [GNU GPL версии 3](LICENSE), только версия 3
(`GPL-3.0-only`). Сведения об авторских правах приведены в [COPYRIGHT.md](COPYRIGHT.md),
уведомления о сторонних компонентах — в [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md),
а порядок получения соответствующего исходного кода — в [SOURCE_CODE.md](SOURCE_CODE.md).

Происхождение графических материалов описано в [ASSET_PROVENANCE.md](ASSET_PROVENANCE.md).
Происхождение FFmpeg, параметры сборки и закреплённые источники находятся в
[`compliance/ffmpeg/PROVENANCE.md`](compliance/ffmpeg/PROVENANCE.md).
