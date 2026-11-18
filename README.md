# Husbandry

Hitching posts and breeding for the player's animals in [MeitouClient](https://github.com/Meitou-Modding-Ecosystem/MeitouClient). Mod id
`husbandry`, no dependencies; what it does, its data file and settings: [docs/husbandry.md](docs/husbandry.md). GPL-3.0-or-later.

- Build and run locally with Visual Studio, VS Code or the terminal (this repo's engine or the installed launcher, deployed to the game): [docs/play.md](docs/play.md).
- Setting up the repos on your machine: [docs/local-development.md](docs/local-development.md).
- Instructing agents on a change that spans repos: [docs/agents-across-repos.md](docs/agents-across-repos.md).

## Layout

- `engine/`: MeitouClient as a git submodule, pinned to the engine commit the mod builds against. After a pull that moves it:
  `git submodule update --init`.
- `Husbandry/`: the mod's project and `meitou.json`. `Husbandry.Data/`: the generator that writes `Husbandry.mod` from the installed game
  (Play runs it).
- `Play/`: one-click build, deploy and run.
- `Directory.Build.props` imports the engine's, so mods build with the game's settings (warnings are errors).

## Branches

PRs into `beta`; CI (build and tests) must pass. `beta` is promoted to `main` once tested in play, and production goes through a PR to
`release`.
