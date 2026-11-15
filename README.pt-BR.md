# Egorika Vehicle Studio

[Русский](README.ru.md) · [English](README.md) · [Español](README.es.md) · [Français](README.fr.md) · **Português (Brasil)**

Visualizador de veículos e editor de modificações para GTA V Legacy no
Windows x64. Abra arquivos YFT, inspecione níveis de detalhe, ossos, materiais
e pinturas decorativas, experimente peças e edite `carcols.meta`.
Salve a cena de trabalho e o rascunho em um projeto `.egvehicle`.

## Baixar e executar

1. Baixe o [instalador para Windows x64](https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/latest/download/EgorikaVehicleStudio-OnlineSetup-Windows-x64.exe) e execute-o. Escolha a pasta de instalação e, se quiser, crie um atalho na área de trabalho.
2. Abra o Egorika Vehicle Studio pelo menu Iniciar ou pela pasta de instalação. A primeira execução precisa de internet para baixar o CodeWalker 30 dev46 do site do autor e verificar seu SHA-256.
3. O Windows requer [.NET Framework 4.8 ou 4.8.1](https://learn.microsoft.com/dotnet/framework/install/versions-and-dependencies). Para importar os arquivos do jogo, informe o caminho do GTA V Legacy em **Configurações → Caminho do GTA V…**. É possível abrir arquivos YFT locais sem instalar o jogo.

Para desinstalar, use o menu Iniciar ou execute `unins000.exe` na pasta de instalação. Seus projetos e preferências são preservados.

Também há um [ZIP portátil](https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/latest/download/EgorikaVehicleStudio-Windows-x64.zip). Extraia-o em uma pasta com permissão de gravação e execute `EgorikaVehicleStudio.exe`.

Em **Configurações → Idioma**, escolha russo, inglês, espanhol, francês ou português do Brasil. Reinicie o aplicativo para aplicar o idioma.

## Atualizações

Ao iniciar, o Vehicle Studio verifica as versões públicas no GitHub em segundo plano. Ele baixa a nova versão, verifica o SHA-256 e aplica a atualização na próxima abertura. A verificação manual está disponível no menu Ajuda.

O EXE anterior é salvo como `previous.exe` em uma pasta `.egorika-update-*`, dentro de `runtime` na versão instalada e ao lado do EXE na versão portátil.

## Compilar a partir do código-fonte

```powershell
.\build.ps1 -CodeWalkerDirectory 'C:\path\to\CodeWalker'
```

Para publicar uma versão, incremente `VERSION`, execute
`package-release.ps1` com o mesmo parâmetro e anexe os dois arquivos de
`dist\v<version>` a um release do GitHub com a tag `v<version>`. O ZIP é
para a primeira instalação. O EXE separado é usado pelo atualizador do aplicativo.

Para gerar o instalador junto com o ZIP e o EXE de atualização, informe `-InnoSetupCompiler` ao executar `package-release.ps1`.

## Licenças

O código próprio do Egorika Vehicle Studio está disponível sob a
[licença MIT](LICENSE). O [SharpDX](SharpDX-LICENSE.txt) é distribuído com
seu aviso MIT. O aplicativo usa o
[CodeWalker do dexyfex](https://www.gta5-mods.com/tools/codewalker-gtav-interactive-3d-map)
para ler os formatos do jogo. Código e recursos de terceiros seguem seus
próprios termos.
