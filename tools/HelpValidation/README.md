# Isolated help validation

This Windows/WPF executable runs the production `App.OnStartup`, views, container and commands with an in-memory `Properties.Settings` provider and explicitly redirected stores. It skips user settings migration, production crash-log handlers, IPC activation, device monitoring and update requests. The folder picker returns only the generated demo folder. No model is loaded.

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
