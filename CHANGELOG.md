# Changelog

## 1.1.3.4 - 2026-09-12

### Added

- Ctrl + mouse wheel zoom in the rich-text editor.
- A **100%** button beside the zoom indicator to reset the actual page scale
  and return horizontal scrolling to the left edge; hidden in reading preview.

### Changed

- Preserve the user's zoom factor relative to fit-to-width when resizing the
  window or reloading the editor view. The displayed percentage follows the
  available width, including after resetting to 100%.
- Keep document page size, font sizes and saved content unchanged while zooming;
  allow horizontal scrolling when the enlarged sheet exceeds the viewport.
- Extend WPF regression coverage for wheel zoom, limits, rendered page borders,
  resize/reload behavior and the reset button.
- Synchronize application, installer, documentation and release metadata for 1.1.3.4.

Full comparison: [v1.1.3.3...v1.1.3.4](https://github.com/RomanovCopy/CryptoBook/compare/v1.1.3.3...v1.1.3.4)

## 1.1.3.3 - 2026-09-09

### Added

- A2, A3 and A4 page-size selection with portrait/landscape orientation in the
  new rich-text document dialog.
- Built-in Markdown syntax help with examples, navigation back to the editor,
  and explanations of supported preview syntax and image/link restrictions.
- An Insert image command in the rich-text editor context menu.

### Changed

- Center the document sheet and automatically scale the editor to the available
  width when resizing the window; show the current zoom in the status bar.
- Preserve document width through XamlPackage/protected rich-text saves and
  document-session switches, and use it when preparing the reading preview.
- Refresh English and Russian READMEs with current application views and
  illustrated demonstration documents.
- Synchronize application, installer and release metadata for 1.1.3.3.

Page size controls the continuous editor's sheet width. It does not introduce
separate editable pages or a dedicated PDF exporter.

Full comparison: [v1.1.3.2...v1.1.3.3](https://github.com/RomanovCopy/CryptoBook/compare/v1.1.3.2...v1.1.3.3)

## 1.1.3.2 - 2026-09-07

### Added

- Added a dedicated Markdown editor with source editing and rendered preview,
  including headings, lists, quotes, tables, code blocks and local images.
- Rich-text and Markdown documents can remain open independently, with navigation
  through the side menu and Back/Forward buttons.

### Changed

- Markdown saves preserve the source text, original encoding, BOM and line endings;
  protected saves also use the Markdown source rather than the rendered preview.
- Recovery and lock snapshots include both open documents and their unsaved changes.
- File commands and unsaved-change prompts target the relevant document; application
  shutdown checks both editors.
- Added Markdig and its BSD-2-Clause license to dependency and licensing records.

### Security

- Markdown HTML remains inert text, remote images are not loaded, and active links
  are restricted to absolute HTTP, HTTPS and mailto URLs.

Full comparison: [v1.1.3.1...v1.1.3.2](https://github.com/RomanovCopy/CryptoBook/compare/v1.1.3.1...v1.1.3.2)

## 1.1.3.1 - 2026-09-06

### Added

- Added document background images with localized choose and remove actions in
  the editor's Paper controls.
- XamlPackage and protected documents now preserve their document background
  color or embedded background image and restore it when reopened.

### Changed

- Document preview refreshes immediately after its background changes, and a
  background change now marks the document as modified so it is not lost.

### Fixed

- The text caret now follows the active foreground color after selection,
  navigation, and continued typing.
- Invalid or unsupported optional background metadata no longer prevents the
  document text from opening.

Full comparison: [v1.1.3.0...v1.1.3.1](https://github.com/RomanovCopy/CryptoBook/compare/v1.1.3.0...v1.1.3.1)

## 1.1.3.0 - 2026-09-04

### Added

- Added complete German localization for the application and installer.
- Added Ukrainian language support to the installer, matching the existing
  application localization.

### Security

- Protected V2 media is now decrypted as an authenticated seekable stream
  instead of a plaintext temporary file. Legacy media uses a zeroing in-memory
  buffer with a 256 MB limit and has no automatic disk fallback.
- The built-in updater now verifies the published SHA-256 checksum and the
  declared Authenticode signing state before launching an installer.

### Fixed

- Restored Left/Right video seeking and Alt+Left/Alt+Right navigation when
  protected media is played from a decrypted stream.
- Prevented end-of-video seek buttons from resetting playback to the beginning,
  and restored the protected file name in the playback bar.
- Android/MTP moves now verify directory contents and each file's size and
  SHA-256 checksum before deleting the source.

Full comparison: [v1.1.2.51...v1.1.3.0](https://github.com/RomanovCopy/CryptoBook/compare/v1.1.2.51...v1.1.3.0)

## 1.1.2.51 - 2026-09-02

### Fixed

- Fixed an in-place upgrade failure where the new .NET 10 single-file
  executable could load `System.Private.CoreLib.dll` and other runtime files
  left by an earlier .NET 8 multi-file installation, then exit before opening
  the main window.
- The installer now removes only known legacy runtime files and directories
  before copying the current single-file application. It deliberately avoids a
  broad deletion of the installation directory.
- Added regression coverage for the legacy-runtime cleanup rules.
- Because affected 1.1.2.5 installations fail before application startup, they
  cannot use the built-in updater. Version 1.1.2.51 must be installed manually
  once over the existing installation; user documents and settings are kept.

Full comparison: [v1.1.2.5...v1.1.2.51](https://github.com/RomanovCopy/CryptoBook/compare/v1.1.2.5...v1.1.2.51)

## 1.1.2.5 - 2026-09-02

### Highlights

- Migrated the application, tests, build scripts, and release workflows from
  .NET 8 to .NET 10. Official self-contained builds now include the pinned
  .NET 10.0.11 runtime.
- Updated the media stack to Flyleaf 3.11.3 and FFmpeg 9. The exact native
  runtime is now kept in a repository-local NuGet package so locked restores
  and release builds use the verified binaries.
- Hardened media startup by checking the complete required FFmpeg DLL set and
  added a release smoke test that builds and validates the Flyleaf WPF control
  template in clean CI environments.
- Updated Autofac, Microsoft.Data.Sqlite, SQLitePCLRaw, Material Design, WPF
  Behaviors, test infrastructure, and their locked dependency graphs.
- Expanded FFmpeg/Flyleaf provenance checks and release evidence, refreshed
  third-party notices, and included the local runtime package and reconstruction
  tooling in the provenance archive.
- Modernized the legacy PBKDF2 implementation without changing its derivation
  parameters, and cleaned up obsolete or ambiguous framework references after
  the platform migration.

Full comparison: [v1.1.2.4...v1.1.2.5](https://github.com/RomanovCopy/CryptoBook/compare/v1.1.2.4...v1.1.2.5)

Older releases are available on the
[GitHub Releases page](https://github.com/RomanovCopy/CryptoBook/releases).
