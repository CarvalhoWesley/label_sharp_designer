# LabelSharpDesignerCore

LabelSharpDesignerCore é um **editor de etiquetas** (como as de código de barras, preço, endereço etc.)
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
- Quer **usar** o LabelSharpDesignerCore a partir de outra aplicação .NET? Veja
  [INTEGRATION.md](INTEGRATION.md) (passo a passo completo) e [USAGE.md](USAGE.md) (referência de
  API). Para um passo a passo curto e direto (implementar o editor, gerenciar etiquetas, imprimir),
  veja [GUIA_RAPIDO_MODERNO.md](GUIA_RAPIDO_MODERNO.md) (.NET moderno) ou
  [GUIA_RAPIDO_FRAMEWORK.md](GUIA_RAPIDO_FRAMEWORK.md) (.NET Framework 4.x).
- Quer ver um exemplo funcionando de ponta a ponta? Abra
  [`src/LabelSharpDesignerCore.SampleApp`](src/LabelSharpDesignerCore.SampleApp).

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
| `LabelSharpDesignerCore.Core` | O modelo de dados: o que é uma etiqueta, o que é um elemento de texto/código de barras/etc. | [src/LabelSharpDesignerCore.Core](src/LabelSharpDesignerCore.Core/README.md) |
| `LabelSharpDesignerCore.Expressions` | Motor que entende e calcula `{{ expressões }}` dentro dos textos. | [src/LabelSharpDesignerCore.Expressions](src/LabelSharpDesignerCore.Expressions/README.md) |
| `LabelSharpDesignerCore.Layout` | Transforma o desenho (mm, expressões) em coordenadas prontas em pixels. | [src/LabelSharpDesignerCore.Layout](src/LabelSharpDesignerCore.Layout/README.md) |
| `LabelSharpDesignerCore.Serialization` | Salva e carrega arquivos `.label` (JSON versionado). | [src/LabelSharpDesignerCore.Serialization](src/LabelSharpDesignerCore.Serialization/README.md) |
| `LabelSharpDesignerCore.Barcode` | Gera o desenho (raster) de códigos de barras. | [src/LabelSharpDesignerCore.Barcode](src/LabelSharpDesignerCore.Barcode/README.md) |
| `LabelSharpDesignerCore.History` | Undo/redo do editor. | [src/LabelSharpDesignerCore.History](src/LabelSharpDesignerCore.History/README.md) |
| `LabelSharpDesignerCore.Rendering.Abstractions` | Contrato comum usado pelos "desenhadores" (renderers). | [src/LabelSharpDesignerCore.Rendering.Abstractions](src/LabelSharpDesignerCore.Rendering.Abstractions/README.md) |
| `LabelSharpDesignerCore.Rendering.Canvas` | O "desenhador" principal (SkiaSharp) — usado no preview, no PNG e na impressão raster. | [src/LabelSharpDesignerCore.Rendering.Canvas](src/LabelSharpDesignerCore.Rendering.Canvas/README.md) |
| `LabelSharpDesignerCore.Rendering.Png` | Exporta a etiqueta como imagem PNG. | [src/LabelSharpDesignerCore.Rendering.Png](src/LabelSharpDesignerCore.Rendering.Png/README.md) |
| `LabelSharpDesignerCore.Rendering.Pdf` | Exporta a etiqueta como PDF vetorial. | [src/LabelSharpDesignerCore.Rendering.Pdf](src/LabelSharpDesignerCore.Rendering.Pdf/README.md) |
| `LabelSharpDesignerCore.Rendering.ArgoxPpla` | Gera os comandos para impressoras térmicas Argox (PPLA). | [src/LabelSharpDesignerCore.Rendering.ArgoxPpla](src/LabelSharpDesignerCore.Rendering.ArgoxPpla/README.md) |
| `LabelSharpDesignerCore.PrintTransport.Windows` | Manda os bytes (PDF ou PPLA) para a impressora de verdade. | [src/LabelSharpDesignerCore.PrintTransport.Windows](src/LabelSharpDesignerCore.PrintTransport.Windows/README.md) |
| `LabelSharpDesignerCore.UI.WinForms` | Os controles visuais reutilizáveis do editor (a área de desenho, painéis). | [src/LabelSharpDesignerCore.UI.WinForms](src/LabelSharpDesignerCore.UI.WinForms/README.md) |
| `LabelSharpDesignerCore.App` | O aplicativo final: telas prontas de biblioteca, editor, exportação e impressão. | [src/LabelSharpDesignerCore.App](src/LabelSharpDesignerCore.App/README.md) |
| `LabelSharpDesignerCore.Legacy.Bridge` | Ponte para sistemas antigos (.NET Framework 4.x) abrirem o editor como programa separado. | [src/LabelSharpDesignerCore.Legacy.Bridge](src/LabelSharpDesignerCore.Legacy.Bridge/README.md) |
| `LabelSharpDesignerCore.SampleApp` | Aplicativo de exemplo mostrando como integrar tudo isso num sistema próprio. | [src/LabelSharpDesignerCore.SampleApp](src/LabelSharpDesignerCore.SampleApp/README.md) |

## Testes

Cada projeto de lógica em `src/` tem um projeto de testes correspondente em `tests/`
(ex.: `LabelSharpDesignerCore.Core.Tests` testa `LabelSharpDesignerCore.Core`). Para rodar tudo:

```powershell
dotnet test LabelSharpDesignerCore.slnx
```

## Requisitos

- .NET 9 SDK.
- Windows para os projetos `App`, `SampleApp`, `UI.WinForms` e `PrintTransport.Windows` (dependem de
  WinForms/Win32). Os demais projetos são `netstandard2.0;net9.0` e rodam em qualquer plataforma.
