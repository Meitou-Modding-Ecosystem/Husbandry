# Husbandry (content mod)

Hitching posts, breeding, training and capsules for the player's animals, and an animal trader in Mourn (`husbandry`). Code: `Husbandry/`; its
records are added at load (Data file). Built on **Animal Behaviors** (`animal.behaviors`, locked in `meitou.lock.json`), which owns the
supported animals, animal sexes, the Hitched job, pens and grazing ([its docs](https://github.com/Meitou-Modding-Ecosystem/AnimalBehaviors/blob/beta/docs/animal-behaviors.md)).
Other mods extend Husbandry with trainable skills: [depending-on-husbandry.md](depending-on-husbandry.md).
Needs Meitou 0.22.2 or later (nightlies included), below 0.23 (`gameVersion`; code-mod API 3, for `World.GiveOrder`); built against the
engine fork's `fork` branch, the engine assemblies stamped 0.22.2.0 (`Directory.Build.rsp`). Uses the engine's mod jobs (`AddJobType`,
`AddJobOffer`; task types 4510 to 4515, 4520 to 4523), mod skills and status effects, `PopulationSystem.SpawnIntoPlayerSquad` with an age and `SpawnPlatoon` (engine docs/modding.md "Mod jobs",
"World hooks").

## Setting up a ranch

1. Research **Animal Husbandry** (Core, level 1, no blueprint). It unlocks the **Hitching Post** (build menu, Husbandry).
2. Build a post and an **Animal Feeder** (vanilla, research Item Storage) inside the pen, and stock the feeder with food.
3. Select animals, right-click the post and choose **Hitch here** (Animal Behaviors' job). Each one lists the "Hitched" job on the post in its
   jobs list; the X unhitches it. A post holds at most 6 animals.
4. Keep the squad's **Feed Animals** option on: hitched animals walk to the feeder themselves when hungry (vanilla goal 243, food from storage in
   a town of the player's; the post creates a player town).

## Hitching Post

The post is marked as an Animal Behaviors hitching post holding **6** animals (`Sites.MarkHitch` in the data phase): the framework offers its
Hitched job there, keeps the animals in a pen round it (settings **Pen radius**, **Grazing**: Animal Behaviors' docs "Hitching") and turns a
seventh away with "This post is full (6 animals)" over it.

- **Animals button**: on a selected post of the player's, **Animals (n/6)** opens a window listing each animal hitched there: name, race, sex,
  age (Pup, Teen, Adult), hunger (well fed from 200, hungry from 100, starving below; out of 300 as the character panel shows it), what it is
  doing (getting food, resting or moving in the pen, out of the pen, down) and a paired female's hours to her next litter. Refreshed once a
  game second (`PostRoster`, `HitchingPostWindow`).
- **Look**: see "Own models" under Training.

## Breeding

- **Adults**: age 0.6 or more, the game's "Adult" label (below 0.4 "Pup", below 0.6 "Teen"); elders too.
- **Sex**: Animal Behaviors' (half and half from the portrait serial; its docs "Sexes"). An animal the Beastmaster sells keeps the sex it was
  sold as (written to the framework's set sexes).
- **Same kind**: same RACE. Pack Beast and Garru share Garru; Domesticated Bull and Pack / Wild Bull are different races. Only races that
  support breeding (Supported animals) breed: by default not robots (Iron and Security Spiders, the Cleanser).
- **Tries**: each hitched adult female with an adult male of her race at the same post tries every **Days between tries** (default 3) game
  days, the first try that long after she is paired; she loses her clock when unpaired. The chance follows the hungrier of her and the best-fed
  male: hunger 2 or more (well fed) 50 %, 1 to 2 (hungry) 25 %, below 1 (starving) 10 % (settings).
- **Birth**: at once, no pregnancy. The pup is the mother's record, aged 0.05, born 12 units from her in her squad (a player character),
  hitched to her post, and "<mother> has given birth to a pup at the hitching post." is posted. None while the faction is at its head cap.
  When the post already holds 6 animals the pup stays unhitched, "This post is full (6 animals)" floats over it and "<mother>'s pup could
  not be hitched: the post already holds 6 animals." is posted.
- **Play-tested** (Observed, Matthew's "Settlement Test" save, 2026-11-23): a garru pair hitched to one post bred; the post then held three
  hitched animals.

## The Beastmaster of Mourn

A Western Hive soldier drone, the **Hive Beastmaster**, stands in Mourn's bar and sells garru, crabs and bonedogs, a male and a female of each,
kept in a pen behind the bar, and every animal pack.

- **Where**: Mourn is TOWN `963-gamedata.base` (editor name "Capital"); its bar is the home of the barman's squad `Bar Mourn`
  (`95554-Mourn.mod`), else the town's first bar. The town's housing picks that building, so the mod finds it in the running world, once
  Mourn's zone is loaded and the bar's doors are registered. His spot is the bar's first general node. The bar's floor is its lowest door's
  height. The pen's centre is fixed in the bar's frame: 56 units along its facing and 154 to its left, the ground behind the bar where a beak
  thing stood in play (Observed, Matthew's save 2026-11-19: 45 and 123, 46 under the floor), pushed a quarter further out. When the ground
  within 30 units of it is not open, the spot is turned round the bar by up to 90 degrees or pushed up to 80 units further. Open ground is
  walkable with the highest walkable surface at most 15 units over the bar's floor. Mourn lies in a pit, its roofs about level with the
  terrain round it (Observed, 2026-11-19: roof at 1100, floor at 1033), so a test against the terrain let stock onto the roof. Pen radius 50.
- **Him**: CHARACTER `3-Husbandry.mod`, a copy of the game's Western Hive "Soldier Drone" (`42196-rebirth.mod`) in Masterwork (armour grade
  5) Crab Armour and Crab Trousers, a Hiver Chain Shirt and a Naginata of Cross make (its only model, Meitou, is the top grade; a soldier
  drone wears no hat or shoes), combat skills 80, strength 75. Squad `4-Husbandry.mod`: him alone, Western Hive, no AI package. The mod adds
  it with `PopulationSystem.SpawnPlatoon` behind the bar (tag `husbandry.beastmaster`, saved with the platoon), holds it
  (`HoldPlatoon`, off screen only) and walks him back to his spot with a move order (`World.GiveOrder`, which holds him against his squad's
  mind until he arrives; with no package he stands idle after) whenever he stands idle away from it; more than 150 units from it and out of a fight he is put straight back
  there (a failed walk can leave him across town). A killed
  Beastmaster is not replaced.
- **Talk**: his conversation (DIALOGUE `5-Husbandry.mod`) is a hiver's talk of breaking wild beasts for the Hive; **Show me your beasts.**
  runs DA 48 CHOOSE_ANIMALS_BUYING, the game's animal buying window, which lists the animals of his faction and town within 3000 units
  (his stock) and his squad's within 2304 (engine docs/game/animals.md "Buying animals"). Prices are the game's: 14000 x (age + 0.1). **Show me your packs.** runs DA 1 TRADE, his shop.
- **Packs**: his squad is a trader's (`is trader`, he and it) with one empty VENDOR_LIST (`19-Husbandry.mod`), `vendors fill total amount` 0
  and `vendors refresh time` 24, so the engine gives it a squad shop that fills with nothing and a purse of 3000 (the squad shop minimum). After
  each fill the mod adds one of every animal pack the stock lacks: every CONTAINER whose `races` names a race of an ANIMAL_CHARACTER (only such
  a list lets an animal wear an item, the engine's `RaceLimiter`), found in the merged data at his first check and logged, so other mods'
  packs are sold too. Prices are the game's. A day later the fill clears the stock (goods sold to him too) and the packs come back.
- **Stock**: six places, a female and a male each of Garru (`42201-rebirth.mod`), Crab (`56088-Newwworld.mod`) and Dog, the bonedog
  (`3979-gamedata.base`). Each is a held platoon of its own (`SpawnPlatoon` from its SQUAD_TEMPLATE, `20-Husbandry.mod` to `22-Husbandry.mod`: the animal
  alone, of his faction and home town, aged 0.8, adult; tagged `husbandry.stock.<place>`) on a ring 20 units round the pen's centre. Once its
  platoon has loaded, the animal is named "Garru (female)" and so on, its sex kept by the mod. An idle one more than 50 units from the centre walks back (a move order, as his); one
  inside strolls now and then; one standing more than 15 units over the bar's floor (on its roof or upper floor) is put back on its ring spot.
- **Buying**: the window makes the bought animals the player's (the game's recruit). At the next check (once a game second) the mod sees the
  animal has become the player's, puts it on the ground 40 units outside the bar's lowest door (along the door's normal, away from the bar; 25
  when that is not open ground; no path from the pen to the buyer is sure) and orders it to follow the player character nearest the
  Beastmaster (the buyer, who stands talking to him). A player's animal bought from him (its sex is in his table) standing 15 to 150 units over
  the bar's floor within 300 units of the door, out of a fight, is put there too: stock sold from the roof by v0.2.1 could not walk down.
  Its place fills again 24 game hours later. A place whose animal is gone without being sold (it
  died, or the squad left the world and came back without it) fills at once. The window's "Gender:" line shows the mod's sex (a Harmony
  postfix on the engine's `CharacterTrading.OpenCharacterTrade`; the game reads the look's `sex female`, which no animal has).

## Training

Animals learn six skills, each a mod skill in the stats window (`husbandry.fetch`, `.guard`, `.hunt`, `.scout`, `.pack`, `.tieup`; labels
"Beast: ..."), 0 until trained. A skill is **unlocked at 10**; below that the animal refuses the work that needs it (the job comes off with a
floating "Needs Fetch 10" line). Skills train on the game's curve (`ModSkill.Gain`), are saved and hashed by the engine with the character.
The **Animal Husbandry** research also unlocks the three buildings: Training Post `30-Husbandry.mod` (no function), Pet Station `31-` (no
function), Bone Pile `32-` (a Raw Meat machine, below). Each wears its own model when the deployed data has it, else a game building's (the stand-in):
Training Post on Training Dummy, else Prisoner Pole; Pet Station on Bedroll, else Prisoner Pole; Bone Pile on Wooden Barrel, else Storage:
General. Build cost always comes from the stand-in (`TrainingData`). Checked once a game second; a job on a building
that is gone or not the player's comes off.

Own models: `Husbandry/Structures/training-post.glb`, `pet-station.glb`, `bone-pile.glb`, each with a `.json` (`research` `none`, so only the
Husbandry building shows in the build menu). Play's structure import ([structures.md](structures.md)) turns each into a building, ids
900200-, 900100- and 900000-Husbandry.mod (`Structures/ids.json`), which the data phase copies the look of. Heights 2.64 m, 1.62 m and 1.08 m:
the art prompts' sizes (project file `husbandry-training/art-prompts.md`) times 1.2, as for the Bathhouse's pieces, the Pet Station by its
width. Textures are shrunk to 2048.

The Hitching Post's look is `Husbandry/Structures/hitching-post.glb` (building 900400-Husbandry.mod, 1.3 m, the model's own height), on the
Prisoner Pole's build cost. Its textures stay at 4096 (`textureSize` in `hitching-post.json`): the badger painted on the sign is baked in many
small pieces across the atlas, and 2048 blurs it. Concept art: `concept-art/post-*.png`.

- **Training Post**: right-click it with animals selected and choose **Train: <skill>** (job types 4510 to 4515, "Training <skill>"; skills
  of other mods offer their own). An animal whose race does not support training, or that a skill's race list leaves out, comes off the job
  with a floating "A <race> can't learn <skill>" (Supported animals). The animal keeps to a pen round the post as a hitched one does (Animal Behaviors' Pen radius and Grazing) and learns only from a trainer.
- **Trainers** (job 4523 "Training animals", **Train animals here** on the Training Post, people; `Trainers`): while an animal of the player's
  holds a training job on the post, the trainer walks to 30 units in front of it and works there (a job of the chooser, `IJobSource`; it stands
  down when no animal trains there). Each working trainer trains one animal of the post (animals and trainers in slot order); an animal without
  one floats "Waiting for a trainer" each game hour. The animal gains, per game hour, 0.2 from a trainer at **Animal Handling** 1 (mod skill
  `husbandry.handling`, people start at 1) rising in a line to 0.8 at 50 or more: about two game days, or half a day, from 0 to 10. The
  trainer gains 0.5 Animal Handling a game hour. **Treats** (`Treats`): at each game hour mark the trainer feeds one Raw Meat, from its own
  inventory (and backpack) first, else from the nearest finished storage of the player's within 300 units of the post; with none at hand the
  animal learns at half rate and the trainer floats "No Raw Meat for treats".
- **Fetch** (job 4520 "Fetching", offered on every storage building of the merged data as **Fetch for here**): an idle animal free of the
  player's orders carries the output of the player's finished machines within 3000 units of the storage that the storage takes, nearest machine
  first, into the storage. It carries in its pack only (a live animal's main grid takes nothing, engine `Inventory.Add` `mainShut`), so without a
  pack the job comes off. At 80 units of a building's centre it is there; after three walks to one target that end short, that target is skipped
  for a game hour. +0.5 Fetch a load.
- **Guard** (job 4521 "Guarding", **Guard here** on the Pet Station): the animal keeps to a pen round the station and, while within 2000 units
  of it (200 m), has the status effect **Guarding home** (+30 melee attack and defence). It trains 0.2 a game hour there, 2 in a fight.
- **Hunt** (job 4522 "Hunting", **Hunt from here** on the Bone Pile): the animal rests in a pen round the pile, then runs to a walkable spot
  1500 to 12000 units away (1200 m), turns back on arrival or after 3 game hours, and drops raw meat (the game's ITEM named "Raw Meat"; one
  piece plus one per 25 Hunt) into the pile's output, on the ground by it what does not fit. It rests 8 game hours at Hunt 10, down to 2 at 100. +2 Hunt a hunt.
  The pile has no storage of its own: it is a machine (BUILDING_FUNCTIONALITY `38-`, function 4 refinery, `production mult` 0 so it makes
  nothing by itself, `max operators` 0 so it is automatic) whose one output slot holds 20 Raw Meat (none when the game has no item named Raw
  Meat). Haulers and Fetch animals take from it as from any machine's output and never put anything in. Its output bar shows the furthest
  hunter's cycle: resting fills it to half, the run out to three quarters, the run back to nearly full.
- **Scout**: the engine lets any character sneak (the original never lets an animal: engine docs/game/animals.md); the mod turns sneaking off
  for an animal below Scout 10. A scout gets **Scout trained**, +3 stealth per 10 levels, and trains 0.3 a game hour while sneaking. It has no
  sneaking animation.
- **Pack**: **Pack trained**, +3 strength per 10 levels (more carry weight). Trains 0.3 a game hour while its pack holds anything.
- **Tie up**: a melee hit landed by an animal with Tie up 10 or more knocks the foe out (`MedicalState.KnockOut`) with chance 0.25 % per level
  (2.5 % at 10, 25 % at 100), for 0.02 x level + 0.45 seconds, at most 2. Hits are observed with `ICombatHitObserver` and the knock-downs applied
  in the next serial step. +0.05 Tie up a landed hit. Aiming at the legs and severing one need an engine hit hook (Husbandry #40, #41).
- **Mount training**: not built; it belongs with Meitou Transport's riding.

Trips (fetch walks, hunts) are not saved: after a load a hunter starts resting again.

## Beast Capsules

ITEM `33-Husbandry.mod` **Beast Capsule**, the empty capsule (500 cats, one inventory cell, `stackable` 5; a copy of the game's Fertiliser,
else Leather or Hemp, for its weight and other fields), and ITEM `39-` **Filled Beast Capsule** (`CapsuleData.Filled`: a copy of it with
`stackable` 1, no recipe, and the icon `husbandry-beastcapsule-filled.png`, the same picture with a bright red outline, made by
`tools/make-icons.py`). Its inventory icon is `Husbandry/items/icons/husbandry-beastcapsule.png` (`tools/make-icons.py` from
`concept-art/beastcapsule-inventory.png`; record `icon`, `auto icon image` false), which Play copies into the deployed mod (`PlayDeployAssets`).
Its look is its own model, `Husbandry/ItemModels/beast-capsule.glb` (`size` 0.11 m in `beast-capsule.json`, textures 1024): the import
makes mesh `husbandry_beast_capsule.mesh` and MATERIAL_SPECS_CLOTHING `950000-Husbandry.mod` (`ItemModels/ids.json`), and when the data has
that material the data phase sets the capsule's `mesh`, `ground mesh` and `material` to them (else it keeps the stand-in's look). The Beastmasters keep 5 in their shops after each fill.

**Capsule Bench** (BUILDING `36-`, BUILDING_FUNCTIONALITY `37-Husbandry.mod`, build menu category HUSBANDRY, unlocked by Animal Husbandry,
which also enables the capsule's craft with `enable item`: a bench lists in the Crafting tab, and makes, only research-enabled crafts) crafts
capsules: two Iron Plates and one Leather (`ingredients` 200 / 100), 4 game hours each (`production time`). The game prices a crafted item at its
ingredients' worth times 1 + `profitability`, ignoring `value`, so the data phase sets `profitability` from the ingredients' worth in the loaded
data to keep the capsule at 500 cats (`BenchData.AddRecipe`; 0.17 on the base game). Its functionality is the game's `medical crafting basic`
with only the capsule listed and no Fabrics input; `use range` 10. Its look is `Husbandry/Structures/capsule-bench.glb` (1.2 m, textures 2048;
building `900300-Husbandry.mod`) with two use nodes 10.4 units out at local +Z (the toolboard is at -Z), 3 units either side of its middle (the
game reads use nodes as the operator's spot only with two or more); it keeps the Basic Medical Workbench's
build cost (3 Iron Plates) and power draw (5), and is that bench when the import has not run.

- **Catch**: right-click an empty capsule in a character's own inventory (engine item use) within 40 units of an animal that is one of the
  player's whose race supports summoning, or is knocked out (unconscious) and of a race that supports taming (Supported animals), not dead,
  carried or already out of a capsule. The nearest goes in: what it carried drops where it lay, one empty capsule of the stack is used up,
  the character is removed from the world and a **Filled Beast Capsule** bound to it drops on the ground where it lay, to be picked up.
- **Throw**: right-click a knocked-out beast a capsule may catch and choose **Throw Beast Capsule**: the first selected character holding an
  empty capsule walks to within 300 units (30 m) of it (a move order, renewed every 2 s), stops (a move order to where it stands) and plays
  the throw: the clip is 1.7 s, the hand lets go 0.9 s in and the capsule flies at 300 units a second (`CapsuleThrows.LandAt`). Each tick until
  the clip has ended and the capsule has landed the thrower is turned to face the beast (`CharacterHot.Yaw`). Before the landing, another
  order (a new order in hand, or a goal more than 5 units from where it stopped) or a hit on the thrower (`ICombatHitObserver`) interrupts the
  throw: it fails with "Throw interrupted" and the capsule is kept. In flight a red streak runs from the hand (`CapsuleThrows.Hand`, in the
  thrower's frame) to the beast on an arc a fifth of the distance high (`CapsuleFlights`: one small puff of `Poison_Gas_White` tinted red a
  tick, from `AddFrame`; display only: the engine has no way for a mod to draw a flying item). On landing it catches as above, except 20% of
  the time (`BreakFreeChance`, rolled from the world seed, the beast and the tick): the beast breaks free, its KO timer ends and its vital parts
  (flesh − stun + rig, stun first) and blood come up to 1, just enough that it stands and fights; the capsule is kept. Where it lands a pink
  mist rises for 3 s (the same particle system tinted, drawn by `CapsuleUi` from the landings the system queues). The throw plays the clip
  `husbandry_capsule_throw` (a disabled COMBAT_TECHNIQUE, so no fight picks it, played as a mod combat move; `ThrowData`) from
  `items/animations/husbandry_throw_male.skeleton` / `_female.skeleton`, an ANIMATION_FILE the data phase links to every race whose `male mesh`
  is under `data/character/meshes/` (humans, shek, hivers, skeletons, fishmen: male_skeleton / female_skeleton bodies; not the Hive Queen).
  The clip is Meshy's Ball_throw retargeted onto both skeletons with the Motion Lab (Meitou-Modding-Ecosystem/PZAKenshiMotionLab,
  `rig --reference`, then `motionlab.kenshi_export`), cut to 0.5-2.2 s of the source, played in place and turned so the hand lets go straight
  ahead. A throw also ends with a line over the thrower when the beast gets up, is carried or leaves, the thrower is down or has no empty
  capsule left, or after 2 minutes. Throws in progress are not saved.
- **Summon and recall**: a filled capsule stays bound to its beast. Right-click it while the beast is inside: the beast comes out 15 units
  from the character as a new member of the character's squad (`SpawnIntoPlayerSquad` at its age), with its name, sex, stored stats and mod
  skill levels, fully healed, and the capsule keeps its body's lasting id (`LastingIds`). Right-click it while the beast is out and within
  300 units (30 m): the beast goes back in with its stats and skills as they are now (what it carried drops). A beast that died or is gone
  for good leaves the capsule unbound, and its next click turns it into an empty capsule. A capsule acts at most once a second
  (`CapsuleSystem.SettleTicks`, 30 ticks): a click before then, or any further click while paused, says "Wait a moment" and does nothing.
  No room in the squad or the faction: the beast stays in. The click acts at once, in the item use's serial step.
- **Tooltip**: a filled capsule's tooltip (`CapsuleTooltip`, engine item tooltip lines) shows what it contains (name and kind), sex and age
  (adult from 0.6), whether the beast is in or out, Str, Tough, Dex, Atk, Def and Ath, the mod skills trained to 1 or more and its traits.
- **Beast traits**: a capsule's beast keeps a list of traits (`StoredBeast.Traits`: id and value), saved with it and kept while it is out.
  `BeastTraits.Register(new BeastTrait(id, label, format))` names one for the tooltip; nothing gives traits yet (rare catches, breeding and
  specialised training will).
- A filled capsule's `ModTag` is `husbandry.capsule:<key>`; the beasts are kept by key in the sidecar. An older save's full capsule (the empty
  record with a key) still works, and becomes a Filled Beast Capsule at its first click. Debug row **Husbandry: capsules** lists them.

## Supported animals

Animal Behaviors keeps the list (its docs "Supported animals"; debug row **Animal Behaviors: supported animals**). Husbandry reads its flags:
Breeding (pairs breed at a Hitching Post), Training (it learns skills at the Training Post), Taming (a wild one, knocked out, can be caught in
a Beast Capsule) and Summoning (one of the player's own can go into a Beast Capsule and come out again). Mods register animals with Animal
Behaviors; trainable skills with Husbandry ([depending-on-husbandry.md](depending-on-husbandry.md)).

## Building panel

A selected Husbandry building shows bars under its Condition (engine `AddBuildingRows`; `BuildingStatus`, rebuilt each training check, once
a game second; at most 6 bars a building, the engine's cap). The hook is only in the MeitouClientSandbox fork so far: the build defines
`MEITOU_BUILDING_ROWS` when the engine it builds against has it (`tools\Switch-Engine.ps1`), and an upstream build has no bars:

- **Training Post**: per training animal "<name>: <skill>", filled towards 10 ("7.3/10") then towards 100, tooltip its trainer or "Waiting
  for a trainer"; under it "Trainer <name>" with Animal Handling (out of 100).
- **Bone Pile**: per hunter "<name>: Resting / Out hunting / Coming back", its cycle as the output bar shows it, and its Hunt.
- **Pet Station**: each guard's Guard.
- **Hitching Post**: each hitched female's "next litter", filled over the breeding period, hours left as the value.
- **Capsule Bench**: the current capsule's progress, and "Crafter <name>" with the bench's `stat used` while someone works it ("No one at
  the bench" otherwise).

## Settings

Breeding, Days between tries (1 to 30), the three chances, and Beastmaster of Mourn (on by default).

## Debug rows (F10)

- **Husbandry: hitched animals**: each hitched animal's race, sex, age, hunger and next try, then the latest tries (also in the log).
- **Husbandry: capsules**: each animal kept in a Beast Capsule.
- **Husbandry: Beastmaster**: where his pen is, whether he is placed, and each stock place (in the pen, filling, or sold and back at an hour).
- **Husbandry: animal trader at settlement**: puts a Beastmaster with the same stock and shop as Mourn's at one of your settlements. Type the
  settlement's name in the row's field (any case), or its number in the list the row prints when the name is missing or shared (1 = the first
  founded); leave it empty when you have one settlement. His pen is the first patch of even, walkable ground on rings 100 to 300 units round the
  settlement's centre, he stands between it and the centre, and both appear while you are there. His platoons are homed at the town site nearest
  the settlement, which the buying window needs. One per settlement; he runs whatever the "Beastmaster of Mourn" setting says.
- **Husbandry: try breeding now**: every paired female tries at the next check.

## Save data

The sidecar holds each paired female's next try (game hours), the Beastmaster's six stock places (portrait serial, refill hour) and each
settlement Beastmaster (settlement number, name, its six places) and the beasts bound to Beast Capsules (next key; key, record, kind, age, name, sex,
stored stats, mod skill levels, traits, the lasting id of its body while out), version 6 (version 4 and 5 capsules have no kind or traits
and are all inside). Versions 1 to 4 also held the hitches (portrait serial, post id) and the set sexes (from version
2); loading one hands them to Animal Behaviors, which saves them from then on. A version 3 sidecar loads with no capsule animals, a version 2
with no settlement Beastmasters either, a version 1 with none of the last four. All of it is in the world's state hash. A stock platoon is
found again by its tag (`husbandry.stock.N` in Mourn, `husbandry.stock.outpost.T.N` at settlement T); a place whose animal is not found after
a load adopts its platoon's animal again.

## Data file

Record ids `<n>-Husbandry.mod`: 1 the research, 2 the post (a copy of vanilla Prisoner Pole, `18913-small_changes_otto.mod`, without its cage
function, category HUSBANDRY), 3 the Beastmaster, 4 his squad, 5 his conversation, 6 to 18 its lines and the DA 48 and DA 1 actions, 19 his
shop's vendor list, 20 to 22 his stock's squads (Garru, Crab, Dog), 30 to 32 the training buildings, 33 the Beast Capsule, 34 the throw's COMBAT_TECHNIQUE, 35 its ANIMATION_FILE (linked into the humanoid races' `animation files`), 36 the Capsule Bench, 37 its functionality, 38 the Bone Pile's functionality, 39 the Filled Beast Capsule. Every building (post, training buildings, bench) is in the build menu category
HUSBANDRY (`HusbandryData.Category`; the menu lists a category's name as written). The entry adds them
in the data phase (`IModDataPhase.EditData`, engine docs/modding.md "Data phase") from the merged game data as it loads (`HusbandryData.Build`),
so they follow the player's own game and mods and need no install at build time. The shipped `Husbandry.mod` is empty (release
`empty-data-mod: true`); a `Husbandry.mod` generated by an older version (0.6.0 and before) should be deleted from the mods folder, since Play
never replaces an existing one.

## Translations

Husbandry reads the language selected in Kenshi for its building and research text, job menus, Beastmaster dialogue, and animal feedback. The nine catalogs in `Husbandry/Locale/` ship inside `Husbandry.dll`. Entries use the English source as the key and preserve numbered placeholders such as `{1}` for names and levels. Missing entries use English.
