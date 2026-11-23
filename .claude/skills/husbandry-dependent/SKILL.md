---
name: husbandry-dependent
description: Use when making a Meitou code mod depend on Husbandry, or adding animal kinds, breeding, taming, capsule or Training Post skills for animals to a mod.
---

# Build on Husbandry

Husbandry (`husbandry`) owns hitching, breeding, the Training Post and Beast Capsules. Your mod tells it which races may do what and adds
skills the Training Post teaches; it never copies or replaces Husbandry. Read `docs/depending-on-husbandry.md` first, and
`docs/husbandry.md` "Supported animals".

## Hard rules

- One framework: reference `Husbandry` with `Private="false"` (the framework lock does); never list `Husbandry.dll` in your `assemblies`,
  never rename `husbandry`, never fork its code into your mod.
- Register only in `Initialize`, through `c.Dependency<IHusbandryApi>(HusbandryMod.Id)!.For(c)`; later calls throw.
- A skill's training job type is yours to claim: 1000 or more, clear of Husbandry's 4501 and 4510 to 4522 and other mods' ranges.
- Determinism: a skill's effects run in your system's serial step; randomness from the engine's `Rng` with your `c.RngPurpose`.

## Steps

1. **Manifest**: `apiVersion` 3 and `"dependencies": [{ "id": "husbandry", "version": ">=0.7.0 <0.8.0" }]` (match the current
   `Husbandry/meitou.json` minor). Tell the user Husbandry must load above the mod in `mods.cfg`.
2. **Project**: add a `husbandry` entry to `meitou.lock.json` from the release notes of `husbandry-v<version>` (tag, asset, SHA-256,
   folder `Husbandry`, engine commit), and move `engine/` to that commit.
3. **Animals**: `AddAnimal(new AnimalKind(race, support, note))`, race = RACE string id (preferred for your own races) or name (vanilla).
   Flags: `Breeding`, `Training`, `Taming`, `Summoning`, `All`, `None`. The last registration for a race wins.
4. **Skills**: `AddSkill(new ModSkill(id, "Beast: X", column) { Description, Start = 0 }, (TaskType)n, races)`; Husbandry registers the skill,
   the "Training X" job and the "Train: X" offer under your mod. Write the skill's effect in your own `AddSystem` system: read it with
   `AnimalSkills.Has` / `Level`, train it by use with `AnimalSkills.Gain`.
5. **Docs**: list the mod's kinds and skills in its own docs; a change to Husbandry's API updates `docs/depending-on-husbandry.md`,
   `docs/husbandry.md` and this skill, and bumps Husbandry's minor version.
6. **Check**: `dotnet build`, `dotnet test`; in game F10 **Husbandry: supported animals**, and the Training Post's menu shows "Train: X".
