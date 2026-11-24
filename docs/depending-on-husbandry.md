# Depending on Husbandry

How a code mod adds trainable skills to Husbandry (`husbandry`): reference it, declare it, register in `Initialize`. Animal kinds (what each
race supports), sexes and hitching posts are Animal Behaviors' (`animal.behaviors`, Husbandry's own dependency): register animals there
([depending-on-animal-behaviors.md](https://github.com/Meitou-Modding-Ecosystem/AnimalBehaviors/blob/beta/docs/depending-on-animal-behaviors.md)); loader rules: engine
[modding-dependencies.md](https://github.com/Meitou-Modding-Ecosystem/MeitouClient/blob/master/docs/modding-dependencies.md). An agent
skill walks through the same steps: [.claude/skills/husbandry-dependent](../.claude/skills/husbandry-dependent/SKILL.md).

## 1. Reference it at build time, never ship it

In a repo made from [MeitouModTemplate](https://github.com/Meitou-Modding-Ecosystem/MeitouModTemplate), add Husbandry's release zip to
`meitou.lock.json`, after the `animal.behaviors` entry its notes name; the build downloads, checks and references it, and Play deploys it. Every value comes from the release's notes
(`husbandry-v<version>`):

```json
{
  "id": "husbandry",
  "repo": "Meitou-Modding-Ecosystem/Husbandry",
  "tag": "husbandry-v0.8.0",
  "asset": "Husbandry-0.8.0.zip",
  "sha256": "<Zip SHA-256 from the notes>",
  "folder": "Husbandry",
  "engine": "<Submodule engine commit from the notes>"
}
```

- Your `engine/` pin moves to that engine commit (GutenStructure
  [framework lock](https://github.com/TheIvoryDisaster/GutenStructure/blob/beta/docs/framework-lock.md)).
- Never list `Husbandry.dll` in your own `assemblies`: the loader refuses the mod. The loaded `husbandry` serves the types, so every
  dependent shares one registry.
- Outside the template: copy `Husbandry.dll` from the zip to `lib/Husbandry.dll` and reference it with
  `<Reference Include="Husbandry" HintPath="../lib/Husbandry.dll" Private="false" />`.

## 2. Declare it in `meitou.json`

```json
"apiVersion": 3,
"dependencies": [{ "id": "husbandry", "version": ">=0.8.0 <0.9.0" }]
```

- 0.8.0 is the first version with this API (0.7 also registered animals; that moved to Animal Behaviors). Before 1.0 a minor version may break it, so cap the range at the next minor.
- Husbandry must sit above your mod in `data/mods.cfg` (the launcher's Arrange does it). A mod that works without it declares
  `"optional": true` and reaches it only after `c.HasDependency("husbandry")`, in a method of its own (engine docs, "Using a dependency").
- Players who get Husbandry baked into another download get the unchanged `Husbandry/` folder of a release zip: never rename its id or merge
  its DLL into yours.

## 3. Register in `Initialize`

```csharp
public void Initialize(ModContext c)
{
    var husbandry = c.Dependency<IHusbandryApi>(HusbandryMod.Id)!.For(c);
    var animals = c.Dependency<IAnimalBehaviorsApi>(AnimalBehaviorsMod.Id)!.For(c);

    // A race of your mod: every feature (breeding, training, capsules).
    animals.AddAnimal(new AnimalKind("12-mymod.mod", AnimalSupport.All, "my sand lizard"));
    // A vanilla race, by name: no capsules for it once your mod loads.
    animals.AddAnimal(new AnimalKind("Beak Thing", AnimalSupport.Breeding | AnimalSupport.Training, "too wild to keep"));

    // A skill the Training Post teaches, only to garru and bulls.
    var herd = new ModSkill("mymod.herd", "Beast: Herd", SkillColumn.Sciences) { Description = "...", Start = 0 };
    husbandry.AddSkill(herd, MyMod.HerdJob, ["Garru", "Bull"]);   // a TaskType from your own number block
}
```

- **Animals** (Animal Behaviors): `AnimalKind(race, support, note)`. `race` is the RACE record's string id or its name (without case); an id
  beats a name. The last registration for a race wins, by load order. Husbandry reads `Breeding`, `Training`, `Taming` (catch a wild,
  knocked-out one) and `Summoning` (store and summon one of the player's). Declare `animal.behaviors` in your `dependencies` too.
- **Skills**: `AddSkill(skill, trainingType, races)` registers the skill with the game under your mod, claims `trainingType` as the "Training
  Herd" job and offers it on the Training Post as "Train: Herd" (the label after its colon, at most 32 characters with "Train: "). `races`
  (string ids or names) limits who learns it; null means every race that supports training. The job type is yours: take it from your mod's
  number block ([mod number blocks](https://github.com/TheIvoryDisaster/GutenStructure/blob/beta/docs/numbers.md); Husbandry's is 4500 to 4599).
- What the skill does is your code: read it in your own system with `AnimalSkills.Level(cold, skill)` / `AnimalSkills.Has(cold, skill)`
  (unlocked at `AnimalSkills.Unlocked`, 10), train it by use with `AnimalSkills.Gain`. Your own work jobs can use Husbandry's buildings
  (`HusbandryData.TrainingPost`, `PetStation`, `BonePile`) and helpers (`Training.AnimalWork.Holders`, `Free`, `Walk`, `Say`, `Refused`).
- **Reading**: `Skills`; what an animal supports is Animal Behaviors' `SupportOf(race)`.
- Registrations only in `Initialize`; later ones throw. When your `Initialize` throws, Husbandry drops what you registered
  (`IModDependents`).

## 4. Check it

`dotnet build` (warnings are errors), your tests, then in game F10 **Animal Behaviors: supported animals** lists every race with what it supports;
the log has the same list once the first world is built.
