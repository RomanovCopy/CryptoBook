# Isolated help validation

This Windows/WPF executable runs the production `App.OnStartup`, views, container and commands with an in-memory `Properties.Settings` provider and explicitly redirected stores. Automated capture modes skip user settings migration, production crash-log handlers, IPC activation, device monitoring and live update requests. Their folder picker returns only the generated demo folder. The explicit `stage7-acceptance` mode below enables real device monitoring and native pickers/printing for human acceptance; persistent stores still remain isolated. Models are loaded only by the explicit AI inference modes described below.

## Stage 11: complete help layout matrix and separate OS-input session

Use an existing disposable TEMP/TMP under artifacts, one culture/process at a time.
`stage11` reads all 76 embedded topics in four application themes at 1120×780 and
720×480 DIP, records article overflow and scroll bounds, loads and enlarges every
image, checks history/language/reopening and preserves dirty ordinary/Markdown
documents plus an unsent AI fixture. It writes only validation evidence, never
production PNG/raw/annotations/passports. Settings and stores remain isolated.

After editorial changes and rebuilding, `stage11-verify` repeats all 76 topics in
Light at the wide size and all image enlargements. Both modes also capture 144/192
DPI raster density and root LayoutTransform 1.5/2.0: neither is a Windows DPI change.
Environment reports contain the actual monitor count/bounds and WPF DPI.
For a final text-only correction after enlargements already passed, append `text`
after `stage11-verify`: all articles and image resources are still read and laid
out, while repeated enlargement commands are skipped. Reports retain this filter.

`stage11-input` leaves a visible ordinary fixture and Help on doc.create, then waits
until Help closes. It records actual WPF key-up/focus state for external OS input;
it generates no routed keys. Use Computer Use or human input, distinguishing the
two in the acceptance report. It never sends a model request or uses the clipboard.
Close Help to finish and verify preservation. The initial physical-input and
multi-monitor/DPI acceptance remains open in `docs/help/VALIDATION_STAGE11.md`.

`tools/Audit-HelpLanguage.ps1` inventories every title, bold label, alt/caption and
resource match, with an alphabet/mnemonic scan. It is not semantic proofreading.
`tools/New-HelpContactSheets.ps1` makes separate evidence sheets from a chosen PNG
folder. Neither tool changes the source screenshots.

## Stage 10: settings, updates and help

Run `HelpValidation.exe <repository> <culture> <commit> stage10` once per culture
(en-US, ru-RU, uk-UA, de-DE), with TEMP/TMP under artifacts. This exercises the real
settings selectors, all four themes, language changes while help is open, startup/template
preference persistence, about/developer views and reopening. Seven navigation labels must
fit the default pane. The live WPF bold/italic/underline commands are checked separately
from physical keyboard gestures. Both dirty document types and an unsent AI draft must survive.

An isolated fictional release source feeds the production update service, coordinator and
notification: busy/no-update/available/failure/cancellation, Later, Skip and manual override.
Version 99.0.0 is a demonstration, never a real-release claim. A rejecting installer service
prevents installer execution. There is no network, download, UAC, real installation, user-profile
migration, system clipboard or model call. Existing updater/migration tests complement this mode.

It captures 13 new states (52 localized PNGs): S01-a, S46-a/b/c/d, S47-a/b/c/d/e and S48-a/b/c.
Review raw/cropped PNGs and passports, rebuild, then run `stage10-verify` in separate processes.
The program changes CWD to its isolated root before running; verify opens all nine new embedded
articles/images and records previews of the hotkey/help articles without overwriting shipped captures.
The stage report is `docs/help/VALIDATION_STAGE10.md`. DPI, physical input and release packaging
remain separate stages.

Build with the repository SDK and locked dependencies, then run one culture per process:

```powershell
dotnet restore tools/HelpValidation/HelpValidation.csproj --locked-mode
dotnet build tools/HelpValidation/HelpValidation.csproj -c Release --no-restore
dotnet tools/HelpValidation/bin/Release/net10.0-windows10.0.17763.0/win-x64/HelpValidation.dll <absolute-repository> ru-RU <git-head>
```

Use `en-US`, `ru-RU`, `uk-UA`, `de-DE`. The original capture mode writes its new `artifacts/hv/<culture>/<id>/` sandbox and seven explicitly named screenshot sets. It never uses the older `tools/ScreenshotCapture` output. Rerunning intentionally refreshes the selected mode's captures; review them before accepting them.

