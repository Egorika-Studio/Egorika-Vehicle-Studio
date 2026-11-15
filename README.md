# Egorika Vehicle Studio

[Русский](README.ru.md) · **English** · [Español](README.es.md) · [Français](README.fr.md) · [Português (Brasil)](README.pt-BR.md)

A vehicle viewer and tuning editor for GTA V Legacy on Windows x64. Open YFT
files, inspect levels of detail, bones, materials and liveries, try on tuning
parts, and edit `carcols.meta`. Save your workspace and draft as an
`.egvehicle` project.

## Download and run

1. Download the [Windows x64 installer](https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/latest/download/EgorikaVehicleStudio-Setup-Windows-x64.exe) and run it. Choose an installation folder and, if you want, a desktop shortcut.
2. Start Egorika Vehicle Studio from the Start menu or from its installation folder. The first launch needs an internet connection. The app downloads CodeWalker 30 dev46 from the author's distribution site, verifies its SHA-256 hash, and saves it in the application's runtime folder.
3. Windows needs [.NET Framework 4.8 or 4.8.1](https://learn.microsoft.com/dotnet/framework/install/versions-and-dependencies). For game archives, select GTA V Legacy under **Settings → GTA V path**. Local YFT files can be opened without the game.

To uninstall, use **Uninstall Egorika Vehicle Studio** in the Start menu or run `unins000.exe` in the installation folder. Your projects and preferences stay in place.

A [portable ZIP](https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/latest/download/EgorikaVehicleStudio-Windows-x64.zip) is also available. Extract it to a writable folder and run `EgorikaVehicleStudio.exe`.

**Settings → Language** offers Russian, English, Spanish, French, and Brazilian Portuguese. Restart the app to apply the new language.

## Updates

Use **Help → Check for updates…** to download new versions from public GitHub
Releases. After you confirm, the app checks the downloaded EXE's version and
SHA-256 hash against GitHub's release metadata. Save your projects and close
the editor. The app will replace its EXE and restart.

The previous EXE is kept as `previous.exe` in a `.egorika-update-*` directory, inside `runtime` for installed copies and next to the EXE for portable copies.

## Build from source

```powershell
.\build.ps1 -CodeWalkerDirectory 'C:\path\to\CodeWalker'
```

To publish a release, bump `VERSION`, run `package-release.ps1` with the same
argument, and attach both files from `dist\v<version>` to a GitHub Release
tagged `v<version>`. The ZIP is for first-time installation. The standalone EXE
is for in-app updates.

Use `package-release.ps1` with `-InnoSetupCompiler` to build the installer along with the ZIP and update EXE.

## Licenses

Egorika Vehicle Studio's own code is available under the [MIT License](LICENSE).
[SharpDX](SharpDX-LICENSE.txt) ships with its MIT notice.
The app uses [CodeWalker by dexyfex](https://www.gta5-mods.com/tools/codewalker-gtav-interactive-3d-map)
to read game formats. Third-party code and assets are subject to their own terms.
