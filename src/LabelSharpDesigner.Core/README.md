# LabelSharpDesigner.Core

## O que é

O **modelo de dados** de tudo o que existe numa etiqueta. É o projeto mais fundamental do
repositório: praticamente todos os outros projetos dependem dele, e ele não depende de nenhum
outro projeto do LabelSharpDesigner. Pense nele como "os substantivos" do sistema — as classes
que descrevem *o que* é uma etiqueta, sem se preocupar em desenhar, calcular posição ou imprimir
nada disso.

## O que ele resolve

Antes deste projeto, seria preciso responder: "como eu represento, em código, uma etiqueta com um
texto, um código de barras e uma imagem, cada um na sua posição, cor e tamanho?". O `Core` dá essa
resposta através de um conjunto de classes imutáveis (`record`).

## Principais conceitos

- **`Document/LabelDocument`** — a etiqueta inteira: página (`PageConfig`), camadas (`Layers`),
  estilos reutilizáveis (`Styles`), variáveis `{{ }}` disponíveis (`Variables`), a lista de
  elementos visuais (`Elements`) e configurações do editor (`EditorSettings`).
- **`Elements/LabelElement`** — classe-base de tudo que pode ser desenhado numa etiqueta. Cada tipo
  de elemento é uma subclasse:
  - `TextElement` — um texto (pode conter `{{ variáveis }}` misturadas com texto livre).
  - `BarcodeElement` / `QrCodeElement` — código de barras / QR code.
  - `ImageElement` — uma imagem.
  - `RectangleElement`, `EllipseElement`, `CircleElement`, `LineElement` — formas geométricas.
  - `TableElement` — uma tabela simples.
  - `DateElement` / `TimeElement` — data/hora formatada.
  - `GroupElement` — agrupamento de outros elementos.
  - `VariableElement` — forma antiga (legado) de exibir uma variável; hoje um `TextElement` com
    `Content = "{{ variavel }}"` faz a mesma coisa, então não crie `VariableElement` novo.

  Todo elemento é despachado através de `IElementVisitor<TResult>` (padrão *Visitor*) — é assim
  que o resto do sistema (layout, desenho do editor) sabe "que tipo de elemento é esse" sem um
  monte de `if/else`/`switch` espalhado.
- **`Geometry/`** — tipos simples de posição e tamanho em milímetros: `PointMm`, `SizeMm`.
- **`Styles/`** — aparência: `TextStyleSpec` (fonte, tamanho, negrito, cor, alinhamento),
  `ShapeStyleSpec` (borda, preenchimento) e `ArgbColor` (cor com canal alfa).
- **`Layout/`** — a *saída* do cálculo de layout (feito pelo projeto `Layout`, não aqui):
  `ResolvedDocument`, `ResolvedElement`, `ResolvedPayload`. Ficam neste projeto porque tanto quem
  gera (`Layout`) quanto quem consome (os `Rendering.*`) precisam do mesmo tipo.

## Por que tudo é imutável

Toda classe aqui é um `record` — ou seja, para "alterar" algo você não muda a instância existente,
você cria uma cópia com a diferença: `documento with { Name = "Novo nome" }`. Isso pode parecer
estranho no começo, mas é o que permite o undo/redo do projeto `LabelSharpDesigner.History`
funcionar de forma simples: cada passo do histórico guarda um "antes" e um "depois" completos, sem
medo de que alguém mude esse objeto por baixo do pano depois.

## O que este projeto NÃO faz

- Não sabe desenhar nada na tela (isso é `Rendering.Canvas`/`Rendering.Pdf`/etc.).
- Não sabe calcular posição em pixels nem avaliar `{{ expressões }}` (isso é `Layout` +
  `Expressions`).
- Não sabe salvar/carregar arquivo `.label` (isso é `Serialization`).

## Compatibilidade

Multi-targeta `netstandard2.0;net9.0` — ou seja, roda tanto em .NET moderno quanto pode ser
referenciado por uma aplicação legada em .NET Framework 4.6.1+, sem precisar de Windows.

## Saiba mais

Para entender como um `LabelDocument` vira uma etiqueta impressa de fato, veja
[ARCHITECTURE.md](../../ARCHITECTURE.md) (seção 3) e [USAGE.md](../../USAGE.md) (seção 2 e 3) na
raiz do repositório.