For the document lifecycle stage append `stage4`. This captures twelve additional states (S03-a/b, S04-c/d, S05-a/b, S06-c/d/e, S07-a/b/c), leaving the original seven untouched. Append `stage4-verify` after rebuilding to verify all eight embedded articles and images without replacing article screenshots. Each report includes the mode and individual successful checks; `formats.json` records the observed bold-format retention and actual file byte prefixes.

An optional fifth argument, such as `S06-d`, restricts screenshot output to that state while still executing every scenario check. Use this to refresh a changed view without replacing unrelated captures.

Stage 4 executes the production create/open/save/rename/close commands and real disk operations. It supplies native Save As destinations and unsaved-changes decisions through explicit queues; these are not screenshots or physical tests of Windows dialogs. The actual WPF folder picker is exercised for confirmation and cancellation. The home/new-file folder-picker shortcut still supplies only the demo directory. A static ancestor chain exposes the demo directory in FileExplorer without enumerating sibling folders; drive and directory monitoring are disabled. Template and settings stores still use the memory provider. A sharing lock and an unavailable disposable directory exercise real write errors. No security or recovery behaviour is bypassed.

After all captures, rebuild to embed the images and append `verify` to run the window checks without changing the article images. Reports and Light/Dark/System/Sepia, narrow-window and 144-DPI render evidence remain in the run's sandbox. The current directory is changed to that sandbox before application startup; the help loader reads embedded resources only. `tools/Validate-HelpContent.ps1` checks the editorial catalog and capture set.

Capture uses `RenderTargetBitmap` on visible production WPF windows, not desktop pixels; native window chrome is excluded. Functional checks execute production commands/services and routed F1/Escape events, plus WPF focus traversal. This does not claim physical keyboard/mouse operation, an installed-user migration check, or a Windows display-scaling change. The 144-DPI file is a high-DPI render of the same WPF layout.

Raw PNGs are untouched. The annotation JSON records rectangles in normalized crop coordinates and optional badge positions. With no explicit badge, its centre is 18 px left of the rectangle and 13 px below its top (clamped to 14 px from the canvas edges); near the left edge it moves 18 px to the right of the rectangle when space permits. The renderer draws a black/gold outline and a numbered gold badge, never new controls. `visualCheck` remains pending on newly captured passports until actual review.

Only the default `App()` constructor is used in production. The internal constructor accepting explicit container overrides is reserved for isolated validation. Do not call it without installing the memory provider and redirecting every persistent store first.

For editor/content/Markdown scenarios append `stage5`, then rebuild after capture and run `stage5-verify` to check embedded images without replacing screenshots. Twenty new states S08-a/b, S09-a/b/c, S10-a, S11-a, S12-a/b/c/d, S13-a/b, S14-a, S15-a/b, S16-a and S17-a/b/c leave stages 3–4 untouched. Use the optional fifth argument to restrict screenshot output.

Stage 5 takes commands from the visible controls' actual DataContexts, including Home.DocumentView and Home.DocumentStructure. It tests text formatting, paragraph/list/palette commands, disposable clipboard copy/cut/paste with the previous data object restored in memory, undo/redo, real hyperlink and crop confirmation dialogs, production image/layout/crop services, tables, bookmarks, structure, paginated preview, zoom, XamlPackage and Markdown disk roundtrips, all forty old syntax examples, and the separate Markdown help entry. The native image picker supplies queued disposable paths; physical drag gestures, native image-picker interaction and user-profile migration are not tested. Popup captures render their visible production child directly; window captures remain unchanged. The host links the original icon assets because legacy root-relative icon URIs resolve against the entry assembly.

`image-undo.json` records whether the deleted bitmap returned after Undo; `bookmark-roundtrip.json` records anchor loss both with and without background finalization. These product limitations are documented, not treated as successful restoration. `markdown-examples.json` records the renderer output and element types of all 35 syntax/limitation examples, with five editor examples checked separately. Final reports are retained under docs/help/screenshots/validation/stage5.

