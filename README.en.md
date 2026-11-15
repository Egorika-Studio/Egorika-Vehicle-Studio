# Egorika Vehicle Studio

[Русский](README.md) · **English** · [Español](README.es.md) · [Français](README.fr.md) · [Português (Brasil)](README.pt-BR.md)

A vehicle viewer and tuning editor for GTA V Legacy on Windows x64. Open YFT
files, inspect levels of detail, bones, materials and liveries, try on tuning
parts, and edit `carcols.meta`. Save your workspace and draft as an
`.egvehicle` project.

## Download and run

1. Download the [portable ZIP for Windows x64](https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/latest/download/EgorikaVehicleStudio-Windows-x64.zip)
   and extract it to a folder where you have write access.
2. Run `EgorikaVehicleStudio.exe`. The first launch needs an internet connection.
   The app downloads CodeWalker 30 dev46 from the author's distribution site,
   verifies the library's SHA-256 hash, and saves it next to the EXE. If a
   compatible library is already present, the app uses it. Windows needs
   [.NET Framework 4.8 or 4.8.1](https://learn.microsoft.com/dotnet/framework/install/versions-and-dependencies).
   To import from game archives, select your GTA V Legacy installation under
   **Settings → GTA V path**. You can open local YFT files without the game.

The ZIP includes the editor and SharpDX. The app downloads CodeWalker from
[the author's page on GTA5-Mods](https://www.gta5-mods.com/tools/codewalker-gtav-interactive-3d-map).
The app uses your local game installation for game files and vehicle previews.

**Settings → Language** offers Russian, English, Spanish, French, and Brazilian
Portuguese. Restart the app to apply the new language.

## Updates

Use **Help → Check for updates…** to download new versions from public GitHub
Releases. After you confirm, the app checks the downloaded EXE's version and
SHA-256 hash against GitHub's release metadata. Save your projects and close
the editor. The app will replace its EXE and restart.

The old version is kept as `previous.exe` in a `.egorika-update-*` folder
next to the app. To roll back, close the editor and restore the EXE from that copy.

## Build from source

```powershell
.\build.ps1 -CodeWalkerDirectory 'C:\path\to\CodeWalker'
```

To publish a release, bump `VERSION`, run `package-release.ps1` with the same
argument, and attach both files from `dist\v<version>` to a GitHub Release
tagged `v<version>`. The ZIP is for first-time installation. The standalone EXE
is for in-app updates.

## Licenses

Egorika Vehicle Studio's own code is available under the [MIT License](LICENSE).
[SharpDX](SharpDX-LICENSE.txt) ships with its MIT notice.
The app uses [CodeWalker by dexyfex](https://www.gta5-mods.com/tools/codewalker-gtav-interactive-3d-map)
to read game formats. Third-party code and assets are subject to their own terms.
