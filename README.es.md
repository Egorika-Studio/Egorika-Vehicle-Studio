# Egorika Vehicle Studio

[Русский](README.md) · [English](README.en.md) · **Español** · [Français](README.fr.md) · [Português (Brasil)](README.pt-BR.md)

Visor de vehículos y editor de modificaciones para GTA V Legacy en Windows x64.
Abre archivos YFT, inspecciona los niveles de detalle, huesos, materiales y
decoraciones, prueba piezas y edita `carcols.meta`. Guarda la escena de trabajo
y el borrador en un proyecto `.egvehicle`.

## Descarga e inicio

1. Descarga el [ZIP portable para Windows x64](https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/latest/download/EgorikaVehicleStudio-Windows-x64.zip)
   y extráelo en una carpeta con permisos de escritura.
2. Ejecuta `EgorikaVehicleStudio.exe`. El primer inicio requiere conexión a
   internet. La aplicación descarga CodeWalker 30 dev46 del sitio de
   distribución de su autor, verifica el SHA-256 de la biblioteca y la guarda
   junto al EXE. Si ya hay una biblioteca compatible, la utiliza. Windows
   requiere [.NET Framework 4.8 o 4.8.1](https://learn.microsoft.com/dotnet/framework/install/versions-and-dependencies).
   Para importar desde los archivos del juego, indica dónde está instalado
   GTA V Legacy en **Ajustes → Ruta de GTA V…**. Puedes abrir archivos YFT
   locales sin tener instalado el juego.

El ZIP incluye el editor y SharpDX. La aplicación descarga CodeWalker desde
[la página del autor en GTA5-Mods](https://www.gta5-mods.com/tools/codewalker-gtav-interactive-3d-map).
La instalación local del juego proporciona los archivos y las vistas previas
de los vehículos.

En **Ajustes → Idioma** puedes elegir ruso, inglés, español, francés o portugués
de Brasil. Reinicia la aplicación para aplicar el idioma.

## Actualizaciones

Usa **Ayuda → Buscar actualizaciones…** para descargar nuevas versiones desde
los lanzamientos públicos de GitHub. Una vez confirmada la descarga, la
aplicación compara la versión y el SHA-256 del EXE con los datos de GitHub.
Guarda los proyectos y cierra el editor. La aplicación sustituirá el EXE y se
iniciará de nuevo.

La versión anterior queda guardada como `previous.exe` en una carpeta
`.egorika-update-*` junto a la aplicación. Para volver a esa versión, cierra
el editor y restaura el EXE desde la copia.

## Compilación

```powershell
.\build.ps1 -CodeWalkerDirectory 'C:\path\to\CodeWalker'
```

Para publicar una versión, aumenta `VERSION`, ejecuta
`package-release.ps1` con el mismo parámetro y adjunta ambos archivos de
`dist\v<version>` a un lanzamiento de GitHub con la etiqueta `v<version>`.
El ZIP es para la primera instalación. El EXE por separado sirve para las
actualizaciones desde la aplicación.

## Licencias

El código propio de Egorika Vehicle Studio está disponible bajo la
[licencia MIT](LICENSE). [SharpDX](SharpDX-LICENSE.txt) se distribuye con su
aviso MIT. La aplicación utiliza
[CodeWalker de dexyfex](https://www.gta5-mods.com/tools/codewalker-gtav-interactive-3d-map)
para leer los formatos del juego. El código y los recursos de terceros se
rigen por sus propias condiciones.