For the file-manager stage append `stage6`, then rebuild to embed the resulting PNGs and use `stage6-verify`. Sixteen states S18-a/b/c/d, S19-a/b, S20-a, S21-a, S22-a, S23-a/b, S24-a/b/c and S25-a/b leave all earlier captures untouched. The optional fifth argument still limits screenshot output, for example `"S18-d,S25-b"`; all functional scenarios continue to run.

Stage 6 exercises the actual FileExplorer DataContext and ListView selection, address/history/current-document navigation, sorting and recursive folder filtering, unavailable-folder retry, real folder/name/conflict/batch/details WPF dialogs, creation with invalid/duplicate names, Copy/Cut/Paste, Move and DropCommand, byte integrity, all conflict choices and apply-to-all, a real source sharing lock, cancellation through the real progress window, inline rename/cancel and extension retention, favorites, current/pinned/recent path synchronization through batch apply and undo, confirmed Windows Recycle Bin deletion and exact-item restoration, detected PNG content and independently checked SHA-256/SHA-512. Targeted tests complement mid-operation cancellation, rollback/fault injection, pattern/token coverage, properties cancellation and link availability/relocation. Shell Open With/Explorer/Windows properties, physical gestures and Android are not launched or claimed as exercised.

The folder picker now supports a queue limited to the disposable validation root, defaulting to the demo directory for existing modes. This supplies destinations to the production Browse/permission checks and move pipeline; it does not test a native picker gesture. File-copy intent uses the production internal file clipboard. CopyPath and report-copy checks temporarily use the Windows clipboard and restore the previous data object in `finally`, without serializing it. Run cultures sequentially so clipboard checks do not overlap.

`creation-<culture>.json` records invalid-name rejection without a substitute file and successful creation after correction. `rename-extension-<culture>.json` records automatic extension retention and restoration with preserved contents. Final reports, contact sheets, the UI vocabulary and validation summary are retained under docs/help/screenshots/validation/stage6. Rebuild after capture before checking embedded resources; `verify` remains the general help-window regression mode.

Local transfers and batch rename use a write-ahead journal in hidden .cryptobook-operation-* folders beside disposable objects. The production Restore operations command returns interrupted transfers/renames after restart without overwriting occupied names. Its button is enabled only for a stopped nonempty journal in the current folder, and is disabled during recovery and after successful cleanup. Legacy retained-deletion journals remain supported. Recovery object IDs and active leases prevent substitution and recovery of a running operation. Targeted tests cover rollback of completed batch items, safe replacement, durable restart/retry, read-only cleanup and copy hash/tree verification. New local deletions use IRecycleBinService/Windows IFileOperation on an STA, preserving original paths in the Windows Recycle Bin. Shell callbacks reject non-recycling deletes. Cancellation leaves completed deletions in the bin. Validation recycles only root-checked disposable paths and restores only their exact returned bin entries using the canonical undelete verb. New reports are under docs/help/screenshots/validation/stage6/recycle-bin; earlier fixes reports remain historical.

For search/devices/view/print append `stage7`, rebuild to embed its articles and PNGs, then run `stage7-verify`. Fourteen new states S03-c, S26-a/b, S27-a/b/c/d/e, S30-a/b/c, S31-a and S32-a/b leave all earlier captures untouched. The optional fifth argument still restricts image replacement, for example `S27-b`; every functional scenario continues to execute. New passports require visual review after capture.

Stage 7 exercises actual settings name search, recursive local content search, real protected-file generation and key-dialog cancellation, result opening, confirmed direct-provider result deletion, clear/cancel/close, ordinary document search, Find returning from paged preview to editing, Markdown source/preview search, Aa/counters and routed Enter/F3/Escape. It loads actual text/PNG previews, checks the explicit unsupported PDF state, and drives image-player navigation/rotation/zoom/reset and cancelled/confirmed deletion on disposable files. Image-player deletion now shows a warning and moves the original file to the Windows Recycle Bin; the host verifies the exact bin entry and restores it. Content-search deletion still uses the provider directly and does not recycle. Earlier stage 7 reports predate the image-player recycling change.

The host generates a five-second silent uncompressed BGR24 AVI, then plays/seeks it through the real Flyleaf/FFmpeg stack, opens adjacent catalog videos, plays two real windows and pauses both. It checks the shared experimental synchronization switch on/off, without claiming timing alignment. `media-*.json` records actual state/position. S31-a deliberately shows the real empty player and its entry controls: RenderTargetBitmap does not capture D3D video pixels. Native Flyleaf control gestures, audio output and a visual video-frame check remain open.

