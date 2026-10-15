# Rimworld-Psychology-unofficial-v1.6-1.7
This is an unofficial update of The Word-Mule's Psychology mod for Rimworld. It's a complex overhaul to social interactions and adds a psyche system where each pawn is unique.

## Layout
- `Common/` - Defs, Patches, Languages and Textures shared by every supported game version.
- `1.6/`, `1.7/` - compiled assemblies for each game version (plus optional compatibility assemblies under `Mods/<Mod>/Assemblies`).
- `LoadFolders.xml` - which folders RimWorld loads for each version and which compatibility folders depend on other mods.
- `Source/` - C# source: the main `Psychology` project and compatibility projects for Humanoid Alien Races, Character Editor and Pawnmorpher.

## Building
Game and Harmony references come from NuGet (Krafs.Rimworld.Ref, Lib.Harmony.Ref), so the game does not need to be installed to build.

```
pwsh Source/build.ps1                  # every supported version
dotnet build Source/Psychology.sln -c Release -p:RimWorldVersion=1.6
dotnet build Source/Psychology.sln -c Release -p:RimWorldVersion=1.7   # needs the .NET 10 SDK
```

Output goes straight into `<version>/Assemblies`. Compatibility projects are skipped with a warning when the other mod is not installed in the Steam Workshop folder (override with `-p:WorkshopDir=...`).
