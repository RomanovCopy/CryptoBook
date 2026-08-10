from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "marketing" / "vk"
SOURCE = OUT / "source"

FONT_REGULAR = Path(r"C:\Windows\Fonts\segoeui.ttf")
FONT_SEMIBOLD = Path(r"C:\Windows\Fonts\seguisb.ttf")
FONT_BOLD = Path(r"C:\Windows\Fonts\segoeuib.ttf")

NAVY = "#06142F"
BLUE = "#1764E8"
GOLD = "#F4BE38"
WHITE = "#F7FAFF"
MUTED = "#B8C9E6"


def font(path: Path, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(path), size=size)


def cover_crop(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    image = image.convert("RGB")
    target_ratio = size[0] / size[1]
    source_ratio = image.width / image.height
    if source_ratio > target_ratio:
        width = round(image.height * target_ratio)
        left = (image.width - width) // 2
        image = image.crop((left, 0, left + width, image.height))
    else:
        height = round(image.width / target_ratio)
        top = (image.height - height) // 2
        image = image.crop((0, top, image.width, top + height))
    return image.resize(size, Image.Resampling.LANCZOS)


def rounded_image(image: Image.Image, size: tuple[int, int], radius: int) -> Image.Image:
    image = cover_crop(image, size).convert("RGBA")
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, size[0], size[1]), radius=radius, fill=255)
    image.putalpha(mask)
    return image


def icon_tile(image: Image.Image, size: tuple[int, int], radius: int) -> Image.Image:
    inset = max(1, round(min(image.size) * 0.038))
    image = image.crop((inset, inset, image.width - inset, image.height - inset))
    return rounded_image(image, size, radius)


def add_shadowed_card(
    canvas: Image.Image,
    image: Image.Image,
    position: tuple[int, int],
    size: tuple[int, int],
    radius: int,
) -> None:
    x, y = position
    shadow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    shadow_draw = ImageDraw.Draw(shadow)
    shadow_draw.rounded_rectangle(
        (x + 14, y + 18, x + size[0] + 14, y + size[1] + 18),
        radius=radius,
        fill=(0, 0, 0, 145),
    )
    shadow = shadow.filter(ImageFilter.GaussianBlur(22))
    canvas.alpha_composite(shadow)

    card = rounded_image(image, size, radius)
    canvas.alpha_composite(card, (x, y))
    border = ImageDraw.Draw(canvas)
    border.rounded_rectangle(
        (x, y, x + size[0] - 1, y + size[1] - 1),
        radius=radius,
        outline=(105, 151, 236, 185),
        width=2,
    )


def make_avatar(icon: Image.Image) -> None:
    size = 1000
    canvas = Image.new("RGBA", (size, size), NAVY)
    glow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    glow_draw = ImageDraw.Draw(glow)
    glow_draw.ellipse((90, 90, 910, 910), fill=(23, 100, 232, 120))
    glow = glow.filter(ImageFilter.GaussianBlur(95))
    canvas.alpha_composite(glow)

    mark = icon_tile(icon, (900, 900), 195)
    canvas.alpha_composite(mark, (50, 50))

    ring = ImageDraw.Draw(canvas)
    ring.ellipse((14, 14, 986, 986), outline=(244, 190, 56, 190), width=12)
    canvas.convert("RGB").save(OUT / "cryptobook-vk-avatar-1000.png", quality=96)


def make_cover(background: Image.Image, icon: Image.Image) -> None:
    canvas = cover_crop(background, (1920, 768)).convert("RGBA")

    shade = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    pixels = shade.load()
    for x in range(shade.width):
        alpha = int(152 * max(0.0, 1.0 - x / 1420))
        for y in range(shade.height):
            pixels[x, y] = (1, 8, 25, alpha)
    canvas.alpha_composite(shade)

    mark = icon_tile(icon, (300, 300), 68)
    mark_shadow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(mark_shadow).rounded_rectangle(
        (126, 246, 446, 566), radius=78, fill=(0, 0, 0, 175)
    )
    canvas.alpha_composite(mark_shadow.filter(ImageFilter.GaussianBlur(24)))
    canvas.alpha_composite(mark, (118, 226))

    draw = ImageDraw.Draw(canvas)
    draw.text((482, 237), "CryptoBook", font=font(FONT_BOLD, 92), fill=WHITE)
    draw.text(
        (488, 347),
        "Приватное пространство для документов",
        font=font(FONT_SEMIBOLD, 35),
        fill=GOLD,
    )
    draw.text(
        (490, 410),
        "Редактор  •  Файлы  •  Шифрование",
        font=font(FONT_REGULAR, 29),
        fill=MUTED,
    )

    draw.rounded_rectangle((486, 478, 860, 534), radius=28, fill=(20, 79, 177, 205))
    draw.text((526, 488), "Приложение для Windows", font=font(FONT_SEMIBOLD, 25), fill=WHITE)
    draw.rectangle((0, 756, 1920, 768), fill=GOLD)

    canvas.convert("RGB").save(OUT / "cryptobook-vk-cover-1920x768.png", quality=96)


def make_welcome(background: Image.Image, icon: Image.Image, screenshot: Image.Image) -> None:
    canvas = cover_crop(background, (1080, 1350)).convert("RGBA")
    overlay = Image.new("RGBA", canvas.size, (2, 10, 30, 118))
    canvas.alpha_composite(overlay)

    mark = icon_tile(icon, (220, 220), 52)
    canvas.alpha_composite(mark, (430, 74))

    draw = ImageDraw.Draw(canvas)
    title = "CryptoBook"
    title_font = font(FONT_BOLD, 72)
    title_box = draw.textbbox((0, 0), title, font=title_font)
    draw.text(((1080 - (title_box[2] - title_box[0])) // 2, 315), title, font=title_font, fill=WHITE)

    tagline = "Документы остаются на вашем устройстве"
    tagline_font = font(FONT_SEMIBOLD, 32)
    tagline_box = draw.textbbox((0, 0), tagline, font=tagline_font)
    draw.text(
        ((1080 - (tagline_box[2] - tagline_box[0])) // 2, 414),
        tagline,
        font=tagline_font,
        fill=GOLD,
    )

    features = "Редактор  •  Файловый менеджер  •  Защищённое хранилище"
    feature_font = font(FONT_REGULAR, 25)
    feature_box = draw.textbbox((0, 0), features, font=feature_font)
    draw.text(
        ((1080 - (feature_box[2] - feature_box[0])) // 2, 474),
        features,
        font=feature_font,
        fill=MUTED,
    )

    add_shadowed_card(canvas, screenshot, (60, 565), (960, 640), 24)

    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle((296, 1244, 784, 1312), radius=34, fill=(23, 100, 232, 232))
    cta = "Следите за обновлениями"
    cta_font = font(FONT_SEMIBOLD, 30)
    cta_box = draw.textbbox((0, 0), cta, font=cta_font)
    draw.text(
        ((1080 - (cta_box[2] - cta_box[0])) // 2, 1257),
        cta,
        font=cta_font,
        fill=WHITE,
    )

    canvas.convert("RGB").save(OUT / "cryptobook-vk-welcome-1080x1350.png", quality=96)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    icon = Image.open(ROOT / "AppIcon.BlueYellow.v1.png")
    background = Image.open(SOURCE / "cryptobook-vk-background.png")
    screenshot = Image.open(ROOT / "docs" / "screenshots" / "editor.png")

    make_avatar(icon)
    make_cover(background, icon)
    make_welcome(background, icon, screenshot)


if __name__ == "__main__":
    main()