Only stage7 installs ValidationPrintSink instead of the native print service. It records prepared ordinary/Markdown content in memory, without opening PrintDialog or sending a printer job. S32-a shows the real Print entry, not a successful print. The header-only PDF fixture checks the unsupported preview message and format registration, not valid-PDF opening in an external application. No external viewer or Explorer is launched.

The production WindowsPortableDeviceBridge runs a read-only inventory and records only discovered/available counts in `devices-*.json`. It never lists personal device folders or operates on a device. Device IDs and key material are not retained. Android/MTP and two-device transfers, connection loss/reconnection, device-side decryption, the external PDF viewer and native/physical printing require separate acceptance; their help topics remain `sample`. See `docs/help/VALIDATION_STAGE7.md`. Stage7 does not use the clipboard; redirect TEMP/TMP to an existing disposable folder under `artifacts/stage7/tmp` before running.

## Separate stage 7 acceptance modes

The paragraph above describes the original local capture, not the completed hardware acceptance. The current outcome and explicitly accepted exclusions are in `docs/help/VALIDATION_STAGE7_ACCEPTANCE.md`; the six formerly sample topics are now ready. Existing `stage7`/`stage7-verify` behaviour is unchanged.

Use a separate process per mode/culture. Point TEMP/TMP at an existing disposable folder inside artifacts. Use the same repository/culture/commit arguments as above and one of these modes:

- `stage7-device`: **writes to the connected MTP device** in a new uniquely named `CryptoBook-Help-Stage7-<culture>-<suffix>` folder. Select the intended device by its exact WinRT display name with `CRYPTOBOOK_HELP_DEVICE_NAME`. Without a name, the host requires exactly one available non-drive device; it stops before writing if the choice is ambiguous. It enumerates storage-volume metadata, not personal directory contents, then uses only its new folder. It exercises real transfers, all conflict decisions, actual TXT/PNG preview, progress-window cancellation at startup, deletion of one disposable file and decryption with replacement. It captures S28-a/b and S29-a/b/c. Run all four cultures sequentially, review captures and passports, rebuild, then verify embedded resources. The disposable remote folders remain for inspection.
- `stage7-connection`: waits up to eight minutes for a **user-performed idle USB disconnect**, checks inventory/read failure and unavailable-folder handling, then waits for reconnect and verifies the retained probe. This is not interruption of an in-progress transfer. If an earlier host ended at the disconnected call, `CRYPTOBOOK_HELP_RECONNECT_FOLDER` can name its exact `CryptoBook-Help-Stage7-connection-<eight hex digits>` folder to run a **read-only continuation** after reconnect. Reports distinguish this from a complete disconnect/Retry cycle. Do not leave this variable set for a new full connection run.
- `stage7-media`: creates a real 30-second AVI with generated moving bars and quiet PCM tone. Checks two actual Flyleaf players, play/pause/seek synchronization and its disabling, Pause all, and two different decoder snapshots. Snapshots are actual decoded frames, not screenshots of video controls or composited windows. Automation does not claim audible sound, physical gestures or frame-accurate synchronization.
- `stage7-resources`: reads the eleven embedded stage 7 articles and their images in the visible help window from another CWD, without device operations or replacing captures.
- `stage7-acceptance`: **interactive, native mode**. Shows a separate Russian acceptance panel with real device refresh, the disposable folder, actual video windows, a valid external PDF, production print commands and optional evidence capture. Production drive/directory monitoring and native pickers are enabled only here; AI/update/IPC and user settings migration remain disabled. The native print service can send real jobs: for this acceptance use Cancel or explicitly select **Microsoft Print to PDF**, as the user excluded physical EPSON printing. Do not infer printing or audible output merely from a command return. Panel events are recorded in `acceptance.json`; they are not automatic pass marks. The public disposable encryption password shown in the panel is for fixtures only. A frame button uses Flyleaf's native decoder snapshot API. Explorer capture is limited to the local fixture folder or an exact `CryptoBook-Help-Stage7` path segment on a device, and remains separate evidence requiring review.

