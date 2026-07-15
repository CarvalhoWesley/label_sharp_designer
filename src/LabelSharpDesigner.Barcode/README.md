# LabelSharpDesigner.Barcode

## O que é

Um adaptador em cima da biblioteca [ZXing.Net](https://github.com/micjahn/ZXing.Net) que gera a
imagem (raster) de um código de barras ou QR code — as barrinhas/módulos em si, como uma matriz de
pixels preto e branco.

## O que ele NÃO faz

Só gera o desenho das barras/módulos — **não** sabe desenhar o texto legível embaixo do código de
barras (aquele número que aparece por baixo das barras, tipo `7891234567890`). Cada projeto de
renderização (`Rendering.Canvas`, `Rendering.Pdf`) desenha esse texto como um passo separado, depois
de pedir o raster para cá. Veja a seção 4 do [ARCHITECTURE.md](../../ARCHITECTURE.md) para mais
detalhes de por que essa divisão existe.

## Peças principais

- **`BarcodeGenerator`** — recebe os dados a codificar (ex.: `"7891234567890"`) e a simbologia
  (`Ean13`, `Code128`, `QrCode` etc.) e devolve um `BarcodeImage`.
- **`BarcodeImage`** — o resultado: uma matriz de módulos (pixels ligados/desligados) que qualquer
  renderer sabe desenhar.

## Como usar (na prática, indiretamente)

Você normalmente não chama este projeto diretamente — ele é usado por baixo dos panos quando você
resolve um `BarcodeElement`/`QrCodeElement` através do `LabelSharpDesigner.Layout` e desenha o
resultado com `Rendering.Canvas`/`Rendering.Pdf`/`Rendering.ArgoxPpla`. Ainda assim, ele pode ser
usado isoladamente se você só precisar gerar um raster de código de barras, sem o resto do pipeline
de etiquetas.

## Por que barcodes e QR sempre viram raster (até no PDF vetorial)

O ZXing.Net só expõe um buffer de módulos já rasterizado — não existe uma versão vetorial "pura"
para nenhuma simbologia. Por isso até o `Rendering.Pdf`, que desenha texto e formas em vetor de
verdade, embute o código de barras como uma imagem PNG dentro do PDF.

## Dependências

Depende de `LabelSharpDesigner.Core` (para os tipos de simbologia/elemento). Multi-targeta
`netstandard2.0;net9.0`, sem dependência de Windows.
