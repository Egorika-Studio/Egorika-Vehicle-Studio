# Egorika Vehicle Studio

[Русский](README.ru.md) · [English](README.md) · **Español** · [Français](README.fr.md) · [Português (Brasil)](README.pt-BR.md)

Visor de vehículos y editor de modificaciones para GTA V Legacy en Windows x64.
Abre archivos YFT, inspecciona los niveles de detalle, huesos, materiales y
decoraciones, prueba piezas y edita `carcols.meta`. Guarda la escena de trabajo
y el borrador en un proyecto `.egvehicle`.

## Descarga e inicio

1. Descarga el [instalador para Windows x64](https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/latest/download/EgorikaVehicleStudio-OnlineSetup-Windows-x64.exe) y ejecútalo. Elige la carpeta de instalación y, si quieres, crea un acceso directo en el escritorio.
2. Abre Egorika Vehicle Studio desde el menú Inicio o desde la carpeta de instalación. La primera ejecución requiere internet para descargar CodeWalker 30 dev46 desde el sitio del autor y comprobar su SHA-256.
3. Windows requiere [.NET Framework 4.8 o 4.8.1](https://learn.microsoft.com/dotnet/framework/install/versions-and-dependencies). Para importar los archivos del juego, indica la ruta de GTA V Legacy en **Ajustes → Ruta de GTA V…**. Puedes abrir archivos YFT locales sin instalar el juego.

Para desinstalar, usa el menú Inicio o ejecuta `unins000.exe` desde la carpeta de instalación. Se conservan los proyectos y los ajustes.

También hay un [ZIP portable](https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/latest/download/EgorikaVehicleStudio-Windows-x64.zip). Extráelo en una carpeta con permisos de escritura y ejecuta `EgorikaVehicleStudio.exe`.

En **Ajustes → Idioma** puedes elegir ruso, inglés, español, francés o portugués de Brasil. Reinicia la aplicación para aplicar el cambio.

## Actualizaciones

Vehicle Studio comprueba los lanzamientos públicos de GitHub en segundo plano al iniciarse. Descarga la nueva versión, verifica su SHA-256 y la aplica en el siguiente inicio. También puedes buscar actualizaciones en el menú Ayuda.

El EXE anterior se guarda como `previous.exe` en una carpeta `.egorika-update-*`, dentro de `runtime` en instalaciones y junto al EXE en la edición portable.

## Compilación

```powershell
.\build.ps1 -CodeWalkerDirectory 'C:\path\to\CodeWalker'
```

Para publicar una versión, aumenta `VERSION`, ejecuta
`package-release.ps1` con el mismo parámetro y adjunta ambos archivos de
`dist\v<version>` a un lanzamiento de GitHub con la etiqueta `v<version>`.
El ZIP es para la primera instalación. El EXE por separado sirve para las
actualizaciones desde la aplicación.

Para generar el instalador junto con el ZIP y el EXE de actualización, usa `-InnoSetupCompiler` al ejecutar `package-release.ps1`.

## Licencias

El código propio de Egorika Vehicle Studio está disponible bajo la
[licencia MIT](LICENSE). [SharpDX](SharpDX-LICENSE.txt) se distribuye con su
aviso MIT. La aplicación utiliza
[CodeWalker de dexyfex](https://www.gta5-mods.com/tools/codewalker-gtav-interactive-3d-map)
para leer los formatos del juego. El código y los recursos de terceros se
rigen por sus propias condiciones.