These modes preserve production startup and persistent-store isolation. `IsolatedServices.PreparePortableDemoAsync` exposes only the selected device/volume and its newly created demo folder in the automated explorer tree, without listing personal sibling folders. Reports do not retain device IDs. Do not run hardware modes against an unselected device or use personal files as fixtures. The completed acceptance did not test a second Android, ADB, cable loss during writing or physical printing; the user's explicit scope decisions are retained separately.

## Stage 8: protection and distinct recovery mechanisms

Run `stage8` for each culture in a separate process, with TEMP/TMP pointing to an existing disposable folder under artifacts. Rebuild after capture, then run `stage8-verify` for all cultures. Capture replaces only the sixteen explicitly named new states S33-a/b, S34-a/b, S35-a/b, S36-a/b/c/d/e and S37-a/b/c/d/e; earlier captures remain unchanged. The optional fifth argument limits replacement without skipping the functional checks.

Stage8 uses production key-entry, protected Save As/save/open, file-manager protected-copy/mode/export dialogs, real file-security services for copy/replacement, recursive folders and batches, Original/RTF/TXT export with text and formatting checks, live progress-window cancellation before writing, an actual sharing violation and successful retry. Native Save As destinations and .bak confirmation choices are supplied through isolated queues. They are not physical or screenshot checks of those Windows dialogs.

The .bak, crash snapshot and lock snapshot scenarios are independent. Tests cover a dirty previous-version load without disk replacement; crash-copy authentication, deferral, service/coordinator re-creation, explicit previous-session recovery, cancellation of current-work/key choices and durable replacement; ordinary-only key reset, both protected editors, wrong-key retry, saved-source versus unsaved-snapshot choice, and recovery with the original missing. The host checks the actual visible WPF editor after restoring, not just in-memory text. Security settings and the selected interval survive a live language change. Passwords are generated in memory and never included in reports or screenshot text; no plaintext source copy is created for protected editor capture. Ordinary fixtures and decrypted outputs exist to exercise those operations. The clipboard, models, updates and device operations are unused.

`stage8-verify` opens all nine embedded topics and all their images in the visible help window from another CWD; it performs no capture replacement or file-security cycle. Reports identify the mode. Physical keys/mouse, Windows sleep/session lock, power loss, an actually killed process, disk-full conditions and installed-user migration are not claimed. Targeted service tests supplement failure paths; independent language and monitor/DPI acceptance remains stage 11, the full release run and installer remain stage 12. Evidence and editorial review are in docs/help/VALIDATION_STAGE8.md.

Stage 8 follow-up: recovery is now requested only from the File menu through routed commands. Automatic crash/lock recovery banners were removed at the user's request. Stage8 checks their absence, both real menu routes, cancellation and errors after explicit requests. Refresh only S36-b,S37-a,S37-d on all four cultures when updating these menu screenshots; review their new passports. The current run has 90 checks per culture, while stage8-verify has 21. See docs/help/VALIDATION_STAGE8_MANUAL_RECOVERY.md for the superseding result.

## Stage 9.1: local AI models and lifecycle

Set `CRYPTOBOOK_HELP_MODEL` to an existing language GGUF with exactly one compatible mmproj GGUF in its directory, then run `stage9` in a separate process for each culture. Model and projector files are read only. Redirect TEMP/TMP to an existing disposable `artifacts/stage9/tmp` directory. Settings and runtime profiles use the normal isolated stores. No server, downloads, personal messages/documents, clipboard or device operations are used.

This mode captures only S38-a/b/c/d and S39-a/b/c/d/e/f. It exercises production settings Cancel, pending text bindings and Save, Test model, loading, normal completion, cooperative Stop with retained weights, explicit unload with retained messages, close/reopen and routed resize, a real vision reply to a generated red square/blue circle, an advanced-clock production idle check, missing-path validation and disabling. For unexpected exit, reflection resolves only the adaptive service's own current worker and its PID; only that isolated idle process is terminated, then status detection and successful regeneration are checked. No other running application process is discovered or terminated. The native Browse gesture and physical mouse/keyboard are not exercised.

Rebuild after capturing and adding articles, then run `stage9-verify` for each culture. This opens the four embedded articles and all their images from a different CWD, without generating or replacing screenshots. The full stage 9 is split in PROGRESS.md: chat/templates/attachments/apply belong to 9.2, context/memory/performance/appearance to 9.3. Existing stage7/stage8 modes remain unchanged. Evidence and editorial review are in docs/help/VALIDATION_STAGE9_1.md.

