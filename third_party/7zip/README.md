# 7-Zip 26.06 archive engine

Unmodified Windows x64 `7z.exe` and `7z.dll` extracted from the official MSI:
<https://github.com/ip7z/7zip/releases/download/26.06/7z2606-x64.msi>.

`provenance.json` records the official release asset SHA-256 digests and exact
component hashes. Both downloaded MSI and source archive were checked against
the SHA-256 digests in the official GitHub release API. MSI administrative
extraction was used; no system installation was performed.

The application embeds these components as resources, extracts a versioned
per-user runtime and verifies component hashes before starting the console
process. Keep the hashes in `SevenZipArchiveEngine.cs` synchronized when updating.
Never substitute the reduced 7za/7zr build: RAR extraction requires the full engine.

License: `License.txt` (GNU LGPL 2.1-or-later, BSD notices and unRAR restriction).
The complete LGPL 2.1 and original UnRAR terms are retained in `Copying.txt`
and `UnRAR-License.txt`, copied unchanged from the matching source archive.
Project and source availability: <https://www.7-zip.org/>.
Exact corresponding source:
<https://github.com/ip7z/7zip/releases/download/26.06/7z2606-src.tar.xz>.

No modification, linking or recompilation of the 7-Zip code is performed.
