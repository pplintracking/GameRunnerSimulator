# GameRunner Simulator

Faz o Discord achar que um jogo está aberto no seu PC, para completar missões (Quests) que pedem
"jogue X por 15 minutos" sem instalar o jogo.

## Baixar

**[GameRunnerSimulator.exe](https://github.com/pplintracking/GameRunnerSimulator/releases/latest/download/GameRunnerSimulator.exe)**
— um arquivo só, para Windows 10/11 64 bits. Não precisa instalar nada, nem o .NET.

Na primeira vez o Windows pode mostrar "O Windows protegeu o computador" (o .exe não tem assinatura
paga). Clique em **Mais informações → Executar assim mesmo**.

## Como usar

1. Abra o `GameRunnerSimulator.exe` e busque o jogo da missão, pelo nome ou pelo `.exe`.
2. Clique em **Simular** (ou dê Enter). Abre uma janelinha com o nome e o ícone do jogo.
3. Deixe essa janelinha aberta (pode minimizar) até a missão completar no Discord.

O app já escolhe o executável certo; se o jogo tiver mais de um, dá para trocar antes de simular.
Dá para simular vários jogos ao mesmo tempo, e o painel "Simulações ativas" mostra o tempo de
cada um com o botão **Parar**.

Atalhos: `Ctrl+F` busca · `↑` `↓` navegam · `Enter` simula · `Esc` limpa a busca.

## Atualiza sozinho

- **Lista de jogos:** baixada do próprio Discord toda vez que o app abre
  (`discord.com/api/v9/applications/detectable`, hoje com mais de 10 mil jogos de Windows).
  Sem internet, usa a última lista salva.
- **O próprio app:** ao abrir e a cada 6 horas, procura versão nova aqui nos Releases, baixa,
  confere o tamanho e o sha256 e se troca. O rodapé avisa "instalada · Reiniciar agora".
  Se a pasta do .exe não aceitar escrita, ele só mostra o link para baixar.

## Como funciona

A lista do Discord traz, para cada jogo, o executável com a pasta onde ele fica (por exemplo
`_retail_/wow.exe`). Ao simular, o app cria um atalho físico (hard link) dele mesmo com esse nome e essa pasta em
`%LOCALAPPDATA%\GameRunnerSimulator\sims\` e abre esse processo. Quando a simulação para, a pasta
é apagada; se algo ficar para trás, o app limpa na próxima vez que abrir.

Em `%LOCALAPPDATA%\GameRunnerSimulator\` ficam também a lista de jogos (`games.json`) e os ícones.
Para desinstalar, apague o .exe e essa pasta.

> Use por sua conta. Simular jogo para ganhar recompensa pode ir contra os Termos do Discord.

## Para quem quer compilar

Precisa do [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet publish -c Release -o out
```

Sai `out\GameRunnerSimulator.exe` (versão 0.0.0, que não se atualiza sozinha). Para publicar
uma versão nova, crie uma tag anotada; o GitHub Actions compila, testa e cria o Release, e os
.exe já instalados se atualizam a partir dele:

```powershell
git tag -a v2.1.0 -m "o que mudou"
git push origin v2.1.0
```

## Créditos

Fork de [gustavocs97/GameRunnerSimulator](https://github.com/gustavocs97/GameRunnerSimulator).
Ícones dos jogos: CDN do Discord.
