# Egorika Vehicle Studio

[Русский](README.ru.md) · [English](README.md) · [Español](README.es.md) · **Français** · [Português (Brasil)](README.pt-BR.md)

Visualiseur de véhicules et éditeur de modifications pour GTA V Legacy sur
Windows x64. Ouvrez des fichiers YFT, examinez les niveaux de détail, les os,
les matériaux et les livrées, essayez des pièces et modifiez `carcols.meta`.
Enregistrez la scène et le brouillon dans un projet `.egvehicle`.

## Télécharger et lancer

1. Téléchargez puis lancez [l’installateur pour Windows x64](https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/latest/download/EgorikaVehicleStudio-Setup-Windows-x64.exe). Choisissez le dossier d’installation et, si vous le souhaitez, créez un raccourci sur le bureau.
2. Ouvrez Egorika Vehicle Studio depuis le menu Démarrer ou le dossier d’installation. Au premier démarrage, Internet est nécessaire pour télécharger CodeWalker 30 dev46 depuis le site de son auteur et vérifier son SHA-256.
3. Windows nécessite [.NET Framework 4.8 ou 4.8.1](https://learn.microsoft.com/dotnet/framework/install/versions-and-dependencies). Pour importer les archives du jeu, indiquez le chemin de GTA V Legacy dans **Paramètres → Chemin de GTA V…**. Les fichiers YFT locaux s’ouvrent sans installer le jeu.

Pour désinstaller, utilisez le menu Démarrer ou lancez `unins000.exe` depuis le dossier d’installation. Vos projets et vos préférences sont conservés.

Un [ZIP portable](https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/latest/download/EgorikaVehicleStudio-Windows-x64.zip) est aussi disponible. Extrayez-le dans un dossier où vous avez le droit d’écrire et lancez `EgorikaVehicleStudio.exe`.

Dans **Paramètres → Langue**, choisissez le russe, l’anglais, l’espagnol, le français ou le portugais du Brésil. Redémarrez l’application pour appliquer le changement.

## Mises à jour

Dans **Aide → Rechercher des mises à jour…**, téléchargez une nouvelle version
depuis les releases publiques de GitHub. Après confirmation, l’application
vérifie la version et le SHA-256 de l’EXE téléchargé à partir des métadonnées
de GitHub. Enregistrez vos projets et fermez l’éditeur. L’application remplace
l’EXE et redémarre.

L’EXE précédent est conservé sous le nom `previous.exe` dans un dossier `.egorika-update-*`, sous `runtime` pour la version installée et à côté de l’EXE pour la version portable.

## Compiler depuis les sources

```powershell
.\build.ps1 -CodeWalkerDirectory 'C:\path\to\CodeWalker'
```

Pour publier une version, incrémentez `VERSION`, exécutez
`package-release.ps1` avec le même argument et ajoutez les deux fichiers de
`dist\v<version>` à une release GitHub portant le tag `v<version>`.
Le ZIP sert à la première installation. L’EXE seul sert aux mises à jour
depuis l’application.

Pour générer l’installateur avec le ZIP et l’EXE de mise à jour, passez `-InnoSetupCompiler` à `package-release.ps1`.

## Licences

Le code propre à Egorika Vehicle Studio est disponible sous
[licence MIT](LICENSE). [SharpDX](SharpDX-LICENSE.txt) est fourni avec sa
notice MIT. L’application utilise
[CodeWalker de dexyfex](https://www.gta5-mods.com/tools/codewalker-gtav-interactive-3d-map)
pour lire les formats de jeu. Les codes et ressources tiers sont soumis à
leurs propres conditions.
