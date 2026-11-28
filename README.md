# Husbandry

Hitching posts, breeding, animal training and capsules for the player's animals in
[MeitouClient](https://github.com/Meitou-Modding-Ecosystem/MeitouClient). Mod id `husbandry`, built on
[Animal Behaviors](https://github.com/Meitou-Modding-Ecosystem/AnimalBehaviors) (`animal.behaviors`, locked in `meitou.lock.json`); what it
does, its data file and settings: [docs/husbandry.md](docs/husbandry.md). GPL-3.0-or-later.

- **Mod order:** a mod that adds races (Kenshi Races, ...) goes above Husbandry in the mod list. Husbandry links its throw and drill clips to
  the races on the human skeletons in its data phase, which only sees races of mods loaded before it
  ([docs/husbandry.md](docs/husbandry.md), "Beast Capsules").
- Adding Training Post skills from your own mod (animals are registered with Animal Behaviors): [docs/depending-on-husbandry.md](docs/depending-on-husbandry.md) (agent skill
  `.claude/skills/husbandry-dependent`).

- Build and run locally with Visual Studio, VS Code or the terminal (this repo's engine or the installed launcher, deployed to the game): [docs/play.md](docs/play.md).
- Turning `.glb` models (Meshy, Hi3D, Blender) into buildable structures and items, no FCS needed: [docs/structures.md](docs/structures.md);
  getting a generated model's polycount, size and textures right first: [docs/model-prep.md](docs/model-prep.md).
- Setting up the repos on your machine: [docs/local-development.md](docs/local-development.md).
- Instructing agents on a change that spans repos: [docs/agents-across-repos.md](docs/agents-across-repos.md).
- Agent rules for the engine pin, versions, CI and releases: [CLAUDE.md](CLAUDE.md) (same as `AGENTS.md`); step-by-step procedures:
  `.claude/skills/meitou-engine-release`.

## Numbers

Task types and dialogue codes are shared by every loaded mod; Husbandry claims only these, inside its block
([mod number blocks](https://github.com/TheIvoryDisaster/GutenStructure/blob/beta/docs/numbers.md)). `NumbersTests` checks the list. A dependent's Training Post skill (`AddSkill`) takes its job type from the
dependent's own block.

Block 4500-4599

| Number | Pool | Use |
| --- | --- | --- |
| 4510-4515 | Task type | Jobs: Training Fetch, Guard, Hunt, Scout, Pack, Tie up (offered on the Training Post, animals) |
| 4520 | Task type | Job: Fetching (offered on every storage, animals) |
| 4521 | Task type | Job: Guarding (offered on the Pet Station, animals) |
| 4522 | Task type | Job: Hunting (offered on the Bone Pile, animals) |
| 4523 | Task type | Job: Training animals (offered on the Training Post, people) |

## Layout

- `engine/`: MeitouClient as a git submodule, pinned to the engine commit the mod builds against. `git pull` does not move it: after
  every pull run `powershell -ExecutionPolicy Bypass -File tools\Update-Local.ps1 -Clean -Build`
  ([keep clones current](docs/local-development.md#keep-clones-current)).
- `Husbandry/`: the mod's project and `meitou.json`. Its records are added at load from the game's own (data phase); `Husbandry.mod` is
  empty.
- `Husbandry.Tests/`: tests of the framework surface (no game install needed).
- `Play/`: one-click build, deploy and run.
- `Directory.Build.props` imports the engine's, so mods build with the game's settings (warnings are errors).

## Branches

PRs into `beta`; CI (build and tests) must pass. `beta` is promoted to `main` once tested in play, and production goes through a PR to
`release`.
