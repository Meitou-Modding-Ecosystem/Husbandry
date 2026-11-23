# Husbandry

Hitching posts and breeding for the player's animals in [MeitouClient](https://github.com/Meitou-Modding-Ecosystem/MeitouClient). Mod id
`husbandry`, no dependencies; what it does, its data file and settings: [docs/husbandry.md](docs/husbandry.md). GPL-3.0-or-later.

- Adding animals or Training Post skills from your own mod: [docs/depending-on-husbandry.md](docs/depending-on-husbandry.md) (agent skill
  `.claude/skills/husbandry-dependent`).

- Build and run locally with Visual Studio, VS Code or the terminal (this repo's engine or the installed launcher, deployed to the game): [docs/play.md](docs/play.md).
- Turning `.glb` models (Meshy, Hi3D, Blender) into buildable structures, no FCS needed: [docs/structures.md](docs/structures.md).
- Setting up the repos on your machine: [docs/local-development.md](docs/local-development.md).
- Instructing agents on a change that spans repos: [docs/agents-across-repos.md](docs/agents-across-repos.md).
- Agent rules for the engine pin, versions, CI and releases: [CLAUDE.md](CLAUDE.md) (same as `AGENTS.md`); step-by-step procedures:
  `.claude/skills/meitou-engine-release`.

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
