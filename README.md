# CryptoBook

[Русский](README.ru.md)

[![CI](https://github.com/RomanovCopy/CryptoBook/actions/workflows/ci.yml/badge.svg)](https://github.com/RomanovCopy/CryptoBook/actions/workflows/ci.yml)
[![Version](https://img.shields.io/badge/version-1.1.4.8-2ea44f)](https://github.com/RomanovCopy/CryptoBook/releases/tag/v1.1.4.8)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D4)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-GPL--3.0--only-blue)](LICENSE)

**A local-first encrypted document workspace for Windows.**

CryptoBook combines rich-text editing, local file management, full-text search, media preview,
and password-protected documents in one WPF desktop application. Documents stay on your
machine unless you explicitly move or share them.

[![Download latest release](https://img.shields.io/badge/Download-Latest%20release-2ea44f?style=for-the-badge&logo=github)](https://github.com/RomanovCopy/CryptoBook/releases/latest)

> CryptoBook is under active development. Keep independent backups of important data.
> Current Windows binaries may be distributed without Authenticode signing; release assets
> include SHA-256 checksums and an explicit signing-status file.

![CryptoBook editor](docs/screenshots/editor.png)

## Why CryptoBook?

- **Local-first workflow** — documents and application state are stored locally.
- **Encrypted documents** — `.cbook` files use password-derived authenticated encryption.
- **Rich-text editing** — formatting, lists, links, images, bookmarks and printing.
- **File workspace** — browsing, favorites, Quick Access, sorting and clipboard operations.
- **Search** — file-name and full-text search, including supported encrypted documents.
- **Find in document** — Ctrl+F opens search and highlights all matches; Enter/F3 and Shift+Enter/Shift+F3 navigate results, and Esc closes the bar. Supports rich documents and Markdown; searching from paged preview opens the editor.
- **Recovery copies** — retained snapshots, `.bak` backups and atomic file replacement.
- **Media preview** — text, images and video playback through Flyleaf/FFmpeg.
- **Local AI assistant** — GGUF chat, document/image attachments and editor actions.
- **Windows integration** — system dialogs, themes and self-contained x64 releases.

## Screenshots

![Find in the current document](docs/screenshots/document-search.png)

| Markdown preview | Markdown syntax help |
| --- | --- |
| ![Markdown document with a local illustration](docs/screenshots/markdown-preview.png) | ![Built-in Markdown syntax guide](docs/screenshots/markdown-help.png) |
| **Sepia reading mode** | **Document navigation** |
| ![Illustrated document in Sepia](docs/screenshots/sepia-reading.png) | ![Side menu and illustrated document](docs/screenshots/side-menu.png) |
| **New document and templates** | **Default template settings** |
| ![New document dialog](docs/screenshots/new-document.png) | ![Appearance and default template settings](docs/screenshots/template-settings.png) |

Screenshots show an illustrated travel journal and a page background. See the
[capture notes and sample documents](docs/screenshots/README.md).

## New in 1.1.4.8

- Insert AI replies into rich-text documents as editable headings, lists, quotes, code and tables, preserving surrounding text and supporting single-step undo. The Markdown editor keeps the source markup.
- Edit table rows and columns from the context menu, with undo/redo and preservation of cell contents. Structural editing supports rectangular tables without merged cells.
- Select any questions and answers, select all or clear the selection, then copy, insert or replace with the selected messages in conversation order. Reasoning and attachment contents are excluded.
- Keep manual message selection across follow-up requests; otherwise select the latest nonempty answer automatically. Selection controls are disabled during generation and cleared with the chat or workspace lock.

GGUF models and vision projectors are selected separately and are not bundled with the installer.
See the [release notes](docs/releases/v1.1.4.8.md), [table editing guide](docs/AI_TABLE_EDITING.md) and [changelog](CHANGELOG.md).

## Main features

- create and edit TXT, RTF, XAML and XamlPackage documents;
- edit Markdown in a dedicated page and switch between source and rendered preview;
- consult the built-in Markdown syntax guide without closing the document;
- switch between rich-text and Markdown documents while preserving unsaved changes;
- format text and paragraphs, create lists, insert links and images;
- set a document background color or image and preserve it in XamlPackage and
  protected documents;
- resize and position images inside documents;
- choose A2/A3/A4 page width and orientation for new rich-text documents, with automatic
  editor scaling and layout preservation in XamlPackage and protected rich-text saves;
- zoom with Ctrl + mouse wheel and reset the editor view to 100%;
- browse folders and files with sorting, favorites and change monitoring;
- inspect [detailed file properties](docs/FILE_DETAILS.md), content signatures, extension mismatches,
  Windows metadata, permissions, streams, digital signatures and SHA-256/SHA-512 checksums;
- pin frequently used documents with Quick Access;
- search file names inside the selected workspace;
- perform full-text search across supported documents, including `.cbook` files after unlock;
- find text in the current document with case matching, highlighted results and keyboard navigation;
- preview text and images and play video through Flyleaf/FFmpeg;
- use bookmarks and document navigation;
- print the current document through the Windows print dialog;
- choose system, light, dark and Sepia themes;
- choose whether CryptoBook opens on the start page or directly in the editor;
- encrypt individual files and directories;
- create verified protected `.cbook` copies without changing the source files;
- automatically clear the in-memory encryption key after a configurable idle period;
- retain recovery snapshots without startup restoration prompts;
- save atomically and keep the previous version as a `.bak` file;
- check GitHub for stable releases and launch downloaded installers.

## Document templates

When creating a document, choose an empty document or one of the built-in templates:
notes, project plan, meeting minutes, journal or report. Change the default in
**Settings → Appearance → Default new document template**; the creation dialog can override it.
Templates adapt to A2/A3/A4 in either orientation. Markdown keeps source markup,
TXT and text XAML keep text, and RTF, XamlPackage and protected documents keep formatting.
XamlPackage and protected documents also preserve the selected page geometry.

## Local AI assistant

Open **Settings → AI assistant → Model settings**, enable the assistant and select a
local `.gguf` model. Open the **AI** panel in the title bar, enter a request and press
**Ctrl+Enter**. No separate server is required. Models are not included in the release.

- Attach documents and images, or a snapshot of the current document including unsaved changes.
  Image analysis requires a compatible vision model and a matching `mmproj*.gguf` beside it.
- Copy an answer, insert it at the current cursor or replace the current selection.
  The document and selection can change after generation; insertion supports undo.
- Customize chat fonts, colors, borders and Markdown rendering. Context usage, reply capacity
  and a per-model repetition guard help manage longer conversations.
- Model loading and generation run in an isolated worker process. Automatic memory tuning can
  adjust GPU offloading and use a CPU fallback; manual settings and cancellation are available.
- Chat history stays in memory. Clearing the chat resets messages, draft, attachments and
  counters; locking the protected workspace also clears the chat and unloads the model.

See [local AI setup and limits](docs/LOCAL_AI.md) for supported attachments, reasoning,
memory settings and runtime profiles.

## Supported formats

| Purpose | Formats |
| --- | --- |
| Editable documents | `.txt`, `.log`, `.md`, `.cs`, `.json`, `.xml`, `.rtf`, `.xaml`, `.XamlPackage` |
| Images | `.png`, `.jpg`, `.jpeg`, `.bmp`, `.gif`, `.webp` |
| Video | `.mp4`, `.mkv`, `.avi`, `.mov`, `.webm`, `.wmv` and other Flyleaf/FFmpeg-supported containers |
| Protected files | `.cbook`, legacy `.cbox` |
| External opening | `.pdf` |

PDF files are opened by the system application and are not edited inside CryptoBook.
Video-codec support depends on the bundled media engine.

See [Markdown support](docs/MARKDOWN.md) for supported syntax, encoding preservation,
navigation and image/link restrictions.

## Security overview

The current `.cbook` format uses:

- Argon2id for password-based 256-bit key derivation;
- AES-256-GCM for authenticated encryption;
- random salts and nonces;
- atomic file replacement after successful operations.

The encryption key is kept in process memory only for the active session and can be cleared
automatically after inactivity or manually with Ctrl+L. Protected-document recovery snapshots use
password-derived Argon2id/AES-256-GCM; ordinary-document snapshots use Windows DPAPI for the current user.
Legacy `.cbox` files remain readable for backward compatibility.

Encryption reduces the risk of reading a protected file without its password, but it is not a
backup mechanism. A lost password cannot be recovered.

For vulnerability reporting and the supported security scope, see [SECURITY.md](SECURITY.md).

## System requirements

For official builds:

- Windows 10 version 1809 or later;
- 64-bit Windows.

The installer is self-contained and does not require a separate .NET Desktop Runtime install.

## Download and run

The published release is [CryptoBook 1.1.4.8](https://github.com/RomanovCopy/CryptoBook/releases/tag/v1.1.4.8),
released on October 22, 2026.

| File | Purpose |
| --- | --- |
| [Windows x64 installer](https://github.com/RomanovCopy/CryptoBook/releases/download/v1.1.4.8/CryptoBook-Setup-1.1.4.8.exe) | Install or update CryptoBook |
| [Portable ZIP](https://github.com/RomanovCopy/CryptoBook/releases/download/v1.1.4.8/CryptoBook-win-x64.zip) | Extract to a separate folder and run `CryptoBook.exe` |
| [SHA256SUMS.txt](https://github.com/RomanovCopy/CryptoBook/releases/download/v1.1.4.8/SHA256SUMS.txt) | Verify downloaded files |
| [SIGNING-STATUS.txt](https://github.com/RomanovCopy/CryptoBook/releases/download/v1.1.4.8/SIGNING-STATUS.txt) | Check the declared signing status |

The 1.1.4.8 installer and application are **not digitally signed**. Before running a download,
compare its SHA-256 with the entry in `SHA256SUMS.txt`, for example:

```powershell
Get-FileHash .\CryptoBook-Setup-1.1.4.8.exe -Algorithm SHA256
```

The release also includes the license archive, FFmpeg/Flyleaf provenance archive, dependency
lock and SPDX SBOM. See the [release notes](docs/releases/v1.1.4.8.md) and
[changelog](CHANGELOG.md). The built-in updater verifies the installer checksum before launch.

## Build from source

Development requires the .NET 10 SDK (10.0.400 or newer in the 10.0 feature
band). Visual Studio users should install the
**.NET desktop development** workload.

```powershell
git clone https://github.com/RomanovCopy/CryptoBook.git
cd CryptoBook
dotnet restore CryptoBook/CryptoBook.sln --locked-mode
dotnet build CryptoBook/CryptoBook.sln -c Release --no-restore
./tools/Run-ReleaseTests.ps1

# Check the production video-player WPF template
dotnet run --project tools/FlyleafPlaybackSmoke/FlyleafPlaybackSmoke.csproj -c Release --no-restore -- --template-only

# Self-contained single-file x64 build and installer (requires Inno Setup 6)
./installer/Build-Installer.ps1 -Version 1.1.4.8
```

`Run-ReleaseTests.ps1` runs all tests in three isolated processes: AI integration,
document search and the remaining tests. This prevents interference from process-wide WPF
application state. The 1.1.4.8 candidate passed **935 tests**, without failures or skips. See [the release notes](docs/releases/v1.1.4.8.md).
TRX reports are written to `artifacts/TestResults`; the installer is written to
`artifacts/CryptoBook-Setup-1.1.4.8.exe`.

The project uses xUnit and STA tests for WPF. Compiler warnings and detected NuGet
vulnerabilities are treated as errors.

## Project structure

```text
CryptoBook/
├── CryptoBook/          # WPF application
│   ├── Views/
│   ├── ViewModels/
│   ├── Models/
│   ├── Services/
│   ├── Security/
│   ├── FileTemplates/
│   └── Themes/
├── CryptoBook.Tests/    # unit and WPF STA tests
├── CryptoBook.Performance/
├── docs/
├── installer/
├── compliance/
└── .github/workflows/   # CI and release automation
```

CryptoBook is built with WPF and .NET 10, follows MVVM, and uses Autofac for dependency
injection.

## CI and releases

GitHub Actions restores locked dependencies, verifies FFmpeg provenance, runs isolated Release
tests, checks the Flyleaf WPF template and builds the Windows x64 installer. A version tag
starts the production workflow, which stores the verified release files as an artifact.
Public release publication is a separate step after candidate approval and successful CI;
the approved local installer is preserved.

Release 1.1.4.8 contains eight files: installer, portable ZIP, license archive, FFmpeg provenance
archive, dependency lock, SPDX SBOM, signing status and SHA-256 manifest.

Operational release details are documented in [docs/PRODUCTION.md](docs/PRODUCTION.md).

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for the build, test and PR
workflow. Changes to cryptography, recovery or release automation require extra review.

## Contact

Email: [EncryptoBook@gmail.com](mailto:EncryptoBook@gmail.com).

## License and third-party notices

CryptoBook is licensed under [GNU GPL version 3 only](LICENSE) (`GPL-3.0-only`).
Copyright information is in [COPYRIGHT.md](COPYRIGHT.md), third-party notices are in
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), and corresponding-source information is in
[SOURCE_CODE.md](SOURCE_CODE.md).

Asset provenance is documented in [ASSET_PROVENANCE.md](ASSET_PROVENANCE.md). FFmpeg provenance,
build parameters and pinned source references are documented in
[`compliance/ffmpeg/PROVENANCE.md`](compliance/ffmpeg/PROVENANCE.md).
