# LabelSharpDesigner.Rendering.Png

## O que é

Exporta uma etiqueta já resolvida (`ResolvedDocument`) como uma imagem **PNG**. É o projeto mais
simples de todos os `Rendering.*` — praticamente só delega o desenho para o
`LabelSharpDesigner.Rendering.Canvas` e depois codifica o resultado como PNG.

## Como usar

```csharp
using LabelSharpDesigner.Rendering.Png;

byte[] png = PngExporter.Export(resolved, PngScale.X2);                // 1x / 2x / 3x
byte[] thumb = PngExporter.ExportScaled(resolved, targetWidthPx: 240); // largura customizada (ex.: miniatura)
```

## Peças principais

- **`PngExporter`** — desenha o `ResolvedDocument` (via `LabelCanvasRenderer` do
  `Rendering.Canvas`) num `SKBitmap` e codifica como PNG.
  - `Export(resolved, escala)` — exporta em 1×, 2× ou 3× o tamanho original (útil para telas de
    alta densidade/impressão em maior qualidade).
  - `ExportScaled(resolved, targetWidthPx)` — exporta com uma largura específica em pixels,
    mantendo a proporção — o jeito mais comum de gerar uma miniatura para uma lista/grid.
- **`PngScale`** — enum simples com as opções `X1`/`X2`/`X3`.

## Quando usar este projeto em vez de exportar PDF

PNG é ideal para miniaturas (ex.: mostrar uma prévia da etiqueta numa lista de produtos) ou quando
você só precisa de uma imagem estática, sem a necessidade de impressão vetorial de alta qualidade.
Para imprimir de verdade em papel/etiqueta, prefira `Rendering.Pdf` (impressoras comuns) ou
`Rendering.ArgoxPpla` (impressoras térmicas).

## Dependências

Depende de `LabelSharpDesigner.Core`, `LabelSharpDesigner.Rendering.Abstractions` e
`LabelSharpDesigner.Rendering.Canvas` (é quem faz o desenho de verdade). Multi-targeta
`netstandard2.0;net9.0`, sem dependência de Windows.
