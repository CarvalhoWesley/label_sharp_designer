# LabelSharpDesignerCore.Rendering.ArgoxPpla

## O que é

Gera os comandos no formato **PPLA**, a linguagem de impressoras térmicas de etiqueta da marca
Argox (o tipo de impressora usada em açougues, farmácias, logística etc. para etiquetas de preço,
código de barras). Este projeto transforma um `ResolvedDocument` em bytes que a impressora térmica
entende diretamente.

## Dois modos de gerar PPLA

Impressoras térmicas têm um firmware que já sabe desenhar formas simples (texto, linha, código de
barras) a partir de comandos — mas esse firmware é limitado e não cobre todo layout possível. Por
isso este projeto oferece dois caminhos:

- **PPLA nativo** (`PplaCommandBuilder`) — gera **um comando PPLA por elemento** da etiqueta (ex.:
  "desenhe um texto aqui", "desenhe um código de barras ali"). É quem a própria impressora desenha,
  então o resultado é rápido e nítido — mas só funciona para o que o firmware da impressora suporta.
- **PPLA raster** (`PplaRasterBuilder`) — rasteriza a etiqueta **inteira** como uma imagem
  monocromática (reaproveitando o `LabelSharpDesignerCore.Rendering.Canvas`, o mesmo desenho usado no
  preview/PNG) e manda essa imagem para a impressora. Mais lento de imprimir, mas funciona para
  qualquer layout, mesmo os que fogem das capacidades nativas do firmware.

## Peças principais

- **`PplaCommandBuilder`** — monta os comandos PPLA nativos, elemento por elemento.
- **`PplaRasterBuilder`** — rasteriza o documento inteiro via `Rendering.Canvas` e converte para
  bitmap monocromático.
- **`MonochromeBitmap`** / **`MonochromeBmpEncoder`** — representação e codificação de uma imagem
  preto-e-branco (1 bit por pixel), formato que a impressora espera no modo raster.
- **`PplaFields`** — constantes/helpers dos campos/comandos do protocolo PPLA.
- **`Latin1`** — PPLA trabalha com codificação Latin-1 (não UTF-8); esse helper cuida da conversão
  de texto.
- **`ArgoxRendererOptions`** — opções de impressão: `Darkness` (escurecimento, 2–20),
  `TransferType` (`DirectThermal` ou `ThermalTransfer` com ribbon), `FeedOffsetMm`/`OffsetXMm`/
  `OffsetYMm` (calibração manual de posição, específica de cada impressora física).
- **`ArgoxRasterOptions`** — opções extras só do modo raster: `FullResolution`,
  `MirrorHorizontal` (mantenha `true` — é o padrão esperado), `ReverseRowOrder`.

## Como usar

```csharp
using LabelSharpDesignerCore.Rendering.ArgoxPpla;

var opcoes = new ArgoxRendererOptions { Darkness = 10, TransferType = ArgoxTransferType.DirectThermal };

byte[] pplaNativo = PplaCommandBuilder.Build(resolved, opcoes);
byte[] pplaRaster = PplaRasterBuilder.Build(resolved, new ArgoxRasterOptions { Base = opcoes, MirrorHorizontal = true });
```

Os bytes gerados aqui são enviados à impressora pelo `LabelSharpDesignerCore.PrintTransport.Windows`
(`WindowsRawPrintTransport`) — este projeto só gera os bytes, não sabe nada sobre spooler de
impressão do Windows.

## Dependências

Depende de `LabelSharpDesignerCore.Core`, `LabelSharpDesignerCore.Rendering.Abstractions`,
`LabelSharpDesignerCore.Rendering.Canvas` (para o modo raster) e `LabelSharpDesignerCore.Barcode`.
Multi-targeta `netstandard2.0;net9.0` — gerar os comandos não depende de Windows; só o *envio* deles
(`PrintTransport.Windows`) depende.
