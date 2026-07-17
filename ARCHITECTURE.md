# Arquitetura — LabelSharpDesignerCore

LabelSharpDesignerCore é uma recriação em .NET 9 / C# / WinForms do produto Flutter
`flutter_label_designer`: um editor de etiquetas (documento → elementos →
layout → renderização → impressão), pensado para ser referenciado por uma
solução legada ASP.NET Framework 4.x como um app satélite (ver
[§7 Legacy.Bridge](#7-integração-com-o-legado--labelsharpdesignerlegacybridge)).

A regra estrutural mais importante do repositório: **só o `LayoutEngine`
resolve um documento** (mm → dots, `{{ }}` avaliados, z-order, rotação
composta). Nenhum renderer, exportador ou caminho de impressão tem permissão
para recalcular layout — todos recebem um `ResolvedDocument` já pronto. Isso é
o mesmo contrato do projeto Flutter original (`docs/ARCHITECTURE.md` lá,
seções 1–2 e 9) e é o que garante que Canvas, PNG, PDF e PPLA nunca divergem
entre si.

Se você está integrando o LabelSharpDesignerCore a partir de **outra aplicação** (em vez de mexer no
editor em si), veja [INTEGRATION.md](INTEGRATION.md) — um guia de consumo, com exemplo completo em
`src/LabelSharpDesignerCore.SampleApp` — ou [USAGE.md](USAGE.md), a referência objetiva de toda a API
(modelo de documento, elementos, variáveis, resolução, exportação, impressão).

## 1. Visão geral do pipeline

```mermaid
flowchart LR
    subgraph Domínio
        LD[LabelDocument\n+ LabelElement subtypes]
        HM[HistoryManager\nundo/redo]
    end

    subgraph Layout
        LE[LayoutEngine.Resolve /\nResolveBatch]
        EE[ExpressionEngine\n{{ }} ]
    end

    RD[ResolvedDocument\nResolvedElement/Payload]

    subgraph Renderização
        RC[Rendering.Canvas\nSkiaSharp — fonte única]
        RP[Rendering.Png]
        RF[Rendering.Pdf\nPdfSharp]
        RA[Rendering.ArgoxPpla\nPPLA nativo / raster]
    end

    subgraph Saída
        UI[LabelCanvasControl\npreview ao vivo]
        FILE[Arquivo .label / .pdf / .png]
        PRN[Impressora física\nPrintTransport.Windows]
    end

    LD --> HM --> LE
    LD --> LE
    EE --> LE
    LE --> RD
    RD --> RC --> UI
    RD --> RP --> FILE
    RD --> RF --> FILE
    RD --> RF --> PRN
    RD --> RA --> PRN
```

`LabelCanvasControl`, a superfície interativa do editor, é a única exceção
deliberada: durante edição ela desenha **placeholders simplificados
diretamente do modelo de domínio** (`PlaceholderDrawingVisitor`), sem passar
pelo `LayoutEngine`/`Rendering.*`, para manter o drag/resize barato. A aba
"Pré-visualizar", o export e a impressão sempre usam o pipeline real acima —
é por isso que o preview é a fonte de verdade sobre "o que vai sair impresso",
não o canvas.

## 2. Mapa de projetos

| Projeto | TFM | Depende de | Responsabilidade |
|---|---|---|---|
| `LabelSharpDesignerCore.Core` | `netstandard2.0;net9.0` | — | Modelo de domínio (`LabelDocument`, `LabelElement` + subtipos, `PageConfig`, `LabelStyle`/`LabelLayer`/`LabelVariable`, `EditorSettings`) **e** os tipos de saída do layout (`ResolvedDocument`/`ResolvedElement`/`ResolvedPayload`) — mesma colocação do `label_core` original. |
| `LabelSharpDesignerCore.Expressions` | `netstandard2.0;net9.0` | — | Lexer → parser (AST) → evaluator de expressões `{{ }}`, zero dependências (nem de `Core`). `TemplateResolver` resolve placeholders embutidos em texto livre. |
| `LabelSharpDesignerCore.Layout` | `netstandard2.0;net9.0` | `Core`, `Expressions` | `LayoutEngine.Resolve` (um documento) e `.ResolveBatch` (mala direta/colunas — ver [§5](#5-impressão-em-colunas--mala-direta)). Único lugar que converte mm→dots e avalia expressões contra dados de amostra. |
| `LabelSharpDesignerCore.Serialization` | `netstandard2.0;net9.0` | `Core` | Codec JSON versionado (`LabelDocumentCodec`), conversor polimórfico por tipo de `LabelElement`, `MigrationChain`/`IMigration` para versões antigas de `.label`. |
| `LabelSharpDesignerCore.Barcode` | `netstandard2.0;net9.0` | `Core` | Adapter sobre ZXing.Net — gera **só o raster dos módulos/barras**, sem nenhuma capacidade de desenhar texto humano-legível (ver [§4](#4-texto-legível-em-código-de-barras)). |
| `LabelSharpDesignerCore.History` | `netstandard2.0;net9.0` | `Core` | Command pattern imutável: `AddCommand`, `DeleteCommand`, `MoveCommand`, `ResizeCommand`, `RotateCommand`, `ChangePropertyCommand`, `ChangeDocumentCommand`, `CompositeCommand`. `HistoryManager` mantém as pilhas undo/redo. |
| `LabelSharpDesignerCore.Rendering.Abstractions` | `netstandard2.0;net9.0` | `Core` | `IResolvedPayloadVisitor<TResult>` — o contrato que Canvas/Pdf implementam para despachar sobre `ResolvedPayload`. |
| `LabelSharpDesignerCore.Rendering.Canvas` | `netstandard2.0;net9.0` | `Core`, `Rendering.Abstractions`, `Barcode` | **A única rotina de desenho "de verdade"** (SkiaSharp `SKCanvas`) — reusada por preview, PNG e raster PPLA. `TextDrawing` (com quebra de linha), `ShapeDrawing`, `TableDrawing`, `ImageDrawing`. |
| `LabelSharpDesignerCore.Rendering.Png` | `netstandard2.0;net9.0` | `Core`, `Rendering.Abstractions`, `Rendering.Canvas` | `PngExporter` — `Rendering.Canvas` para um `SKBitmap` em 1×/2×/3× ou largura customizada. |
| `LabelSharpDesignerCore.Rendering.Pdf` | `netstandard2.0;net9.0` | `Core`, `Rendering.Abstractions`, `Barcode` | `PdfExporter` (PdfSharp) — texto vetorial real via `XGraphics`/`XFont`, com quebra de linha própria (não compartilha código com `Rendering.Canvas`, ver [§8](#8-convenções-e-pegadinhas)). |
| `LabelSharpDesignerCore.Rendering.ArgoxPpla` | `netstandard2.0;net9.0` | `Core`, `Rendering.Abstractions`, `Rendering.Canvas`, `Barcode` | `PplaCommandBuilder` (comandos PPLA nativos por elemento) e `PplaRasterBuilder` (rasteriza via `Rendering.Canvas` e envia como imagem monocromática) para impressoras térmicas Argox. |
| `LabelSharpDesignerCore.PrintTransport.Windows` | `net9.0-windows` | — | Envio de bytes para a impressora: `WindowsRawPrintTransport` (P/Invoke em `winspool.drv`, equivalente ao `RawPrinterHelper` clássico) para PPLA cru, `WindowsPdfPrintTransport` para PDF via driver, `WindowsPrinterDiscovery`. |
| `LabelSharpDesignerCore.UI.WinForms` | `net9.0-windows10.0.19041.0` | `Core`, `Layout`, `Expressions`, `History`, `Rendering.Canvas` | `LabelCanvasControl` (superfície interativa), `PropertyPanel`, `LayersPanel`, `RulerControl`, `AlignmentSnap`. |
| `LabelSharpDesignerCore.App` | `net9.0-windows10.0.19041.0` (`WinExe`) | quase todos os projetos acima + `Legacy.Bridge` | Composition root: `Program.cs`, `LibraryForm`, `EditorForm`, `ExportDialogForm`, `PrintDialogForm`, `PageSettingsForm`, `LibraryRepository`, configurações de app (`PrintSettings*`, `EditorLayoutSettings*`). |
| `LabelSharpDesignerCore.Legacy.Bridge` | `netstandard2.0;net9.0` | — | DTOs `LaunchRequest`/`LaunchResult`/`LaunchOutcome` e `LegacyLauncher`, o contrato usado pelo ASP.NET Framework legado para iniciar o `App` como processo satélite (ver [§7](#7-integração-com-o-legado--labelsharpdesignerlegacybridge)). |

Todo projeto de domínio/lógica multi-targeta `netstandard2.0;net9.0` — isso é
o que permite o legado ASP.NET Framework 4.x referenciar `Core`,
`Serialization`, `Layout` etc. diretamente (mesmo processo), enquanto só o
`App` satélite (WinForms, `net9.0-windows`) roda como processo separado via
`Legacy.Bridge`.

`Directory.Build.props` na raiz fixa `LangVersion=latest`,
`Nullable=enable`, `ImplicitUsings=enable`, e `TreatWarningsAsErrors=true`
para todo projeto de domínio/lógica (mas não para `UI.WinForms`, `App`, nem
para os projetos de teste). Usa `PolySharp` para poder escrever
`record`/`init`/`required` mesmo na perna `netstandard2.0`.

## 3. Modelo de domínio (`Core`)

- `LabelDocument` — raiz imutável (`record`): `Page` (`PageConfig`),
  `Layers`, `Styles`, `Variables`, `Elements`, `EditorSettings` (grade,
  snap, guias — persistido **com o documento**, não como preferência de
  app), `Metadata`.
- `LabelElement` — hierarquia abstrata de records
  (`TextElement`, `RectangleElement`, `EllipseElement`, `CircleElement`,
  `LineElement`, `BarcodeElement`, `QrCodeElement`, `ImageElement`,
  `TableElement`, `GroupElement`, `DateElement`, `TimeElement`,
  `VariableElement`), despachada via `IElementVisitor<TResult>` (Visitor
  pattern) — usado tanto pelo `LayoutEngine` quanto pelo placeholder do
  canvas.
  - `VariableElement` é mantido só por compatibilidade com `.label`
    salvos antes desta observação — não aparece mais no menu "+
    Adicionar" do editor (`NewElementFactory`). Um `TextElement` cujo
    `Content` inteiro é um único `{{ expressão }}` já resolve
    (via `TemplateResolver`) para exatamente o mesmo valor, sem a
    pegadinha de `VariableElement.Expression` ser uma expressão nua
    (ver §8) — ou seja, não existe mais nenhum caso que só
    `VariableElement` resolva. Arquivos antigos com um continuam
    abrindo/renderizando/imprimindo normalmente; o editor só parou de
    oferecer a criação de um novo.
- `ResolvedDocument`/`ResolvedElement`/`ResolvedPayload` — a saída do
  `LayoutEngine`, deliberadamente "cega a metadados": só contém
  `WidthDots`/`HeightDots`/`Dpi`/`Elements`, nunca nome do documento, id ou
  timestamps. É por isso que PDF/PNG/PPLA nunca carregam metadados do
  documento — só o formato de projeto `.label` (via `Serialization`)
  serializa o `LabelDocument` completo.

Tudo é imutável (`with` para produzir a próxima versão) — é o que permite o
Command pattern do `History` guardar `Before`/`After` como snapshots inteiros
sem se preocupar com aliasing.

## 4. Texto legível em código de barras

`BarcodeGenerator` (sobre ZXing.Net) só produz o raster das barras/módulos —
não tem nenhuma capacidade de desenhar texto. Cada renderer que precisa
mostrar o texto legível (`ShowText`) faz isso como um passo separado,
reservando uma faixa na base do elemento:

- `Rendering.Canvas` (`LabelCanvasRenderer`, compartilhado por
  preview/PNG/raster PPLA): desenha o texto via SkiaSharp depois de gerar as
  barras na área restante.
- `Rendering.Pdf` (`BarcodeDrawing.DrawBarcodeText`): mesma ideia via
  `XGraphics`/`XFont`.
- PPLA nativo não precisa desse tratamento — o firmware da própria impressora
  desenha o texto a partir do comando de barcode.

## 5. Impressão em colunas (mala direta)

`PageConfig.Columns`/`ColumnGapMm` descrevem um rolo com várias etiquetas por
fileira física. `LayoutEngine.ResolveBatch` (porta direta do
`resolveBatch` do `label_layout_engine` original) tila uma lista de
registros em fileiras de `Columns` etiquetas cada, deslocando a coluna `n`
em `n * (larguraMm + gapMm)`.

`PrintDialogForm` expõe **um único controle de quantidade** ("Cópias" /
"Quantidade de etiquetas"), nunca dois multiplicadores empilhados:

- Etiqueta de coluna única: é literalmente "repita N vezes" (impressora/
  driver repete o mesmo `ResolvedDocument`).
- Rolo multi-coluna (`Columns > 1`): a quantidade **é** o total de
  etiquetas — esse número de registros é tilado em `Columns` por
  `ResolveBatch`, e cada fileira física é enviada exatamente uma vez (nunca
  repetida por cópia extra do driver/PPLA). Ex.: 2 colunas + quantidade 2 =
  exatamente 2 etiquetas lado a lado, não 2 fileiras de 2.
- "Um valor diferente por etiqueta" troca o campo de quantidade por uma
  grade (um registro por linha) para o caso de imprimir N itens diferentes,
  um por coluna, sem desperdiçar etiquetas.

Mirrors `apps/label_studio/lib/src/print/print_dialog.dart` do projeto
Flutter original controle a controle.

## 6. Impressão / exportação

- **PDF**: `Rendering.Pdf.PdfExporter` (vetorial, PdfSharp) →
  `WindowsPdfPrintTransport` (via driver do Windows) ou salvo em disco pelo
  `ExportDialogForm`.
- **PPLA nativo**: `Rendering.ArgoxPpla.PplaCommandBuilder` (um comando PPLA
  por elemento) → `WindowsRawPrintTransport` (bytes crus via
  `winspool.drv`).
- **PPLA raster**: `PplaRasterBuilder` rasteriza o `ResolvedDocument` inteiro
  via `Rendering.Canvas` e envia como imagem monocromática — usado quando o
  layout foge das capacidades nativas do firmware PPLA.
- **PNG**: `Rendering.Png.PngExporter`, 1×/2×/3× ou largura customizada.

## 7. Integração com o legado — `LabelSharpDesignerCore.Legacy.Bridge`

O app legado ASP.NET Framework 4.x não pode referenciar o `App` WinForms
`net9.0-windows` diretamente (frameworks incompatíveis), então a integração é
via processo satélite:

1. O legado referencia `Legacy.Bridge` (`netstandard2.0`, compatível com
   Framework 4.x) e usa `LegacyLauncher` para montar um `Process.Start` do
   `LabelSharpDesignerCore.App.exe` com `--edit <path> [--readonly]`.
2. `Program.Main` detecta esses argumentos via `LaunchRequest.TryParse` e
   entra em **modo de edição direta** (`RunEditMode`): abre o `EditorForm`
   sobre aquele arquivo, ignorando a biblioteca inteira.
3. O código de saída do processo é o contrato de retorno
   (`LaunchOutcome`: `Saved`/`Cancelled`/`Error`) que o legado lê depois do
   `Process.Start` terminar.

Sem argumentos, `Program.Main` entra em **modo biblioteca**
(`RunLibraryMode`): abre `LibraryForm` sobre `LibraryRepository`
(`%APPDATA%\LabelSharpDesignerCore\Labels`), com troca de tema
claro/escuro/sistema reabrindo a janela (repintar tema ao vivo não funciona
de forma confiável em controles WinForms já na tela).

## 8. Convenções e pegadinhas

Coisas que não são óbvias lendo um arquivo isolado, mas que valem a pena
saber antes de mexer no editor:

- **`HistoryManager.PreviewChange` vs `Execute`**: `PreviewChange` atualiza
  `Current` e dispara `Changed` (`IsPreview=true`) sem tocar nas pilhas de
  undo/redo — usado tanto para feedback ao vivo durante arraste quanto para
  mudanças "de configuração" não desfazíveis (grade/snap/guias).
  `Execute` grava um passo desfazível de verdade.
- **`LabelCanvasControl.DocumentChanged` vs `LiveChanged`**:
  `DocumentChanged` só dispara para mudanças confirmadas (não-preview) —
  listeners caros (reconstruir painéis) assinam aqui. `LiveChanged` dispara
  em toda mudança, inclusive preview de arraste — só para listeners baratos.
- **PdfSharp/`XGraphics`, escala global dots→pontos**: `PdfExporter` aplica
  um único `gfx.ScaleTransform(72.0/dpi)` por página, convertendo de "dots
  do documento" para pontos PDF. Por causa disso, toda coordenada E todo
  tamanho de fonte passado para a API do PdfSharp (`XFont`, `XRect`) tem
  que estar em dots, nunca em pontos/mm reais — senão a escala é aplicada
  duas vezes.
- **`VariableElement.Expression` é uma expressão nua, não um template**:
  diferente de `TextElement.Content` (que pode ter `{{ }}` misturado com
  texto livre e passa por `TemplateResolver`), `ElementResolvingVisitor.
  VisitVariable` avalia `Expression` diretamente como expressão — sem
  striping de chaves. Colocar `"{{nome}}"` ali (em vez de `"nome"`) faz o
  parser falhar (`{` não é um caractere válido de expressão) e quebra a
  resolução do documento inteiro.
- **Ordem de `Controls.Add` em WinForms decide docking**: o controle
  `Dock=Fill` precisa ser adicionado primeiro; entre vários controles no
  mesmo `Dock` (ex. `Right`), o adicionado por último fica mais perto do
  centro do container. Usado deliberadamente no `EditorForm` para posicionar
  as tiras recolhidas/splitters dos painéis laterais.
- **Barcodes e QR são sempre raster, mesmo no PDF vetorial**: ZXing.Net só
  expõe um buffer de módulos rasterizado — não há vetor puro para nenhuma
  simbologia, então `Rendering.Pdf` embute PNG mesmo dentro de um PDF
  vetorial.
- **Configurações de app vs. configurações do documento**: preferências
  como impressora/formato/última quantidade usada (`PrintSettings`) e
  tamanho/visibilidade dos painéis (`EditorLayoutSettings`) ficam em
  `%APPDATA%\LabelSharpDesignerCore\*.json` — são sobre *como você usa o app*,
  não sobre o documento. Grade/snap/guias, ao contrário, ficam dentro do
  próprio `LabelDocument.EditorSettings` porque são parte do documento.

## 9. Testes

Um projeto xUnit por lib de lógica
(`Core.Tests`, `Serialization.Tests`, `Layout.Tests`, `Expressions.Tests`,
`Barcode.Tests`, `History.Tests`, `Rendering.Png.Tests`,
`Rendering.Pdf.Tests`, `Rendering.ArgoxPpla.Tests`,
`PrintTransport.Windows.Tests`, `UI.WinForms.Tests`, `App.Tests`,
`Legacy.Bridge.Tests`). Onde faz sentido, `UI.WinForms.Tests` instancia
`LabelCanvasControl` direto (sem exibir janela) para testar
undo/redo/seleção/agrupamento de ponta a ponta.
