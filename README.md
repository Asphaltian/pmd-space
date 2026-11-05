# Pokémon Mystery Dungeon: Explorers of Space

> [!WARNING]
> This is VERY early days and even more behind [https://github.com/Asphaltian/pmd-green](Green Rescue Team). We're also using this project to test [NDS Modern Runtime](https://github.com/Asphaltian/NDSModernRuntime) and [NDSRecomp](https://github.com/Asphaltian/NDSRecomp), so expect bugs, and expect more shit to change without warning!

Explorers of Space is Pokémon Mystery Dungeon: Explorers of Sky, recompiled to run natively on PC.

You need your own copy of the game, specifically the **USA** version.

## How to Play

The first time you launch it, pick your ROM when it asks. You can also drop it on `pmd_space`, or pass it on the command line:

```
pmd_space pmdsky.us.nds
```

The game keeps its own copy of the ROM after that, so you only need to do this once.

## Your Data

Saves, settings and screenshots are stored here:

* Windows: `%LOCALAPPDATA%\Explorers of Space`
* Linux: `~/.config/Explorers of Space`
* macOS: `~/Library/Application Support/Explorers of Space`

If you'd rather keep everything next to the game, put an empty file called `portable.txt` in the folder you run it from.

## Building

You will need the .NET 10 SDK and [Slang](https://github.com/shader-slang/slang/releases), with `slangc` available on your PATH.

```
git clone --recurse-submodules https://github.com/Asphaltian/pmd-space
cd pmd-space
```

Put your ROM in the repository root as `pmdsky.us.nds`. The symbols for both CPUs come out of a build of [pret/pmd-sky](https://github.com/pret/pmd-sky), so build it by following its `INSTALL.md`, then copy `build/pmdsky.us/main.nef.xMAP` and `sub/build/arm7.nef.xMAP` into the repository root.

Now recompile both CPUs' code, then build and run the game:

```
dotnet run --project lib/NDSModernRuntime/NDSRecomp/src/NDSRecomp -c Release -- us.arm9.toml
dotnet run --project lib/NDSModernRuntime/NDSRecomp/src/NDSRecomp -c Release -- us.arm7.toml
dotnet run --project src/PMDSpace -c Release -- pmdsky.us.nds
```

## License

Licensed under GPL-3.0.
