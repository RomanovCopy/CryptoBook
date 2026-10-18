# WPD integration checks

Build the app and the integration tool with the locked dependencies:

```powershell
dotnet restore tools/WpdIntegrationSmoke/WpdIntegrationSmoke.csproj --locked-mode
dotnet build tools/WpdIntegrationSmoke/WpdIntegrationSmoke.csproj -c Release --no-restore
dotnet run --project tools/WpdIntegrationSmoke/WpdIntegrationSmoke.csproj -c Release --no-build --no-restore -- --device "Galaxy Tab S7+" --decryption-only
```

`--device` selects a device by its displayed name when several portable devices
or card readers are present. Use `--storage` with a displayed storage name when
the device exposes several storage containers. Omit `--decryption-only` to also
run the existing raw transfer, directory, rename, and move checks.

The decryption checks generate disposable encrypted TXT and PNG fixtures and
use the application's codecs, export service, and MTP transfer engine to verify:

- Windows `.cbook` to a decrypted file on Android.
- Android `.cbook` to a decrypted file in Windows.
- SHA-256 equality, preserved protected sources, unique names on conflicts,
  and removal of prepared decryption workspaces.

The tool creates and removes its own `CryptoBook-WPD-Test-*` device folder and
local workspace. These checks exercise application services; interactive picker
and dialog clicks require a separate UI check.
