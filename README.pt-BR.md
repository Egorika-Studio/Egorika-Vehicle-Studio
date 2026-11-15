# Egorika Vehicle Studio

[Русский](README.md) · [English](README.en.md) · [Español](README.es.md) · [Français](README.fr.md) · **Português (Brasil)**

Visualizador de veículos e editor de modificações para GTA V Legacy no
Windows x64. Abra arquivos YFT, inspecione níveis de detalhe, ossos, materiais
e pinturas decorativas, experimente peças e edite `carcols.meta`.
Salve a cena de trabalho e o rascunho em um projeto `.egvehicle`.

## Baixar e executar

1. Baixe o [ZIP portátil para Windows x64](https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/latest/download/EgorikaVehicleStudio-Windows-x64.zip)
   e extraia em uma pasta com permissão de gravação.
2. Execute `EgorikaVehicleStudio.exe`. A primeira execução requer internet.
   O aplicativo baixa o CodeWalker 30 dev46 do site de distribuição do autor,
   verifica o SHA-256 da biblioteca e a salva ao lado do EXE. Se já houver
   uma biblioteca compatível, ela será usada diretamente. O Windows requer
   [.NET Framework 4.8 ou 4.8.1](https://learn.microsoft.com/dotnet/framework/install/versions-and-dependencies).
   Para importar dos arquivos do jogo, indique onde o GTA V Legacy está
   instalado em **Configurações → Caminho do GTA V…**. É possível abrir
   arquivos YFT locais sem instalar o jogo.

O ZIP inclui o editor e o SharpDX. O aplicativo baixa o CodeWalker pela
[página do autor no GTA5-Mods](https://www.gta5-mods.com/tools/codewalker-gtav-interactive-3d-map).
Os arquivos do jogo e as prévias dos veículos são obtidos da instalação local.

Em **Configurações → Idioma**, escolha russo, inglês, espanhol, francês ou
português do Brasil. Reinicie o aplicativo para aplicar o idioma.

## Atualizações

Use **Ajuda → Verificar atualizações…** para baixar novas versões dos releases
públicos do GitHub. Após a confirmação, o aplicativo verifica a versão e o
SHA-256 do EXE baixado com base nos metadados do GitHub. Salve os projetos e
feche o editor. O aplicativo substituirá o EXE e abrirá novamente.

A versão anterior fica salva como `previous.exe` na pasta
`.egorika-update-*` ao lado do aplicativo. Para voltar à versão anterior,
feche o editor e restaure o EXE a partir dessa cópia.

## Compilar a partir do código-fonte

```powershell
.\build.ps1 -CodeWalkerDirectory 'C:\path\to\CodeWalker'
```

Para publicar uma versão, incremente `VERSION`, execute
`package-release.ps1` com o mesmo parâmetro e anexe os dois arquivos de
`dist\v<version>` a um release do GitHub com a tag `v<version>`. O ZIP é
para a primeira instalação. O EXE separado é usado pelo atualizador do aplicativo.

## Licenças

O código próprio do Egorika Vehicle Studio está disponível sob a
[licença MIT](LICENSE). O [SharpDX](SharpDX-LICENSE.txt) é distribuído com
seu aviso MIT. O aplicativo usa o
[CodeWalker do dexyfex](https://www.gta5-mods.com/tools/codewalker-gtav-interactive-3d-map)
para ler os formatos do jogo. Código e recursos de terceiros seguem seus
próprios termos.
