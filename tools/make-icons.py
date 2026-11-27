"""Makes the items' inventory icons from the concept art: python3 tools/make-icons.py (needs Pillow).

Each concept-art/<name>-inventory.png (transparent background) is cropped to its picture and fitted, centred, into the item's inventory
footprint at 50 px a cell, then saved as Husbandry/items/icons/husbandry-<name>.png, where the game's Icons group finds it. Each OUTLINED
entry also saves husbandry-<name>-<suffix>.png: the same icon with a bright red outline round the picture (the filled Beast Capsule).
"""
from pathlib import Path

from PIL import Image, ImageFilter

PIXELS_PER_CELL = 50
MARGIN = 0.06

# concept-art name -> inventory footprint (cells wide, cells high), as in Capsules/CapsuleData.cs
ICONS = {
    "beastcapsule": (1, 1),
}

# concept-art name -> file suffix of its red-outlined copy
OUTLINED = {
    "beastcapsule": "filled",
}

OUTLINE_PIXELS = 3
OUTLINE_COLOUR = (255, 32, 32, 255)


def outlined(icon):
    """The icon with a solid red band OUTLINE_PIXELS wide round its picture, under the picture."""
    alpha = icon.getchannel("A").point(lambda a: 255 if a > 40 else 0)
    band = alpha.filter(ImageFilter.MaxFilter(2 * OUTLINE_PIXELS + 1))
    out = Image.new("RGBA", icon.size)
    out.paste(Image.new("RGBA", icon.size, OUTLINE_COLOUR), (0, 0), band)
    out.alpha_composite(icon)
    return out


root = Path(__file__).resolve().parent.parent
out = root / "Husbandry" / "items" / "icons"
out.mkdir(parents=True, exist_ok=True)

for name, (cells_wide, cells_high) in ICONS.items():
    art = Image.open(root / "concept-art" / f"{name}-inventory.png").convert("RGBA")
    art = art.crop(art.getchannel("A").getbbox())
    width, height = cells_wide * PIXELS_PER_CELL, cells_high * PIXELS_PER_CELL
    room = (width * (1 - 2 * MARGIN), height * (1 - 2 * MARGIN))
    scale = min(room[0] / art.width, room[1] / art.height)
    art = art.resize((max(1, round(art.width * scale)), max(1, round(art.height * scale))), Image.LANCZOS)
    icon = Image.new("RGBA", (width, height))
    icon.alpha_composite(art, ((width - art.width) // 2, (height - art.height) // 2))
    icon.save(out / f"husbandry-{name}.png", optimize=True)
    print(f"husbandry-{name}.png {width}x{height}")
    if name in OUTLINED:
        outlined(icon).save(out / f"husbandry-{name}-{OUTLINED[name]}.png", optimize=True)
        print(f"husbandry-{name}-{OUTLINED[name]}.png {width}x{height}")
