# Egorika Vehicle Studio

[Русский](README.md) · [English](README.en.md) · [Español](README.es.md) · **Français** · [Português (Brasil)](README.pt-BR.md)

Visualiseur de véhicules et éditeur de modifications pour GTA V Legacy sur
Windows x64. Ouvrez des fichiers YFT, examinez les niveaux de détail, les os,
les matériaux et les livrées, essayez des pièces et modifiez `carcols.meta`.
Enregistrez la scène et le brouillon dans un projet `.egvehicle`.

## Télécharger et lancer

1. Téléchargez le [ZIP portable pour Windows x64](https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/latest/download/EgorikaVehicleStudio-Windows-x64.zip)
   et extrayez-le dans un dossier où vous avez le droit d’écrire.
2. Lancez `EgorikaVehicleStudio.exe`. Une connexion Internet est nécessaire
   au premier démarrage. L’application télécharge CodeWalker 30 dev46 depuis
   le site de distribution de son auteur, vérifie le SHA-256 de la bibliothèque
   et l’enregistre à côté de l’EXE. Si une bibliothèque compatible est déjà
   présente, elle est utilisée directement. Windows nécessite
   [.NET Framework 4.8 ou 4.8.1](https://learn.microsoft.com/dotnet/framework/install/versions-and-dependencies).
   Pour importer depuis les archives du jeu, indiquez le chemin de GTA V Legacy
   dans **Paramètres → Chemin de GTA V…**. Vous pouvez ouvrir des fichiers
   YFT locaux sans installer le jeu.

Le ZIP contient l’éditeur et SharpDX. L’application télécharge CodeWalker depuis
[la page de son auteur sur GTA5-Mods](https://www.gta5-mods.com/tools/codewalker-gtav-interactive-3d-map).
Les fichiers du jeu et les aperçus des véhicules proviennent de votre
installation locale.

Dans **Paramètres → Langue**, choisissez le russe, l’anglais, l’espagnol,
le français ou le portugais du Brésil. Redémarrez l’application pour appliquer
le changement.

## Mises à jour

Dans **Aide → Rechercher des mises à jour…**, téléchargez une nouvelle version
depuis les releases publiques de GitHub. Après confirmation, l’application
vérifie la version et le SHA-256 de l’EXE téléchargé à partir des métadonnées
de GitHub. Enregistrez vos projets et fermez l’éditeur. L’application remplace
l’EXE et redémarre.

L’ancienne version est conservée sous le nom `previous.exe` dans un dossier
`.egorika-update-*` à côté de l’application. Pour revenir à cette version,
fermez l’éditeur et restaurez l’EXE depuis cette copie.

## Compiler depuis les sources

```powershell
.\build.ps1 -CodeWalkerDirectory 'C:\path\to\CodeWalker'
```

Pour publier une version, incrémentez `VERSION`, exécutez
`package-release.ps1` avec le même argument et ajoutez les deux fichiers de
`dist\v<version>` à une release GitHub portant le tag `v<version>`.
Le ZIP sert à la première installation. L’EXE seul sert aux mises à jour
depuis l’application.

## Licences

Le code propre à Egorika Vehicle Studio est disponible sous
[licence MIT](LICENSE). [SharpDX](SharpDX-LICENSE.txt) est fourni avec sa
notice MIT. L’application utilise
[CodeWalker de dexyfex](https://www.gta5-mods.com/tools/codewalker-gtav-interactive-3d-map)
pour lire les formats de jeu. Les codes et ressources tiers sont soumis à
leurs propres conditions.
