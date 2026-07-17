# LabelSharpDesignerCore — Referência de uso do plugin

Referência objetiva de **tudo** que o LabelSharpDesignerCore oferece para quem vai consumi-lo de outra
aplicação .NET: modelo de documento, elementos disponíveis, variáveis/expressões, resolução,
exportação, impressão e as telas prontas. Para "como funciona por dentro" veja
[ARCHITECTURE.md](ARCHITECTURE.md); para o passo a passo de montar uma tela de produtos + etiquetas
+ impressão do zero veja [INTEGRATION.md](INTEGRATION.md) (exemplo completo em
[`src/LabelSharpDesignerCore.SampleApp`](src/LabelSharpDesignerCore.SampleApp)). Este documento é o mapa de
API — cada seção tem código mínimo que roda de verdade.

## 1. O que referenciar

| Você quer... | Projeto |
|---|---|
| Montar/ler um `LabelDocument` na mão | `LabelSharpDesignerCore.Core` |
| Carregar/salvar arquivos `.label` | `LabelSharpDesignerCore.Serialization` |
| Resolver o documento (`{{ }}`, mm→dots) para export/print/preview | `LabelSharpDesignerCore.Layout` |
| Exportar PNG | `LabelSharpDesignerCore.Rendering.Png` |
| Exportar PDF | `LabelSharpDesignerCore.Rendering.Pdf` |
| Exportar/imprimir PPLA (Argox térmica) | `LabelSharpDesignerCore.Rendering.ArgoxPpla` |
| Enviar bytes para uma impressora Windows | `LabelSharpDesignerCore.PrintTransport.Windows` |
| Preview ao vivo dentro da sua própria UI WinForms | `LabelSharpDesignerCore.Rendering.Canvas` (+ `SkiaSharp.Views.WindowsForms`) |
| Reaproveitar as telas prontas (biblioteca, editor, impressão) | `LabelSharpDesignerCore.App` |

Todos multi-targetam `netstandard2.0;net9.0`, exceto `PrintTransport.Windows`, `UI.WinForms` e `App`
(`net48;net9.0-windows(...)`) — só esses três exigem um host Windows/WinForms, mas funcionam tanto em
.NET moderno quanto em .NET Framework 4.6.1+ (ver [INTEGRATION.md](INTEGRATION.md)).

## 2. Modelo de documento (`Core`)

Tudo é imutável (`record`) — qualquer alteração é um `with { ... }` produzindo uma nova instância.

```csharp
var document = new LabelDocument
{
    Name = "Etiqueta de produto",
    Page = new PageConfig { WidthMm = 100, HeightMm = 60, Dpi = 203 },
    Layers = [new LabelLayer { Id = "layer-1", Name = "Base", Order = 0 }],
    Variables =
    [
        new LabelVariable { Name = "descricao", Type = VariableValueType.String, DefaultValue = "Produto exemplo" },
        new LabelVariable { Name = "preco", Type = VariableValueType.Number, DefaultValue = "19.9" },
        new LabelVariable { Name = "codigobarras", Type = VariableValueType.String, DefaultValue = "7891234567890" },
    ],
    Elements = [ /* seção 3 */ ],
};
```

| Propriedade | Tipo | Para quê |
|---|---|---|
| `Page` | `PageConfig` | Tamanho (mm), DPI, orientação, margens, `Columns`/`ColumnGapMm` (mala direta — seção 5) |
| `Layers` | `IReadOnlyList<LabelLayer>` | `Id`, `Name`, `Visible`, `Locked`, `Order` — um elemento invisível ou numa camada oculta é pulado na resolução |
| `Styles` | `IReadOnlyList<LabelStyle>` | Estilos nomeados reutilizáveis (`Text`/`Shape`), referenciados por `StyleId` nos elementos |
| `Variables` | `IReadOnlyList<LabelVariable>` | Declaração de cada `{{ }}` que a etiqueta usa — `Name`, `Type` (`String`/`Number`/`Date`/`Boolean`), `DefaultValue`, `Description` (seção 4) |
| `Elements` | `IReadOnlyList<LabelElement>` | Conteúdo visual (seção 3) |
| `EditorSettings` | `EditorSettings` | Grade/snap/guias — preferência do editor, nunca afeta o output resolvido |
| `Metadata` | `DocumentMetadata` | `CreatedAt`/`UpdatedAt`/`ThumbnailPngBase64`/`Author` |

