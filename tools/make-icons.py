"""Makes the items' inventory icons from the concept art: python3 tools/make-icons.py (needs Pillow).

Each concept-art/<name>-inventory.png (transparent background) is cropped to its picture and fitted, centred, into the item's inventory
footprint at 50 px a cell, then saved as Husbandry/items/icons/husbandry-<name>.png, where the game's Icons group finds it.
"""
from pathlib import Path

from PIL import Image

PIXELS_PER_CELL = 50
MARGIN = 0.06

# concept-art name -> inventory footprint (cells wide, cells high), as in Capsules/CapsuleData.cs
ICONS = {
    "beastcapsule": (1, 1),
}

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
