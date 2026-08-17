from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


IMAGE_SUFFIXES = {".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg", ".webp"}


def git(root: Path, *args: str) -> str:
    result = subprocess.run(
        ["git", "-C", str(root), *args],
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        encoding="utf-8",
        errors="replace",
    )
    return result.stdout


def first_added(root: Path, relative: str) -> dict[str, str] | None:
    raw = git(
        root,
        "log",
        "--follow",
        "--diff-filter=A",
        "--format=%H%x1f%aI%x1f%an%x1f%ae%x1f%s",
        "--",
        relative,
    ).strip()
    if not raw:
        return None
    fields = raw.splitlines()[-1].split("\x1f", 4)
    if len(fields) != 5:
        return {"raw": raw.splitlines()[-1]}
    return dict(zip(("commit", "date", "author", "email", "subject"), fields))


def inspect_image(path: Path) -> tuple[dict[str, object], Image.Image | None]:
    if path.suffix.lower() == ".svg":
        return {"format": "SVG", "width": None, "height": None, "mode": None, "metadata": {}}, None

    with Image.open(path) as opened:
        opened.seek(0)
        metadata: dict[str, str] = {}
        for key, value in sorted(opened.info.items()):
            if key.lower() in {"icc_profile", "exif"}:
                metadata[key] = f"<{len(value)} bytes>"
            elif isinstance(value, bytes):
                metadata[key] = f"<{len(value)} bytes>"
            else:
                metadata[key] = str(value)[:500]
        preview = opened.convert("RGBA").copy()
        details = {
            "format": opened.format,
            "width": opened.width,
            "height": opened.height,
            "mode": opened.mode,
            "frames": getattr(opened, "n_frames", 1),
            "metadata": metadata,
        }
    return details, preview


def load_font(size: int) -> ImageFont.ImageFont:
    candidates = [
        Path(r"C:\Windows\Fonts\segoeui.ttf"),
        Path(r"C:\Windows\Fonts\arial.ttf"),
        Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


def contact_sheet(records: list[dict[str, object]], previews: dict[str, Image.Image], output: Path) -> None:
    columns = 4
    cell_width, cell_height = 340, 245
    margin, thumb_height = 18, 178
    rows = (len(records) + columns - 1) // columns
    canvas = Image.new("RGB", (columns * cell_width, rows * cell_height), "#111827")
    draw = ImageDraw.Draw(canvas)
    font = load_font(15)
    small_font = load_font(12)

    for index, record in enumerate(records):
        x = (index % columns) * cell_width
        y = (index // columns) * cell_height
        draw.rectangle((x + 6, y + 6, x + cell_width - 6, y + cell_height - 6), fill="#1f2937", outline="#475569")
        preview = previews.get(str(record["path"]))
        if preview is not None:
            area = (cell_width - 2 * margin, thumb_height - margin)
            preview.thumbnail(area, Image.Resampling.LANCZOS)
            px = x + (cell_width - preview.width) // 2
            py = y + margin + (thumb_height - margin - preview.height) // 2
            checker = Image.new("RGBA", preview.size, "#e5e7eb")
            checker.alpha_composite(preview)
            canvas.paste(checker.convert("RGB"), (px, py))

        name = str(record["path"])
        if len(name) > 43:
            name = "…" + name[-42:]
        draw.text((x + 12, y + 187), name, fill="#f8fafc", font=font)
        dimensions = f'{record.get("width")}×{record.get("height")} · {record.get("format")} · {str(record["sha256"])[:12]}'
        draw.text((x + 12, y + 213), dimensions, fill="#a5b4fc", font=small_font)

    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, format="PNG", optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser(description="Inventory Git-tracked image assets.")
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output", type=Path)
    parser.add_argument("--contact-sheet", type=Path)
    args = parser.parse_args()

    root = args.root.resolve()
    output = (args.output or root / "compliance" / "assets" / "asset-manifest.json").resolve()
    sheet = (args.contact_sheet or root / "compliance" / "assets" / "contact-sheet.png").resolve()
    tracked = [
        item
        for item in git(root, "ls-files").splitlines()
        if Path(item).suffix.lower() in IMAGE_SUFFIXES
    ]

    records: list[dict[str, object]] = []
    previews: dict[str, Image.Image] = {}
    for relative in sorted(tracked, key=str.casefold):
        path = root / relative
        data = path.read_bytes()
        details, preview = inspect_image(path)
        record: dict[str, object] = {
            "path": relative.replace("\\", "/"),
            "size": len(data),
            "sha256": hashlib.sha256(data).hexdigest().upper(),
            **details,
            "firstAdded": first_added(root, relative),
        }
        records.append(record)
        if preview is not None:
            previews[record["path"]] = preview

    manifest = {
        "schemaVersion": 1,
        "scope": "Git-tracked raster and vector image files",
        "assetCount": len(records),
        "assets": records,
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    contact_sheet(records, previews, sheet)
    print(f"Wrote {len(records)} assets to {output}")
    print(f"Wrote contact sheet to {sheet}")


if __name__ == "__main__":
    main()
