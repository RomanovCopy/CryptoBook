# Asset provenance

Audit date: 2026-08-17. The machine-readable inventory in
`compliance/assets/asset-manifest.json` records the SHA-256 hash, dimensions,
embedded metadata and first-addition Git commit for all 48 tracked image files.
`compliance/assets/contact-sheet.png` is the visually reviewed index.

Git authorship proves who committed a file, not who created the underlying
artwork or granted redistribution rights. The status below therefore separates
reproducible repository evidence from copyright assertions.

| Asset family | Count | Evidence | Status for redistribution |
| --- | ---: | --- | --- |
| `docs/screenshots/*.png` | 4 | Added in commit `3ae6f0ef855578d2efb2966bd94c4e1b3193a4c4` by `RomanovCopy`; each image visibly depicts CryptoBook. | Project-derived screenshots; record Романов Сергей's explicit confirmation in `compliance/assets/ATTESTATION.md` before treating them as GPL-3.0-only project assets. |
| `marketing/vk/source/cryptobook-vk-background.png` | 1 | Added with `imagegen-prompt.md`; the prompt says `Mode: built-in image generation` and describes this background. | Generation record present; keep the prompt and output together. Confirmation by the account that requested the generation is still recommended. |
| `marketing/vk/cryptobook-vk-*.png` | 3 | Added with `build_assets.py`. A clean rebuild on 2026-08-17 produced byte-identical SHA-256 hashes for all three outputs from the background, project icon and screenshot. | Derivation is reproducible, but the inputs retain their own provenance requirements. |
| `AppIcon.BlueYellow.v1.png`, its resource copy, and `Resources/Icons/AppIcon.ico` | 3 | Added together in commit `3d4425fde8b8d3ea33b984cb3f3b9663f9a82193` by `RomanovCopy`; the two PNG files are byte-identical. No prompt, editable source, external URL or creator declaration is committed. | **Unresolved.** Obtain the creator/generation record and license declaration, or replace the icon with newly documented artwork. |
| Numbered files in `CryptoBook/Resources/Images/` | 37 | 32 current files descend from assets first added in commit `cad47b2882db633eb31fcea223f5f9df9abf82cf` by Сергей Романов; five alignment/paragraph files were replaced in commit `69cf462750065506a64ea35b195d5673ddffc6e2`. Metadata names old Paint.NET and Inkscape versions, but contains no creator or license. Exact-name searches in public web and GitHub code search returned no independent source match. | **Unresolved/high priority.** A commit and editing-software tag do not establish ownership. Obtain a signed origin statement plus editable sources, or replace the set with documented project-created/GPL-compatible vector icons. |

## Required record

The developer/copyright holder should complete
`compliance/assets/ATTESTATION.md`. For anything obtained elsewhere, attach a
source URL, original filename and license/receipt instead of asserting
project authorship. Preserve prompts, editable SVG/XCF/PSD files and generation
scripts with future assets.

Do not mark the two unresolved families as GPL-3.0-only merely because they are
stored in this GPL repository. Until the record is completed, they remain a
release gate or should be replaced.

## Regeneration

Regenerate the factual manifest and contact sheet with a Python/Pillow
environment:

```powershell
python tools/compliance/new_asset_inventory.py
```
