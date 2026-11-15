# Egorika Vehicle Studio

**Русский** · [English](README.en.md) · [Español](README.es.md) · [Français](README.fr.md) · [Português (Brasil)](README.pt-BR.md)

Редактор тюнинга и просмотрщик транспорта GTA V Legacy для Windows x64.
Открывает YFT, показывает уровни детализации, кости, материалы и ливреи,
позволяет примерять детали и редактировать `carcols.meta`.
Рабочая сцена и черновик сохраняются в проекте `.egvehicle`.

## Скачать и запустить

1. Скачайте [portable ZIP для Windows x64](https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/latest/download/EgorikaVehicleStudio-Windows-x64.zip)
   и распакуйте его в папку, доступную для записи.
2. Запустите `EgorikaVehicleStudio.exe`. При первом запуске потребуется интернет:
   программа скачает CodeWalker 30 dev46 со страницы распространения автора,
   проверит SHA-256 библиотеки и разместит её рядом с EXE. Уже установленная
   совместимая библиотека будет использована как есть.
   Для Windows требуется
   [.NET Framework 4.8 или 4.8.1](https://learn.microsoft.com/dotnet/framework/install/versions-and-dependencies).
   Для импорта из игровых архивов укажите путь к своей GTA V Legacy через
   **Settings → GTA V path**. Для открытия локальных YFT игра не требуется.

В архив входят редактор и SharpDX. CodeWalker загружается с
[официальной страницы автора на GTA5-Mods](https://www.gta5-mods.com/tools/codewalker-gtav-interactive-3d-map).
Игровые файлы и превью транспорта берутся из локальной установки.

В **Settings → Language** доступны русский, английский, испанский, французский
и бразильский португальский. Язык применяется после перезапуска.

## Обновления

В меню **Help → Check for updates…** можно скачать новую версию из публичного
GitHub Releases. После подтверждения программа проверяет версию и SHA-256
загруженного EXE по метаданным GitHub. Сохраните проекты и закройте редактор
вручную. Программа заменит EXE и запустится снова.
Старая версия остаётся как `previous.exe` в папке `.egorika-update-*`
рядом с программой. Для отката закройте редактор и восстановите EXE из этой копии.

## Сборка

```powershell
.\build.ps1 -CodeWalkerDirectory 'C:\path\to\CodeWalker'
```

Для выпуска поднимите `VERSION`, запустите `package-release.ps1` с тем же параметром
и приложите оба файла из `dist\v<version>` к GitHub Release с тегом `v<version>`.
ZIP предназначен для первого запуска, а отдельный EXE — для встроенного обновления.

## Лицензии

Собственный код Egorika Vehicle Studio доступен под [MIT](LICENSE).
[SharpDX](SharpDX-LICENSE.txt) поставляется с сохранением его уведомления MIT.
Парсинг игровых форматов использует [CodeWalker от dexyfex](https://www.gta5-mods.com/tools/codewalker-gtav-interactive-3d-map).
Код и ресурсы сторонних проектов регулируются их собственными условиями.
