# LabelSharpDesigner.Rendering.Pdf

## O que é

Exporta uma etiqueta já resolvida (`ResolvedDocument`) como um **PDF vetorial de verdade**, usando a
biblioteca [PdfSharp](http://www.pdfsharp.net/). "Vetorial" aqui quer dizer que texto e formas são
desenhados como instruções geométricas (não como uma foto/pixel) — o PDF fica nítido em qualquer
zoom ou tamanho de impressão.

## Por que este projeto não reaproveita o `Rendering.Canvas`

Diferente de `Rendering.Png`/`Rendering.ArgoxPpla` (que reaproveitam o `LabelCanvasRenderer` do
`Rendering.Canvas`), este projeto desenha por conta própria via `XGraphics`/`XFont` do PdfSharp —
inclusive tem sua própria lógica de quebra de linha de texto, separada da do `Rendering.Canvas`.
Isso acontece porque a API do PdfSharp (voltada a desenho vetorial em PDF) é bem diferente da API do
SkiaSharp (voltada a desenho em bitmap) — não dá para simplesmente compartilhar o mesmo código de
desenho entre as duas. Isso é uma pegadinha conhecida do projeto: ao corrigir um bug de texto, é
preciso lembrar de arrumar nos dois lugares se o bug também existir no Canvas.

## Como usar

```csharp
using LabelSharpDesigner.Rendering.Pdf;

byte[] pdf = PdfExporter.Export(resolved);                 // uma etiqueta, um PDF
byte[] pdfLote = PdfExporter.ExportBatch(fileiras);         // uma página por fileira (LayoutEngine.ResolveBatch)
```

## Peças principais

- **`PdfExporter`** — ponto de entrada; monta o documento PDF e aplica, por página, um único
  `gfx.ScaleTransform(72.0 / dpi)` para converter "dots do documento" em pontos PDF. **Atenção**: por
  causa dessa escala global, toda coordenada e todo tamanho de fonte passado para a API do PdfSharp
  precisa estar em dots (não em pontos/mm reais) — senão a escala acaba sendo aplicada duas vezes.
- **`TextDrawing`** — desenho de texto vetorial com quebra de linha própria.
- **`ShapeDrawing`** — retângulos, elipses, círculos, linhas.
- **`TableDrawing`** — tabelas.
- **`ImageDrawing`** — imagens.
- **`BarcodeDrawing`** — embute o raster do código de barras (gerado por `LabelSharpDesigner.Barcode`)
  como imagem dentro do PDF vetorial, e desenha o texto legível (`DrawBarcodeText`) como um passo
  separado, já que nenhuma simbologia de código de barras tem representação vetorial pura.

## Dependências

Depende de `LabelSharpDesigner.Core`, `LabelSharpDesigner.Rendering.Abstractions` (implementa
`IResolvedPayloadVisitor`) e `LabelSharpDesigner.Barcode`. Multi-targeta `netstandard2.0;net9.0`,
sem dependência de Windows.

## Quem usa este projeto

`LabelSharpDesigner.PrintTransport.Windows` (`WindowsPdfPrintTransport`) manda o PDF gerado aqui
para o driver de impressão do Windows; `LabelSharpDesigner.App` (`ExportDialogForm`) também usa este
projeto para salvar PDF em disco.
