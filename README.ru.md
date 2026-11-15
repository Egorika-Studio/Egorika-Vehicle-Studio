# Egorika Vehicle Studio

**Русский** · [English](README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Português (Brasil)](README.pt-BR.md)

Редактор тюнинга и просмотрщик транспорта GTA V Legacy для Windows x64.
Открывает YFT, показывает уровни детализации, кости, материалы и ливреи,
позволяет примерять детали и редактировать `carcols.meta`.
Рабочая сцена и черновик сохраняются в проекте `.egvehicle`.

## Скачать и запустить

1. Скачайте [установщик для Windows x64](https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/latest/download/EgorikaVehicleStudio-OnlineSetup-Windows-x64.exe) и запустите его. Выберите папку установки и при желании добавьте ярлык на рабочий стол.
2. Запустите Egorika Vehicle Studio через меню «Пуск» или из папки установки. При первом запуске нужен интернет. Программа загрузит CodeWalker 30 dev46 с площадки автора, проверит SHA-256 и сохранит библиотеку в служебной папке.
3. Для Windows нужен [.NET Framework 4.8 или 4.8.1](https://learn.microsoft.com/dotnet/framework/install/versions-and-dependencies). Для импорта из игровых архивов укажите путь к GTA V Legacy в настройках. Локальные YFT можно открывать без игры.

Для удаления запустите деинсталлятор из меню «Пуск» или `unins000.exe` в папке установки. Проекты и настройки сохраняются.

Также доступен [portable ZIP](https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/latest/download/EgorikaVehicleStudio-Windows-x64.zip). Распакуйте его в папку с правом записи и запустите `EgorikaVehicleStudio.exe`.

В настройках языка доступны русский, английский, испанский, французский и бразильский португальский. Язык применяется после перезапуска.

## Обновления

Vehicle Studio проверяет GitHub Releases в фоне при запуске и показывает уведомление о новой версии. Скачивание начинается после подтверждения, а обновление применяется при следующем запуске. Ручная проверка доступна в меню **Справка → Проверить обновления…**.

Предыдущий EXE сохраняется как `previous.exe` в папке `.egorika-update-*`. В установленной версии она находится внутри `runtime`, в portable — рядом с EXE.

## Сборка

```powershell
.\build.ps1 -CodeWalkerDirectory 'C:\path\to\CodeWalker'
```

Для выпуска поднимите `VERSION`, запустите `package-release.ps1` с тем же параметром
и приложите оба файла из `dist\v<version>` к GitHub Release с тегом `v<version>`.
ZIP предназначен для первого запуска, а отдельный EXE — для встроенного обновления.

Для сборки установщика вместе с ZIP и EXE обновления передайте `-InnoSetupCompiler` скрипту `package-release.ps1`.

## Лицензии

Собственный код Egorika Vehicle Studio доступен под [MIT](LICENSE).
[SharpDX](SharpDX-LICENSE.txt) поставляется с сохранением его уведомления MIT.
Парсинг игровых форматов использует [CodeWalker от dexyfex](https://www.gta5-mods.com/tools/codewalker-gtav-interactive-3d-map).
Код и ресурсы сторонних проектов регулируются их собственными условиями.
