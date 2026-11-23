# Play Husbandry with Meitou Transport

Husbandry (this repo) and Mattresspad's [Meitou Transport](https://github.com/mattresspad/MeitouTransport) (a framework plus three packs)
in one game, so hitching, breeding and riding can be tested on the same animals. Set up and checked on 2026-11-18: both load, log below.

## Folders

```powershell
$Repos     = "C:\Users\Matthew\source\repos"
$Husbandry = "$Repos\Husbandry"
$Transport = "$Repos\MeitouTransport"
$Kenshi    = "C:\Program Files (x86)\Steam\steamapps\common\Kenshi"
$Engine    = "$Husbandry\engine\src\Meitou.Game\bin\Release\net11.0"
```

## Versions

- Transport's `meitou.json` pins `"gameVersion": "=0.20.1"`: any other Meitou release refuses it ("built for Meitou =0.20.1; this is …").
- Husbandry's `engine` submodule (fork `1cc76a0be`) describes as `v0.20.1`, so the engine Play builds passes the pin. So does the launcher's
  installed `versions\0.20.1`.
- Husbandry's `meitou.lock.json` names no frameworks: nothing is downloaded, and Husbandry doesn't depend on Transport or Rivalry Core.

## Build and deploy

**Husbandry** (engine, mod, `Husbandry.mod` generated from the game, copied to `$Kenshi\mods\Husbandry`):

```powershell
cd $Husbandry
git pull
git submodule update --init --recursive
dotnet build Play -c Release
```

**Transport** has no Play project: build it against the engine above, package it and extract the four ZIPs into the mods folder.

```powershell
cd $Transport
git pull
$env:MEITOU_DIR = $Engine
dotnet build framework/Meitou.Transport.csproj -c Release
dotnet run --project tests/Meitou.Transport.Tests.csproj -c Release
python tools/package.py
Get-ChildItem dist\*.zip | ForEach-Object { Expand-Archive $_.FullName -DestinationPath "$Kenshi\mods" -Force }
```

- Build Husbandry first: `$Engine` is its output.
- Close the game first: a running game locks the dlls.
- The release ZIPs from the [playtest.5 page](https://github.com/mattresspad/MeitouTransport/releases/tag/v0.3.0-playtest.5) work too
  (built against the released v0.20.1); extract them the same way.

## Load order

`$Kenshi\data\mods.cfg` ends with (already set; the earlier file is `mods.cfg.before-transport.bak` beside it):

```
Husbandry.mod
LivingWasteland.mod
Meitou Transport.mod
Garru Saddle.mod
Stoat Desert Ride.mod
Western Hive Skiff.mod
```

- Transport before its packs, Garru Saddle before Stoat Desert Ride. Husbandry can go anywhere.
- No trust step: enabling a code mod is the consent (engine `docs/modding.md` "Trust").
- The launcher's **Phoenix Glaives** preset has Husbandry but not the four Transport mods: picking it drops them. Save a new preset after
  enabling them, or re-enable them after.

## Launch

```powershell
cd $Husbandry
dotnet run --project Play
```

Or Visual Studio: open `Husbandry.slnx`, profile **Play (engine + mods)**, F5. Either one rebuilds and redeploys Husbandry first (not
Transport). The log is `$Engine\log.txt`; a good start has:

```
mods      13 code mod(s) enabled; Harmony 3.0.0.0, code-mod API 2
mod       husbandry: loaded from Husbandry (Husbandry)
mod       meitou.transport: Garru Saddle.mod registered 0 vessel(s), 0 placement(s) and 1 mount fit(s).
mod       meitou.transport: Stoat Desert Ride.mod registered 0 vessel(s), 0 placement(s) and 1 mount fit(s).
mod       meitou.transport: Western Hive Skiff.mod registered 1 vessel(s), 1 placement(s) and 0 mount fit(s).
mod       meitou.transport: loaded from Meitou Transport (Meitou.Transport)
```

```powershell
Select-String "$Engine\log.txt" -Pattern 'husbandry|meitou\.transport|built for Meitou'
```

## Test

Quickest start: **New Game → Stoat Desert Ride** (a Scorchlander, a Garru and a Domesticated Bull outside Stoat, X 34595, Z −70128).

1. **Ride.** Select only the Scorchlander beside the Garru, **F10 → Mount nearby animal**, right-click the dunes. **F10 → Dismount** on clear
   ground.
2. **Hitch.** Research **Animal Husbandry**, build a **Hitching Post** (Farming) and an Animal Feeder, select both animals, right-click the
   post, **Hitch here** ([husbandry.md](husbandry.md)). Mount the hitched Garru and ride out past the pen radius: husbandry.md says the order
   wins and the Garru walks back once idle after the rider dismounts. Also try hitching while ridden and dismounting at the post.
3. **Breed.** Garru and Domesticated Bull are different races, so the Stoat pair never breeds. Buy a second Garru or Pack Beast (same race;
   sex is half and half, so you may need a third), hitch them to one post with food in the feeder and pass 3 game days. Then mount a parent
   mid-way and check its breeding clock survives the ride.
4. **Save and reload** while mounted and while hitched: the rider comes back on foot beside the animal (Transport), the hitch should hold.
5. **Skiff** (optional): console **Insert**, `tp -120780 -17640`, **F10 → Board nearby vessel**, **Sail to camera target**.

Report Transport issues on [its repo](https://github.com/mattresspad/MeitouTransport/issues) with versions, load order and log lines;
Husbandry ones here.

## What went wrong before

- **Empty `engine` / `build/gutenstructure`.** A plain `git clone` leaves both submodules empty and the build stops at the
  `Meitou.Frameworks.targets` import. Clone with `--recurse-submodules` or run `git submodule update --init --recursive`.
- **Building Husbandry from MeitouMods.** Husbandry left MeitouMods (its `beta` now holds only Rivalry Core); the local MeitouMods checkout is
  63 commits behind and still has the old copy. Its Play deploys to the same `mods\Husbandry` folder, so whichever builds last wins. Build
  Husbandry only from its own repo; `git -C "$Repos\MeitouMods" pull` drops the old copy.
- **Transport isn't deployed by any Play.** Its build stops with "Set MEITOU_DIR …" unless `MEITOU_DIR` points at a v0.20.1 game, and it has
  to be extracted into `mods` by hand.
- **The wrong Meitou.** The launcher's Play button runs its installed release, not this repo's engine, and a launcher update to v0.21 or
  later refuses Transport. Test with **Play (engine + mods)**; keep the launcher on 0.20.1 until Transport ships a newer pin.
