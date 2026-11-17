# Egorika Vehicle Studio

[Русский](README.ru.md) · **English** · [Español](README.es.md) · [Français](README.fr.md) · [Português (Brasil)](README.pt-BR.md)

A vehicle viewer and tuning editor for GTA V Legacy on Windows x64. Open YFT
files, inspect levels of detail, bones, materials and liveries, try on tuning
parts, and edit `carcols.meta`. Save your workspace and draft as an
`.egvehicle` project.

## Download and run

1. Download the [Windows x64 installer](https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/latest/download/EgorikaVehicleStudio-OnlineSetup-Windows-x64.exe) and run it. Choose an installation folder and, if you want, a desktop shortcut.
2. Start Egorika Vehicle Studio from the Start menu or from its installation folder. The first launch needs an internet connection. The app downloads CodeWalker 30 dev46 from the author's distribution site, verifies its SHA-256 hash, and saves it in the application's runtime folder.
3. Windows needs [.NET Framework 4.8 or 4.8.1](https://learn.microsoft.com/dotnet/framework/install/versions-and-dependencies). For game archives, select GTA V Legacy under **Settings → GTA V path**. Local YFT files can be opened without the game.

To uninstall, use **Uninstall Egorika Vehicle Studio** in the Start menu or run `unins000.exe` in the installation folder. Your projects and preferences stay in place.

The installation folder contains the stock GTA V vehicle previews in `thumbnails`, generated mod previews in `thumbnails\mods`, and local preferences in `settings`. A mod preview is generated the first time the model is opened.

**Settings → Language** offers Russian, English, Spanish, French, and Brazilian Portuguese. Restart the app to apply the new language.

## Updates

Vehicle Studio checks public GitHub Releases in the background on startup and
shows a blue download arrow next to the version when an update is available. If you confirm the
download, it verifies the EXE against the release metadata and applies it at
the next launch. You can also check manually in **Help → Check for updates…**.

The previous EXE is kept as `previous.exe` in a `.egorika-update-*` directory, inside `runtime` for installed copies and next to the EXE for portable copies.

## Session recovery

The currently open vehicle, tuning draft and workspace are saved under
`settings\session.egvehicle` every 30 seconds while the editor is idle and again
when it closes. At the next launch, Vehicle Studio restores the last completed
snapshot. The session snapshot is separate from manually saved projects.

## Build from source

```powershell
.\build.ps1 -CodeWalkerDirectory 'C:\path\to\CodeWalker'
```

To package a release, bump `VERSION` and run `package-release.ps1` with the same
argument. The script reads pre-rendered stock thumbnails from `bin\thumbnails`
by default. Use `-ThumbnailDirectory` to select another folder. To render the
library from a local GTA V Legacy installation, run the built viewer with
`--bake-thumbnails "C:\path\to\GTA V" "C:\path\to\thumbnails" "*"`.
Publish the ZIP and standalone EXE on a public prerelease tagged
`payload-v<version>`. Upload only the online installer to the stable release
tagged `v<version>`. Publish the prerelease first.

Use `package-release.ps1` with `-InnoSetupCompiler` (Inno Setup 6.5 or later) to build the online installer along with the ZIP and update EXE.
The online installer, `EgorikaVehicleStudio-OnlineSetup-Windows-x64.exe`, downloads the public portable ZIP for its release tag without signing in. It verifies the ZIP against the SHA-256 embedded at build time and installs the application into the folder selected in Setup.
The online setup window uses WPF and requires .NET Framework 4.8 or later, as does Vehicle Studio. Its English and Russian interface uses the Egorika Setup wordmark and Source Sans 3.
Keep the exact ZIP used to build the installer available on its prerelease.

## Licenses

Egorika Vehicle Studio's source code is publicly available under the
[MIT License with Commons Clause 1.0](LICENSE). You may use, modify, and
share builds for free. Selling the software or a build whose value comes
substantially from it requires a separate license. Earlier versions
released under MIT remain available under their original terms.

[SharpDX](SharpDX-LICENSE.txt) ships with its MIT notice.
The app uses [CodeWalker by dexyfex](https://www.gta5-mods.com/tools/codewalker-gtav-interactive-3d-map)
to read game formats. Third-party code and assets are subject to their own terms.
