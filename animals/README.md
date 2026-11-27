# New animals

Source art for animals Husbandry may add later: one folder per animal. Nothing here is built, imported or shipped; an animal's files move
into the mod when it becomes a playable animal.

| Animal | Folder | Contents |
| --- | --- | --- |
| Froxlotl | `froxlotl/` | `froxlotl-rigged.glb` (rigged original, one material, 1024 px textures); `variants/*.glb` (the same rig with a recoloured base colour: 6 pastel, 6 tropical); `textures/` (each variant's base colour, `original.png`, `region-mask.png`); `colour-variants.png` (preview sheet) |

## Froxlotl colour variants

`froxlotl/tools/` recolours the base colour by region and renders the sheet (Python 3 with numpy and Pillow; rendering needs `pip install bpy`):

- `masks.py`: regions from CIELAB on a 4 px blur: gills and feet `a* > 8.5`, belly `L* > 58` and `b* < 13`, the rest body; dark spots are
  5 `L*` below a 10 px local mean inside the body. The eyes (UV px (66, 213) and (172, 290)) keep their colour. `region-mask.png`: red gills
  and feet, blue belly, green body, yellow spots.
- `recolour.py`: `SCHEMES` holds each variant's body, belly, gills and optional spot colour (CIELAB); a pixel keeps its offset from its
  region's mean. `python recolour.py textures/original.png <out dir>`.
- `swapglb.py <rigged glb> <out glb> <png>` replaces the base colour texture; `render.py` (env `RARGS`) and `sheet.py` make the preview.