`PageConfig` por padrão: `Dpi = 203`, `Orientation = Portrait`, `Margins = PageMargins.Zero`,
`Columns = 1`, `ColumnGapMm = 0`.

## 3. Elementos disponíveis

Todo `LabelElement` tem `Id`, `Name?`, `Position` (`PointMm`), `Size` (`SizeMm`), `RotationDegrees`,
`Visible`, `Locked`, `Opacity`, `LayerId?`, `ZIndex`, `Transform` (`FlipH`/`FlipV`/`SkewX`/`SkewY`).

| Elemento | Campo principal | Extras |
|---|---|---|
| `TextElement` | `Content` (string, aceita `{{ }}` livre no meio do texto) | `StyleId?`, `Style` (`TextStyleSpec`) |
| `BarcodeElement` | `Data` (string, aceita `{{ }}`) | `Symbology` (`Ean13`/`Ean8`/`Code39`/`Code128`/`Upc`/`Itf`/`Codabar`), `ShowText`, `ModuleWidth`, `TextSize` |
| `QrCodeElement` | `Data` (string, aceita `{{ }}`) | `ErrorCorrectionLevel` (`Low`/`Medium`/`Quartile`/`High`) |
| `ImageElement` | `Source` (caminho/URL, aceita `{{ }}`) | `Fit` (`Contain`/`Cover`/`Fill`/`FitWidth`/`FitHeight`/`None`), `CropPosition?`, `CropSize?` |
| `RectangleElement` | `Style` (`ShapeStyleSpec`) | `CornerRadius` |
| `EllipseElement` / `CircleElement` | `Style` (`ShapeStyleSpec`) | — |
| `LineElement` | `StrokeColor`, `StrokeWidth` | — |
| `DateElement` / `TimeElement` | `Format` (formato .NET, ex. `dd/MM/yyyy`) | `Source` (`Now` ou `Variable`), `VariableName?`, `Style` |
| `TableElement` | `Columns` (`IReadOnlyList<TableColumn>`: `Header`, `DataField`, `WidthMm`) | `RowHeightMm`, `HeaderStyle`, `CellStyle` |
| `GroupElement` | `Children` (`IReadOnlyList<LabelElement>`) | Não gera elemento próprio — achata nos filhos, compondo rotação |
| `VariableElement` *(legado)* | `Expression` (expressão **nua**, sem `{{ }}`) | Mantido só por compatibilidade com `.label` antigos — um `TextElement` com `Content = "{{ expressão }}"` já faz exatamente o mesmo, sem essa pegadinha; prefira sempre `TextElement` em conteúdo novo |

`ShapeStyleSpec`: `BorderColor`/`BorderWidthMm`/`FillColor?` (`ArgbColor`, `null` = sem preenchimento).
`TextStyleSpec`: `FontFamily`/`FontSizePt`/`Bold`/`Italic`/`Underline`/`Color`/`Align`
(`Left`/`Center`/`Right`/`Justify`). `ArgbColor` é `(byte A, R, G, B)`, com `.ToHex()`/`ArgbColor.FromHex(...)`.

