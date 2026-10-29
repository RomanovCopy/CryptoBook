# Isolated help validation

This Windows/WPF executable runs the production `App.OnStartup`, views, container and commands with an in-memory `Properties.Settings` provider and explicitly redirected stores. It skips user settings migration, production crash-log handlers, IPC activation, device monitoring and update requests. The folder picker returns only the generated demo folder. No model is loaded.

Build with the repository SDK and locked dependencies, then run one culture per process:

```powershell
dotnet restore tools/HelpValidation/HelpValidation.csproj --locked-mode
dotnet build tools/HelpValidation/HelpValidation.csproj -c Release --no-restore
dotnet tools/HelpValidation/bin/Release/net10.0-windows10.0.17763.0/win-x64/HelpValidation.dll <absolute-repository> ru-RU <git-head>
```

Use `en-US`, `ru-RU`, `uk-UA`, `de-DE`. A capture run writes only its new `artifacts/hv/<culture>/<id>/` sandbox and the seven explicitly named help screenshot/annotation/passport/output files. It never uses the older `tools/ScreenshotCapture` output. Rerunning intentionally refreshes these help captures; review them before accepting them.

After all captures, rebuild to embed the images and append `verify` to run the window checks without changing the article images. Reports and Light/Dark/System/Sepia, narrow-window and 144-DPI render evidence remain in the run's sandbox. The current directory is changed to that sandbox before application startup; the help loader reads embedded resources only. `tools/Validate-HelpContent.ps1` checks the editorial catalog and capture set.

Capture uses `RenderTargetBitmap` on visible production WPF windows, not desktop pixels; native window chrome is excluded. Functional checks execute production commands/services and routed F1/Escape events, plus WPF focus traversal. This does not claim physical keyboard/mouse operation, an installed-user migration check, or a Windows display-scaling change. The 144-DPI file is a high-DPI render of the same WPF layout.

Raw PNGs are untouched. The annotation JSON records rectangles in normalized crop coordinates and optional badge positions. With no explicit badge, its centre is 18 px left of the rectangle and 13 px below its top (clamped to 14 px from the canvas edges); near the left edge it moves 18 px to the right of the rectangle when space permits. The renderer draws a black/gold outline and a numbered gold badge, never new controls. `visualCheck` remains pending on newly captured passports until actual review.

Only the default `App()` constructor is used in production. The internal constructor accepting explicit container overrides is reserved for isolated validation. Do not call it without installing the memory provider and redirecting every persistent store first.
