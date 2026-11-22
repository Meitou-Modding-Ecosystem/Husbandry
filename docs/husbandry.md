# Husbandry (content mod)

Hitching posts and breeding for the player's animals, and an animal trader in Mourn (`husbandry`, no dependencies). Code: `Husbandry/`; data file generator: `Husbandry.Data/`.
Needs Meitou 0.22.2 or later (nightlies included), below 0.23 (`gameVersion`; code-mod API 3, for `World.GiveOrder`); built against the
engine fork's `fork` branch, the engine assemblies stamped 0.22.2.0 (`Directory.Build.rsp`). Uses the engine's mod jobs (`AddJobType`,
`AddJobOffer`; task type 4501), `PopulationSystem.SpawnIntoPlayerSquad` with an age and `SpawnPlatoon` (engine docs/modding.md "Mod jobs",
"World hooks").

## Setting up a ranch

1. Research **Animal Husbandry** (Core, level 1, no blueprint). It unlocks the **Hitching Post** (build menu, Farming).
2. Build a post and an **Animal Feeder** (vanilla, research Item Storage) inside the pen, and stock the feeder with food.
3. Select animals, right-click the post and choose **Hitch here**. Each one lists the "Hitched" job on the post in its jobs list; the X unhitches it.
4. Keep the squad's **Feed Animals** option on: hitched animals walk to the feeder themselves when hungry (vanilla goal 243, food from storage in
   a town of the player's; the post creates a player town).

## Pen

A hitched animal standing idle more than **Pen radius** (setting, default 100 game units) from its post walks back to within half the radius;
one inside strolls to another spot now and then (setting **Grazing**). Anything else it is doing comes first, so it leaves the pen only for:
a food run to a feeder or food on the ground, a fight, a job of its jobs list (Follow or Bodyguard: remove it to keep the animal home), or a
player's order (riding it with MeitouTransport is one). It comes back once it stands idle again. Checked once a game second per animal.

Distances: 1 game unit is about 10 cm (engine docs/formats/terrain.md). Large farms' crop grids measure 81 x 72 (Wheat L) to 91 x 91 (Vegetable
L), about 90 x 90. Four of them by area are 180 x 180; a circle of that area has radius about 100.

## Breeding

- **Adults**: age 0.6 or more, the game's "Adult" label (below 0.4 "Pup", below 0.6 "Teen"); elders too.
- **Sex**: the game gives animals none (no ANIMAL_CHARACTER has `female chance`; Meitou draws every animal male, engine docs/game/animals.md
  "Breeding and eggs"), so the mod decides it half and half from the character's portrait serial, which is saved with the character. A character
  loaded from a save that has no serial gets a new one on load, so its sex is fixed from the first save made with Meitou. An animal the
  Beastmaster sells keeps the sex it was sold as instead (kept by serial in the sidecar). The character panel shows it on every animal's Age line ("Adult (female)",
  and a "Sex:" row in its tooltip): a Harmony postfix on the engine's `HudTips.Lines`, keeping the panel's number of lines.
- **Same kind**: same RACE. Pack Beast and Garru share Garru; Domesticated Bull and Pack / Wild Bull are different races. Robot races (Iron and
  Security Spiders, the Cleanser) never breed.
- **Tries**: each hitched adult female with an adult male of her race at the same post tries every **Days between tries** (default 3) game
  days, the first try that long after she is paired; she loses her clock when unpaired. The chance follows the hungrier of her and the best-fed
  male: hunger 2 or more (well fed) 50 %, 1 to 2 (hungry) 25 %, below 1 (starving) 10 % (settings).
- **Birth**: at once, no pregnancy. The pup is the mother's record, aged 0.05, born 12 units from her in her squad (a player character),
  hitched to her post, and "<mother> has given birth to a pup at the hitching post." is posted. None while the faction is at its head cap.

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

## Load scan

Any of the player's animals can be hitched, those of other mods included: the job takes every animal (the engine's `SquadRegistry.IsAnimal`), so
no list per kind is kept. At load the mod logs the animal kinds and races of the merged data, robots marked.

## Settings

Pen radius (30 to 600), Grazing, Breeding, Days between tries (1 to 30), the three chances, and Beastmaster of Mourn (on by default).

## Debug rows (F10)

- **Husbandry: hitched animals**: each hitched animal's race, sex, age, hunger and next try, then the latest tries (also in the log).
- **Husbandry: Beastmaster**: where his pen is, whether he is placed, and each stock place (in the pen, filling, or sold and back at an hour).
- **Husbandry: try breeding now**: every paired female tries at the next check.

## Save data

The sidecar holds the hitches (portrait serial, post id), each paired female's next try (game hours), the sexes the mod set (portrait serial,
female) and the Beastmaster's six stock places (portrait serial, refill hour), version 2; a version 1 sidecar loads with none of the last two. The engine
saves the hitch jobs too (as mod jobs of task type 4501); the sidecar's hitches go back on their characters at the first tick after a load, so
saves from before 0.3.1 keep theirs. Both are in the world's state hash. A stock platoon is found again by its tag; a place whose animal is not
found after a load adopts its platoon's animal again.

## Data file

`Husbandry.mod`, record ids `<n>-Husbandry.mod`: 1 the research, 2 the post (a copy of vanilla Prisoner Pole, `18913-small_changes_otto.mod`,
without its cage function, category FARMING), 3 the Beastmaster, 4 his squad, 5 his conversation, 6 to 18 its lines and the DA 48 and DA 1
actions, 19 his shop's vendor list, 20 to 22 his stock's squads (Garru, Crab, Dog). A `.mod` generated before 0.2.0 lacks 3 to 19, one before
0.2.5 lacks 17 to 19 (no pack shop; logged), one before 0.3.1 lacks 20 to 22 (no stock; logged): generate it again. Generated from the installed game: `dotnet run --project Husbandry.Data -- "<Kenshi>" "<out>"`;
the Play project does it on F5. Untested in play.
