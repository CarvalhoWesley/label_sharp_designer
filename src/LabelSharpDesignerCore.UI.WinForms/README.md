# LabelSharpDesignerCore.UI.WinForms

## O que é

Os **controles visuais reutilizáveis** do editor de etiquetas: a área de desenho interativa onde o
usuário arrasta/redimensiona/rotaciona elementos, e os painéis auxiliares ao redor dela (camadas,
propriedades, régua). Este projeto não é o aplicativo final — é a "caixa de peças" de UI que o
`LabelSharpDesignerCore.App` monta dentro das próprias janelas (e que qualquer outra aplicação WinForms
também pode reaproveitar).

## Peças principais (`Canvas/`)

- **`LabelCanvasControl`** — o controle principal: a superfície onde o usuário edita a etiqueta
  visualmente. Cuida de seleção, arraste, redimensionamento (`ResizeHandle`), rotação, alinhamento
  automático (`AlignmentSnap`) e integra com o `LabelSharpDesignerCore.History` para undo/redo.
  - Dispara dois eventos diferentes de mudança: `DocumentChanged` (só para mudanças **confirmadas**,
    não-preview — use para reconstruir painéis, algo "caro") e `LiveChanged` (dispara em **toda**
    mudança, inclusive durante um arraste em andamento — use só para listeners baratos, tipo
    atualizar um label de posição).
- **`PlaceholderDrawingVisitor`** — durante a edição, o canvas **não** usa o pipeline real de
  renderização (`LayoutEngine` + `Rendering.Canvas`) para desenhar — ele desenha placeholders
  simplificados direto do modelo de domínio, através deste visitor, para manter o arraste/
  redimensionamento leve e responsivo. Por isso a aba "Pré-visualizar" (que usa o pipeline real) é
  sempre a fonte de verdade sobre "o que vai sair impresso", não o que aparece durante a edição.
- **`ElementGeometry`** — cálculos de geometria (bounds, pontos de manipulação) dos elementos na
  tela.
- **`CanvasTransform`** — conversão entre coordenadas de tela (pixels da janela) e coordenadas do
  documento (mm).
- **`ColorExtensions`** — conversão entre `ArgbColor` (do `Core`) e `System.Drawing.Color`.

## Peças principais (`Panels/`)

- **`PropertyPanel`** — painel de propriedades do elemento selecionado (posição, tamanho, estilo
  etc.).
- **`LayersPanel`** — lista/gerencia as camadas (`LabelLayer`) do documento.
- **`RulerControl`** — as réguas horizontal/vertical ao redor do canvas.

## Por que separar UI de lógica

Ao manter os controles visuais num projeto separado de `LabelSharpDesignerCore.App`, é possível montar
outra janela/aplicativo próprio reaproveitando `LabelCanvasControl` sem precisar reconstruir a
biblioteca inteira do `App` — é exatamente o que faz sentido se você quiser, por exemplo, um botão
"editar etiqueta" dentro da sua própria tela, sem abrir o app completo.

## Dependências

Depende de `LabelSharpDesignerCore.Core`, `LabelSharpDesignerCore.Layout`, `LabelSharpDesignerCore.Expressions`,
`LabelSharpDesignerCore.History` e `LabelSharpDesignerCore.Rendering.Canvas`. Alvo
`net9.0-windows10.0.19041.0`: **exige Windows** (WinForms).