```csharp
Elements =
[
    new TextElement
    {
        Id = "t1", Position = new PointMm(5, 5), Size = new SizeMm(60, 8),
        Content = "{{descricao}} — R$ {{preco}}",
        Style = TextStyleSpec.Default with { FontSizePt = 12, Bold = true },
    },
    new BarcodeElement
    {
        Id = "b1", Position = new PointMm(5, 20), Size = new SizeMm(60, 20),
        Data = "{{codigobarras}}", Symbology = BarcodeSymbology.Code128, ShowText = true,
    },
    new QrCodeElement
    {
        Id = "q1", Position = new PointMm(70, 5), Size = new SizeMm(25, 25),
        Data = "{{codigobarras}}",
    },
]
```

## 4. Variáveis e expressões `{{ }}`

- **`TextElement.Content`, `BarcodeElement.Data`, `QrCodeElement.Data`, `ImageElement.Source`**: texto
  livre com placeholders `{{ expressão }}` misturados (`TemplateResolver`) — qualquer trecho fora das
  chaves é copiado literalmente. Uma expressão pode ser mais que um nome nu: `{{ preco * 1.2 }}`,
  `{{ produto.nome }}` (acesso a membro de um objeto no `SampleData`) etc.
- **`VariableElement.Expression`** (legado — seção 3): expressão **nua**, nunca envolta em `{{ }}`.
- Toda variável referenciada precisa existir em `LabelDocument.Variables` com o mesmo nome — referenciar
  uma variável não declarada lança `ExpressionEvaluationException` ("Unknown variable '...'").
- `LabelVariable.Type` (`String`/`Number`/`Date`/`Boolean`) é só uma dica para quem constrói o
  `SampleData` (seção 5) — o `LayoutEngine` em si não valida o tipo do valor recebido.
- No editor (`LabelSharpDesignerCore.App`), o botão **"Variáveis..."** da toolbar declara/edita essa lista
  (com validação do `DefaultValue` contra o `Type`), e o botão **"{{ }} Inserir variável..."** ao lado
  de cada campo de texto/dados insere `{{ nome }}` no cursor — nenhum dos dois é obrigatório para usar
  o plugin via código, mas é o caminho mais rápido para montar etiquetas manualmente.

## 5. Resolvendo o documento (`Layout`)

**Regra de ouro: só o `LayoutEngine` resolve.** Nenhum exportador/renderizador recalcula layout — todos
recebem um `ResolvedDocument` já pronto.

```csharp
using LabelSharpDesignerCore.Layout;

var options = new LayoutOptions
{
    SampleData = new Dictionary<string, object?>
    {
        ["descricao"] = "Camiseta azul",
        ["preco"] = 49.9,              // Number → double
        ["codigobarras"] = "7891234567890",
    },
};

ResolvedDocument resolved = new LayoutEngine().Resolve(document, options);
// resolved.WidthDots / HeightDots / Dpi / Elements (cada um já em dots, com {{ }} avaliado)
```

`LayoutOptions` também aceita `Now` (`DateTimeOffset`, para `DateElement`/`TimeElement` com
`Source = Now`) e `ExpressionEngine` (troque só se precisar de funções/operadores customizados).

### Mala direta / colunas (`ResolveBatch`)

Quando `Page.Columns > 1`, use `ResolveBatch` para imprimir vários registros lado a lado por fileira
física — um `ResolvedDocument` por fileira, cada um já com `Columns` etiquetas tiladas:

```csharp
IReadOnlyList<IReadOnlyDictionary<string, object?>> registros =
[
    new Dictionary<string, object?> { ["descricao"] = "Produto A", ["preco"] = 10.0, ["codigobarras"] = "111" },
    new Dictionary<string, object?> { ["descricao"] = "Produto B", ["preco"] = 20.0, ["codigobarras"] = "222" },
];

IReadOnlyList<ResolvedDocument> fileiras = new LayoutEngine().ResolveBatch(document, registros);
```

