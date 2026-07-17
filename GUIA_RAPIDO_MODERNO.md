# Guia rápido — editor de etiquetas para aplicação .NET moderna

Versão enxuta de [INTEGRATION.md §2](INTEGRATION.md#2-caminho-a--referência-direta-aplicação-net-moderna)
com só o essencial: **implementar o editor**, **gerenciar as etiquetas criadas** e **imprimir**, já
com as três decisões de produto abaixo fixadas em código (não são opção do usuário final). Para
detalhes/casos de borda (vínculo de campos dinâmico, mala direta, preferências de app etc.) volte
para [INTEGRATION.md](INTEGRATION.md) e [USAGE.md](USAGE.md). Existe um exemplo funcionando de ponta
a ponta em [`src/LabelSharpDesigner.SampleApp`](src/LabelSharpDesigner.SampleApp) — todo trecho
abaixo foi tirado (ou simplificado) direto dele.

Vale para qualquer aplicação .NET moderna (mesmo TFM do próprio plugin — hoje `net9.0-windows`) que
roda no mesmo processo/host Windows, sem precisar do processo satélite do
[guia para .NET Framework 4.x](GUIA_RAPIDO_FRAMEWORK.md).

## Padrões fixados neste guia

| Ponto | Decisão |
|---|---|
| Elementos que o "+ Adicionar" do editor oferece | Só **Texto, Linha, Código de barras, QR Code, Imagem** — o resto (retângulo, elipse, tabela, data/hora...) não aparece |
| Painel de camadas | Nunca exibido |
| Formato de impressão | `PrintDialogForm` sempre abre em **PPLA raster** com **Transferência térmica (ribbon)** já selecionados — o usuário ainda pode trocar na hora, mas nunca é PDF/térmica direta por padrão |

## Passo 1 — Referenciar os projetos

Seu projeto precisa ser `net9.0-windows10.0.19041.0` com `UseWindowsForms=true`. Referencie
`LabelSharpDesigner.App` (traz tudo — telas prontas, `Layout`, `Rendering.*`,
`PrintTransport.Windows` — como dependências transitivas). Ver
[`LabelSharpDesigner.SampleApp.csproj`](src/LabelSharpDesigner.SampleApp/LabelSharpDesigner.SampleApp.csproj)
como modelo pronto.

## Passo 2 — Nova etiqueta (documento em branco)

`LibraryRepository.Create` já monta o documento em branco no padrão de
[USAGE.md §2](USAGE.md#2-modelo-de-documento-core) — nenhum código extra necessário:

```csharp
using LabelSharpDesigner.App.Library;

var labelRepository = LibraryRepository.Open(); // %APPDATA%\LabelSharpDesigner\Labels
var entry = labelRepository.Create(); // documento em branco, 100×60mm/203dpi por padrão
```

## Passo 3 — Implementar o editor (elementos restritos, sem painel de camadas)

`EditorForm`/`LibraryForm` já aceitam os dois parâmetros que travam os padrões da tabela acima —
`allowedElementKinds` e `showLayersPanel`:

```csharp
using LabelSharpDesigner.App;
using LabelSharpDesigner.App.Library;

private static readonly NewElementKind[] ElementosPermitidos =
    [NewElementKind.Text, NewElementKind.Line, NewElementKind.Barcode, NewElementKind.QrCode, NewElementKind.Image];

// Um único documento, sem a biblioteca inteira ao redor:
using var editor = new EditorForm(
    entry.Document,
    onSave: doc => labelRepository.Save(entry, doc),
    allowedElementKinds: ElementosPermitidos,
    showLayersPanel: false);
editor.ShowDialog(this);
```

`allowedElementKinds`/`showLayersPanel` nunca afetam uma etiqueta já existente com elementos fora
dessa lista (ex.: um `.label` desenhado antes dessa restrição existir) — eles só decidem o que o
menu "+ Adicionar" oferece daqui pra frente; abrir/editar/imprimir continua funcionando normalmente
para qualquer elemento que já esteja no documento.

## Passo 4 — Gerenciar as etiquetas criadas

Não precisa reconstruir nada — `LibraryForm` já é a tela completa de listar/criar/editar/duplicar/
excluir/exportar/imprimir, e repassa os mesmos dois parâmetros para todo `EditorForm` que abrir:

```csharp
using var library = new LibraryForm(
    labelRepository,
    allowedElementKinds: ElementosPermitidos,
    showLayersPanel: false);
library.ShowDialog(this);
```

`LibraryRepository.List()` devolve `IReadOnlyList<LibraryEntry>` (`Id`, `FilePath`, `Document`) — use
o `Id` como chave estável para qualquer configuração sua vinculada a uma etiqueta específica (ex.:
vínculo campo-da-entidade → variável-da-etiqueta, ver
[INTEGRATION.md §2.4](INTEGRATION.md#24-vinculando-os-dados-da-sua-entidade-às-variáveis-da-etiqueta)).

## Passo 5 — Imprimir (sempre PPLA raster)

`PrintDialogForm` já abre com **PPLA raster** e **Transferência térmica (ribbon)** pré-selecionados
— não é preciso passar nada extra, e essa não é uma preferência que o app "lembra" da última vez
(diferente de impressora/quantidade/darkness, que continuam sendo lembrados entre sessões):

```csharp
using var printDialog = new PrintDialogForm(entry.Document);
printDialog.ShowDialog(this);
```

Se você monta sua própria tela de impressão em lote em vez de reaproveitar `PrintDialogForm` (como o
[`PrintProductsForm`](src/LabelSharpDesigner.SampleApp/Printing/PrintProductsForm.cs) do exemplo, que
imprime vários produtos de uma vez), aplique o mesmo padrão no seu combo de formato:

```csharp
_formatCombo.SelectedIndex = (int)PrintDialogForm.PrintFormat.PplaRaster;
// ...
_transferTypeCombo.SelectedIndex = 1; // Transferência térmica (ribbon)
```

Para imprimir sem abrir nenhuma UI (ex.: impressão em lote disparada por código), monte o raster
direto e envie os bytes — ver
[INTEGRATION.md §2.6](INTEGRATION.md#26-resolvendo-em-lote-e-imprimindo):

```csharp
using LabelSharpDesigner.Layout;
using LabelSharpDesigner.Rendering.ArgoxPpla;
using LabelSharpDesigner.PrintTransport.Windows;

var fileiras = new LayoutEngine().ResolveBatch(entry.Document, registros);
var opcoes = new ArgoxRendererOptions { Darkness = 10, TransferType = ArgoxTransferType.ThermalTransfer };
foreach (var fileira in fileiras)
{
    var bytes = PplaRasterBuilder.Build(fileira, new ArgoxRasterOptions { Base = opcoes, FullResolution = true, MirrorHorizontal = true });
    new WindowsRawPrintTransport().Send(bytes, nomeDaImpressora);
}
```

## Checklist

- [ ] Todo `EditorForm`/`LibraryForm` aberto passa `allowedElementKinds: ElementosPermitidos` e
      `showLayersPanel: false`.
- [ ] Você reaproveita `LibraryForm`/`LibraryRepository` para gerenciar etiquetas — não reimplementa
      listar/criar/duplicar/excluir na mão.
- [ ] Toda impressão (via `PrintDialogForm` ou sua própria tela) começa em "PPLA raster" +
      "Transferência térmica (ribbon)", nunca em PDF/térmica direta.
- [ ] Você **não** resolve/rasteriza um documento na mão — sempre `LayoutEngine.Resolve`/
      `ResolveBatch` primeiro (ver [ARCHITECTURE.md](ARCHITECTURE.md)).
