# LabelSharpDesigner.Rendering.Canvas

## O que é

O **desenhador principal de verdade** do LabelSharpDesigner. Usa a biblioteca
[SkiaSharp](https://github.com/mono/SkiaSharp) para desenhar um `ResolvedDocument` (já calculado
pelo `LayoutEngine`) num `SKCanvas`. É o único lugar do repositório que sabe "desenhar de verdade"
no sentido gráfico — os outros caminhos de saída (pré-visualização ao vivo, PNG, impressão PPLA em
modo raster) reaproveitam exatamente este código, em vez de reimplementar desenho por conta própria.

## Por que isso importa

Reaproveitar a mesma rotina de desenho em vários lugares é o que garante que a pré-visualização na
tela, o PNG exportado e a impressão térmica em modo raster **nunca fiquem diferentes entre si** —
qualquer melhoria ou correção de bug no desenho beneficia automaticamente os três caminhos ao mesmo
tempo. (O PDF é a única exceção deliberada: `Rendering.Pdf` desenha texto/vetor por conta própria,
via PdfSharp — veja o README daquele projeto.)

## Peças principais

- **`LabelCanvasRenderer`** — o ponto de entrada: recebe um `ResolvedDocument` e um `SKCanvas` e
  desenha a etiqueta inteira, elemento por elemento, respeitando z-order e rotação.
- **`TextDrawing`** — desenho de texto, incluindo quebra de linha automática quando o texto não
  cabe na largura do elemento.
- **`ShapeDrawing`** — retângulos, elipses, círculos e linhas.
- **`TableDrawing`** — tabelas (cabeçalho + linhas).
- **`ImageDrawing`** — imagens, respeitando o modo de encaixe (`Contain`/`Cover`/`Fill`/etc.).
- **`ColorExtensions`** — conversão entre `ArgbColor` (tipo do `Core`) e as cores do SkiaSharp.

## Quem usa este projeto

- **`LabelSharpDesigner.Rendering.Png`** — usa este renderer para desenhar num `SKBitmap` e depois
  salvar como PNG.
- **`LabelSharpDesigner.Rendering.ArgoxPpla`** — usa este renderer para o modo "PPLA raster"
  (rasteriza a etiqueta inteira como imagem monocromática, para impressoras térmicas).
- **`LabelSharpDesigner.UI.WinForms`** — a aba "Pré-visualizar" do editor usa este renderer para
  mostrar exatamente o que vai sair impresso. (A área de edição em si, `LabelCanvasControl`, desenha
  placeholders simplificados direto do modelo, sem passar por aqui, para manter o arraste/
  redimensionamento leve — veja o README de `UI.WinForms`.)

## Dependências

Depende de `LabelSharpDesigner.Core`, `LabelSharpDesigner.Rendering.Abstractions` (implementa
`IResolvedPayloadVisitor`) e `LabelSharpDesigner.Barcode` (para desenhar o raster de códigos de
barras/QR). Multi-targeta `netstandard2.0;net9.0`, sem dependência de Windows — o SkiaSharp
funciona em qualquer plataforma.