## Stage 9.2: chat, templates, attachments and editor transfer

Use `stage9-chat` with the same read-only CRYPTOBOOK_HELP_MODEL/projector and isolated TEMP/TMP setup. Run each culture in a separate process. This mode writes only S40-a/b/c, S41-a/b/c/d, S42-a/b/c/d and S43-a/b/c/d. Optional capture filtering still runs all functional checks. Template windows are enlarged to 740×1000 DIP to show the full template fields and Save/Cancel; original captures remain at 1440×960 for the main window and 550×930 for panel crops.

The mode runs real local table generation, a follow-up question, a mixed vision/document-snapshot request and its follow-up. It checks automatic/manual message selection, exact single/multiple Copy payload and failure via a memory-only ITextClipboardService; the system clipboard is unused. Template scenarios cover creation, empty instructions, empty/duplicate-name rejection, edit, selection persistence without clearing the chat/draft/attachments/weights, last-template protection, Cancel and saved deletion. Only isolated settings are changed.

Production attachment readers handle disposable text, a 400-byte binary (256-byte HEX prefix), a generated PNG and whole ordinary/Markdown document snapshots. Native picker selections use a root-checked queue; actual FileDropCommandBehavior receives a routed Drop event, using the internal WPF DragEventArgs constructor for the event fixture. Neither a native picker gesture nor physical dragging is claimed. Tests include immutable snapshots, retained conversation attachments, the four-file limit, oversized and empty files.

Both visible editors execute real Insert/Replace with the selected generated messages, including multiple-message order, editable WPF tables, current target selection after receiving the answer, read-only/Markdown-preview refusal and one-step Undo/Redo. Markdown receives exact source with no inserted line breaks. The native Ctrl+Enter binding is inspected; Send executes its production command without claiming physical key input.

After capturing, rebuild and run `stage9-chat-verify` for all cultures to read the four embedded articles and images from another CWD without inference or capture. `stage9-chat-templates` is a separate optional settings-only capture mode: it executes the initial template checks and returns before inference; its result must not be counted as a full chat run. Final reports and visual review are in docs/help/VALIDATION_STAGE9_2.md. Stage9.3 is not executed by these modes.

## Stage 9.3: context, memory, performance and appearance

Use the same read-only `CRYPTOBOOK_HELP_MODEL` and isolated TEMP/TMP, one process and culture at a time. `stage9-settings` checks invalid/unsaved settings, saved context and reply ceilings, opt-in preload with real weights, measured streaming tokens versus the draft estimate, actual context overflow, context reset, real manual GPU and CPU-only inference, reasoning/repetition settings and appearance without cancelling a live request. Appearance checks cover the existing generated reply, question draft, attachment identity, typography and separate code color, invalid color rejection, Markdown/source display, memory-only Copy and Reset followed by Save. It captures S38-e/f/g, S40-d/e/f, S44-a/b/c/d/e and S45-a/b/c/d.

`stage9-performance` runs real Balanced and Power saving requests, saves/reloads without clearing conversation, cancels a native benchmark at an actual trial and retries to completion. It records measured speeds and the five-percent selection decision, then verifies saved Optimal through another real request and Retune through a fresh model test. It captures S44-f/g/h/i. This mode requires actual AC power with the default AC-only restriction; it never substitutes power or memory readings, closes other applications or deliberately exhausts system memory. Benchmark results can differ between runs and locales because these are separate measurements.

`stage9-settings-reasoning` is an optional focused real-generation recapture of S44-e with runtime details collapsed so the analysis header remains visible. Its report is separate and does not stand for the full settings cycle. The model may return an answer without a separate analysis channel even with reasoning allowed.

Review the new PNGs and passports, rebuild to embed the final assets, then run `stage9-settings-verify` for all four cultures from another CWD. It opens all four articles and all 19 images without inference or capture replacement. Native color-picker gestures, physical input, battery transitions and induced memory-pressure fallbacks are outside these capture modes. The existing `LocalAiSmoke --context-reuse <GGUF> <output>` probe separately checks native exact-prefix reuse and invalidation with synthetic conversations; run it after unloading other validation workers. Evidence, test counts and explicit boundaries are in docs/help/VALIDATION_STAGE9_3.md.
