# LabelSharpDesigner

LabelSharpDesigner é um **editor de etiquetas** (como as de código de barras, preço, endereço etc.)
escrito em .NET 9 / C# / WinForms. Com ele dá para desenhar uma etiqueta visualmente (texto, código
de barras, QR code, imagens, tabelas...), salvar esse desenho como um arquivo `.label`, e depois usar
esse arquivo para gerar PNG, PDF ou mandar direto para uma impressora térmica.

Se você nunca trabalhou com esse tipo de sistema, pense assim: é parecido com um "Word para
etiquetas" — só que, em vez de só imprimir texto fixo, os campos podem ser preenchidos
dinamicamente com dados reais (nome do produto, preço, código de barras) na hora de imprimir.

## Por onde começar

- Nunca mexeu no projeto? Comece por este README para entender a divisão de projetos, depois abra a
  pasta do projeto específico que for mexer — cada uma tem seu próprio `README.md` explicando o que
  ele faz.
- Quer entender o **pipeline interno** (como um documento vira etiqueta impressa)? Veja
  [ARCHITECTURE.md](ARCHITECTURE.md).
- Quer **usar** o LabelSharpDesigner a partir de outra aplicação .NET? Veja
  [INTEGRATION.md](INTEGRATION.md) (passo a passo) e [USAGE.md](USAGE.md) (referência de API).
- Quer ver um exemplo funcionando de ponta a ponta? Abra
  [`src/LabelSharpDesigner.SampleApp`](src/LabelSharpDesigner.SampleApp).

## Como o desenho vira etiqueta impressa (resumo)

```
LabelDocument  →  LayoutEngine  →  ResolvedDocument  →  Renderer/Exporter  →  Tela / Arquivo / Impressora
(o que a pessoa    (calcula posições    (documento já       (desenha de fato:
 desenhou)          em pixels, resolve   pronto pra          Canvas/PNG/PDF/PPLA)
                     {{variáveis}})       desenhar)
```

A regra mais importante do repositório: **só o `LayoutEngine` (projeto `Layout`) calcula layout**.
Nenhum exportador ou driver de impressão recalcula nada — todos recebem um documento já resolvido.
Isso é o que garante que a pré-visualização, o PNG, o PDF e a impressão térmica nunca fiquem
diferentes entre si.

## Mapa dos projetos (`src/`)

| Projeto | Em uma frase | README |
|---|---|---|
| `LabelSharpDesigner.Core` | O modelo de dados: o que é uma etiqueta, o que é um elemento de texto/código de barras/etc. | [src/LabelSharpDesigner.Core](src/LabelSharpDesigner.Core/README.md) |
| `LabelSharpDesigner.Expressions` | Motor que entende e calcula `{{ expressões }}` dentro dos textos. | [src/LabelSharpDesigner.Expressions](src/LabelSharpDesigner.Expressions/README.md) |
| `LabelSharpDesigner.Layout` | Transforma o desenho (mm, expressões) em coordenadas prontas em pixels. | [src/LabelSharpDesigner.Layout](src/LabelSharpDesigner.Layout/README.md) |
| `LabelSharpDesigner.Serialization` | Salva e carrega arquivos `.label` (JSON versionado). | [src/LabelSharpDesigner.Serialization](src/LabelSharpDesigner.Serialization/README.md) |
| `LabelSharpDesigner.Barcode` | Gera o desenho (raster) de códigos de barras. | [src/LabelSharpDesigner.Barcode](src/LabelSharpDesigner.Barcode/README.md) |
| `LabelSharpDesigner.History` | Undo/redo do editor. | [src/LabelSharpDesigner.History](src/LabelSharpDesigner.History/README.md) |
| `LabelSharpDesigner.Rendering.Abstractions` | Contrato comum usado pelos "desenhadores" (renderers). | [src/LabelSharpDesigner.Rendering.Abstractions](src/LabelSharpDesigner.Rendering.Abstractions/README.md) |
| `LabelSharpDesigner.Rendering.Canvas` | O "desenhador" principal (SkiaSharp) — usado no preview, no PNG e na impressão raster. | [src/LabelSharpDesigner.Rendering.Canvas](src/LabelSharpDesigner.Rendering.Canvas/README.md) |
| `LabelSharpDesigner.Rendering.Png` | Exporta a etiqueta como imagem PNG. | [src/LabelSharpDesigner.Rendering.Png](src/LabelSharpDesigner.Rendering.Png/README.md) |
| `LabelSharpDesigner.Rendering.Pdf` | Exporta a etiqueta como PDF vetorial. | [src/LabelSharpDesigner.Rendering.Pdf](src/LabelSharpDesigner.Rendering.Pdf/README.md) |
| `LabelSharpDesigner.Rendering.ArgoxPpla` | Gera os comandos para impressoras térmicas Argox (PPLA). | [src/LabelSharpDesigner.Rendering.ArgoxPpla](src/LabelSharpDesigner.Rendering.ArgoxPpla/README.md) |
| `LabelSharpDesigner.PrintTransport.Windows` | Manda os bytes (PDF ou PPLA) para a impressora de verdade. | [src/LabelSharpDesigner.PrintTransport.Windows](src/LabelSharpDesigner.PrintTransport.Windows/README.md) |
| `LabelSharpDesigner.UI.WinForms` | Os controles visuais reutilizáveis do editor (a área de desenho, painéis). | [src/LabelSharpDesigner.UI.WinForms](src/LabelSharpDesigner.UI.WinForms/README.md) |
| `LabelSharpDesigner.App` | O aplicativo final: telas prontas de biblioteca, editor, exportação e impressão. | [src/LabelSharpDesigner.App](src/LabelSharpDesigner.App/README.md) |
| `LabelSharpDesigner.Legacy.Bridge` | Ponte para sistemas antigos (.NET Framework 4.x) abrirem o editor como programa separado. | [src/LabelSharpDesigner.Legacy.Bridge](src/LabelSharpDesigner.Legacy.Bridge/README.md) |
| `LabelSharpDesigner.SampleApp` | Aplicativo de exemplo mostrando como integrar tudo isso num sistema próprio. | [src/LabelSharpDesigner.SampleApp](src/LabelSharpDesigner.SampleApp/README.md) |

## Testes

Cada projeto de lógica em `src/` tem um projeto de testes correspondente em `tests/`
(ex.: `LabelSharpDesigner.Core.Tests` testa `LabelSharpDesigner.Core`). Para rodar tudo:

```powershell
dotnet test LabelSharpDesigner.slnx
```

## Requisitos

- .NET 9 SDK.
- Windows para os projetos `App`, `SampleApp`, `UI.WinForms` e `PrintTransport.Windows` (dependem de
  WinForms/Win32). Os demais projetos são `netstandard2.0;net9.0` e rodam em qualquer plataforma.