Nunca some um multiplicador de "cópias" por cima disso — a contagem de `registros` já **é** a
quantidade de etiquetas físicas (ver [ARCHITECTURE.md §5](ARCHITECTURE.md#5-impressão-em-colunas--mala-direta)).

## 6. Exportando

```csharp
using LabelSharpDesignerCore.Rendering.Png;
using LabelSharpDesignerCore.Rendering.Pdf;

byte[] png = PngExporter.Export(resolved, PngScale.X2);              // 1x/2x/3x
byte[] thumb = PngExporter.ExportScaled(resolved, targetWidthPx: 240); // largura customizada

byte[] pdf = PdfExporter.Export(resolved);                 // uma etiqueta, um PDF
byte[] pdfLote = PdfExporter.ExportBatch(fileiras);         // uma página por fileira (ResolveBatch)
```

PDF é vetorial de verdade (`PdfSharp`/`XGraphics`) — texto e formas em vetor; barcode/QR sempre
embutidos como raster (nenhuma simbologia tem representação vetorial pura).

## 7. Imprimindo

```csharp
using LabelSharpDesignerCore.PrintTransport.Windows;

// PDF via driver do Windows — funciona com qualquer impressora instalada
new WindowsPdfPrintTransport { Copies = 1 }.Send(pdf, target: null); // null = impressora padrão

// Listar impressoras instaladas
IReadOnlyList<string> impressoras = new WindowsPrinterDiscovery().ListAvailable();
```

Para impressoras térmicas Argox (PPLA), via `LabelSharpDesignerCore.Rendering.ArgoxPpla`:

```csharp
using LabelSharpDesignerCore.Rendering.ArgoxPpla;

var opcoes = new ArgoxRendererOptions
{
    Darkness = 10,                                  // escurecimento, 2-20
    TransferType = ArgoxTransferType.DirectThermal,  // ou ThermalTransfer (com ribbon)
    FeedOffsetMm = 0,                                // avanço de etiqueta — calibração manual
    OffsetXMm = 0, OffsetYMm = 0,                     // deslocamento de impressão — calibração manual
};

// PPLA nativo — um comando por elemento, a impressora desenha
byte[] pplaNativo = PplaCommandBuilder.Build(resolved, opcoes);

// PPLA raster — rasteriza o ResolvedDocument inteiro (usa quando o layout foge das capacidades do firmware)
byte[] pplaRaster = PplaRasterBuilder.Build(resolved, new ArgoxRasterOptions
{
    Base = opcoes,
    FullResolution = true,      // qualidade — resolução total do cabeçote (D11)
    MirrorHorizontal = true,    // espelhamento horizontal — mantenha true (ver doc do campo)
    ReverseRowOrder = false,
});

new WindowsRawPrintTransport().Send(pplaNativo, target: "Nome da impressora");
```

`WindowsRawPrintTransport` envia bytes crus direto para o spooler (`winspool.drv`), sem passar pelo
driver — é o único caminho para PPLA. `target: null` usa a impressora padrão do Windows em ambos.

## 8. Serialização (`.label`)

```csharp
using LabelSharpDesignerCore.Serialization;

string json = LabelDocumentCodec.Save(document);
File.WriteAllText("etiqueta.label", json);

LabelDocument carregado = LabelDocumentCodec.Load(File.ReadAllText("etiqueta.label"));
```

`Load` aplica automaticamente a `MigrationChain` para arquivos de versões antigas do schema — nunca
precisa tratar isso manualmente.

## 9. Telas prontas (`LabelSharpDesignerCore.App`)

Não precisa reconstruir UI nenhuma — todo o editor visual já existe e é reaproveitável direto:

| Classe | O que é |
|---|---|
| `LibraryRepository` | Catálogo em disco de `.label` (`%APPDATA%\LabelSharpDesignerCore\Labels`). `Open()`/`OpenAt(dir)`, `List()`, `Create(pageConfig)`, `Save(entry, doc)`, `Duplicate(entry)`, `Delete(entry)` |
| `LibraryForm` | Tela completa de listar/criar/editar/duplicar/excluir/exportar/imprimir — `new LibraryForm(repository).ShowDialog()` |
| `EditorForm` | Editor visual de um documento — `new EditorForm(document, doc => Save(doc)).ShowDialog()` |
| `VariablesForm` | Declarar/editar `LabelDocument.Variables` (com validação de tipo) |
| `PageSettingsForm` | Editar `PageConfig` |
| `ExportDialogForm` / `PrintDialogForm` | Exportar (PNG/PDF) / imprimir (PDF, PPLA nativo, PPLA raster) um documento, com preview ao vivo e coleta de dados de amostra pelas `Variables` |

Passo a passo completo (com um app de exemplo do zero: cadastro de produtos + etiquetas + impressão
em lote com vínculo dinâmico de campos) em [INTEGRATION.md](INTEGRATION.md).

## 10. Exemplo completo

Ponta a ponta sem nenhuma UI — monta o documento, resolve, exporta PNG/PDF e imprime:

```csharp
using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Core.Elements;
using LabelSharpDesignerCore.Core.Geometry;
using LabelSharpDesignerCore.Core.Styles;
using LabelSharpDesignerCore.Layout;
using LabelSharpDesignerCore.PrintTransport.Windows;
using LabelSharpDesignerCore.Rendering.Pdf;
using LabelSharpDesignerCore.Rendering.Png;
using LabelSharpDesignerCore.Serialization;

var document = new LabelDocument
{
    Name = "Etiqueta de produto",
    Page = new PageConfig { WidthMm = 100, HeightMm = 60, Dpi = 203 },
    Layers = [new LabelLayer { Id = "layer-1", Name = "Base", Order = 0 }],
    Variables =
    [
        new LabelVariable { Name = "descricao", Type = VariableValueType.String, DefaultValue = "Produto exemplo" },
        new LabelVariable { Name = "preco", Type = VariableValueType.Number, DefaultValue = "19.9" },
        new LabelVariable { Name = "codigobarras", Type = VariableValueType.String, DefaultValue = "7891234567890" },
    ],
    Elements =
    [
        new TextElement
        {
            Id = "t1", Position = new PointMm(5, 5), Size = new SizeMm(90, 8),
            Content = "{{descricao}} — R$ {{preco}}",
            Style = TextStyleSpec.Default with { FontSizePt = 12, Bold = true },
        },
        new BarcodeElement
        {
            Id = "b1", Position = new PointMm(5, 20), Size = new SizeMm(60, 20),
            Data = "{{codigobarras}}", Symbology = BarcodeSymbology.Code128,
        },
        new QrCodeElement
        {
            Id = "q1", Position = new PointMm(70, 20), Size = new SizeMm(20, 20),
            Data = "{{codigobarras}}",
        },
    ],
};

// 1. Persistir como .label (opcional — só se quiser guardar/reabrir no editor depois)
File.WriteAllText("etiqueta.label", LabelDocumentCodec.Save(document));

// 2. Resolver com os dados reais de um produto
var options = new LayoutOptions
{
    SampleData = new Dictionary<string, object?>
    {
        ["descricao"] = "Camiseta azul",
        ["preco"] = 49.9,
        ["codigobarras"] = "7891234567890",
    },
};
var resolved = new LayoutEngine().Resolve(document, options);

// 3. Exportar
File.WriteAllBytes("etiqueta.png", PngExporter.Export(resolved, PngScale.X2));
File.WriteAllBytes("etiqueta.pdf", PdfExporter.Export(resolved));

// 4. Imprimir (PDF via driver do Windows, impressora padrão)
new WindowsPdfPrintTransport { Copies = 1 }.Send(PdfExporter.Export(resolved), target: null);
```

## 11. Mapa de projetos (referência rápida)

Ver [ARCHITECTURE.md §2](ARCHITECTURE.md#2-mapa-de-projetos) para a tabela completa
projeto → TFM → dependências → responsabilidade, e §8 para as pegadinhas que valem saber antes de
integrar (expressão nua vs. template, `Page.Columns`/mala direta, config de app vs. config do
documento).
