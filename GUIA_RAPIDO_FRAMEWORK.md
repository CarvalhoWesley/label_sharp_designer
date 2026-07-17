# Guia rápido — editor de etiquetas para aplicação .NET Framework 4.x

Versão enxuta de [INTEGRATION.md §1](INTEGRATION.md#1-como-referenciar-os-projetos)
com só o essencial: **implementar o editor**, **gerenciar as etiquetas criadas** e **imprimir**, já
com as três decisões de produto abaixo fixadas em código (não são opção do usuário final). Para
detalhes/casos de borda (vínculo de campos dinâmico, mala direta, preferências de app etc.) volte
para [INTEGRATION.md](INTEGRATION.md) e [USAGE.md](USAGE.md).

`UI.WinForms`, `App` e `PrintTransport.Windows` multi-targetam `net48;net9.0-windows(...)` — a API é
exatamente a mesma usada por uma aplicação .NET moderna (ver
[GUIA_RAPIDO_MODERNO.md](GUIA_RAPIDO_MODERNO.md)), só a `TargetFramework` do seu projeto muda. Não há
processo satélite nem ponte de nenhum tipo: você referencia os projetos e chama `EditorForm`/
`LibraryForm` no seu próprio processo, exatamente como qualquer outra lib WinForms.

**Única diferença de comportamento**: o tema escuro (`Application.SetColorMode`) é uma API exclusiva
do WinForms .NET 9+ — no .NET Framework 4.x o editor sempre roda no tema claro clássico do Windows.

**Isso vale para aplicações desktop** (WinForms/WPF, sessão de desktop interativa). Se a sua
aplicação é web (ASP.NET Framework atrás de IIS de produção atendendo clientes remotos pela
internet), este guia não serve como está — veja a nota sobre Session 0 em
[INTEGRATION.md §1](INTEGRATION.md#1-como-referenciar-os-projetos).

## Padrões fixados neste guia

| Ponto | Decisão |
|---|---|
| Elementos disponíveis que o editor oferece | Só **Texto, Linha, Código de barras, QR Code, Imagem** — o resto (retângulo, elipse, tabela, data/hora...) não aparece |
| Painel de camadas | Nunca exibido |
| Formato de impressão | Diálogo sempre abre em **PPLA raster** com **Transferência térmica (ribbon)** já selecionados — o usuário ainda pode trocar na hora, mas nunca é PDF/térmica direta por padrão |

## Passo 1 — Referenciar os projetos

Seu projeto precisa ser `net48` (ou qualquer .NET Framework 4.6.1+) com `UseWindowsForms=true`.

- **Projeto SDK-style** (`<Project Sdk="Microsoft.NET.Sdk">` mirando `net48`, o formato moderno de
  `.csproj` — é o caso, por exemplo, do
  [`LabelSharpDesignerCore.LegacySampleApp.csproj`](src/LabelSharpDesignerCore.LegacySampleApp/LabelSharpDesignerCore.LegacySampleApp.csproj)
  deste repositório): `ProjectReference` para `LabelSharpDesignerCore.App` funciona direto, igual ao
  `SampleApp` faz para net9 — o MSBuild resolve sozinho a perna `net48` de cada dependência.
- **Projeto clássico** (`packages.config`, sem `Sdk=` no `.csproj`): publique o `App` e referencie os
  `.dll` compilados:

  ```powershell
  dotnet publish src/LabelSharpDesignerCore.App -c Release -f net48
  ```

  Copie a pasta de saída inteira (`LabelSharpDesignerCore.App.exe` + todas as DLLs ao lado — o `App`
  traz várias dependências transitivas: `SkiaSharp`, `PdfSharp`, `ZXing.Net`, `System.Text.Json`) para
  um caminho fixo acessível pelo seu projeto, e referencie cada `.dll` que for usar:

  ```xml
  <Reference Include="LabelSharpDesignerCore.App">
    <HintPath>C:\LabelSharpDesignerCore\LabelSharpDesignerCore.App.dll</HintPath>
  </Reference>
  <!-- + Core, Serialization, Layout, Rendering.*, PrintTransport.Windows, UI.WinForms conforme a Passo 1 do INTEGRATION.md -->
  ```

## Passo 2 — Nova etiqueta (documento em branco)

Uma etiqueta nova é só um `LabelDocument` em branco, no mesmo padrão de
[INTEGRATION.md §3](INTEGRATION.md#3-o-que-você-precisa-saber-do-labeldocument):

```csharp
using LabelSharpDesignerCore.App.Library;

var labelRepository = LibraryRepository.Open(); // %APPDATA%\LabelSharpDesignerCore\Labels
var entry = labelRepository.Create(); // documento em branco, 100×60mm/203dpi por padrão
```

## Passo 3 — Implementar o editor (elementos restritos, sem painel de camadas)

`EditorForm`/`LibraryForm` já aceitam os dois parâmetros que travam os padrões da tabela acima —
`allowedElementKinds` e `showLayersPanel`:

```csharp
using LabelSharpDesignerCore.App;
using LabelSharpDesignerCore.App.Library;

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
[INTEGRATION.md §4](INTEGRATION.md#4-vinculando-os-dados-da-sua-entidade-às-variáveis-da-etiqueta)).

## Passo 5 — Imprimir (sempre PPLA raster)

`PrintDialogForm` já abre com **PPLA raster** e **Transferência térmica (ribbon)** pré-selecionados
— não é preciso passar nada extra:

```csharp
using var printDialog = new PrintDialogForm(entry.Document);
printDialog.ShowDialog(this);
```

Se você monta sua própria tela de impressão em lote em vez de reaproveitar `PrintDialogForm` (como o
[`PrintProductsForm`](src/LabelSharpDesignerCore.SampleApp/Printing/PrintProductsForm.cs) do exemplo
net9, que imprime vários produtos de uma vez), aplique o mesmo padrão no seu combo de formato:

```csharp
_formatCombo.SelectedIndex = (int)PrintDialogForm.PrintFormat.PplaRaster;
// ...
_transferTypeCombo.SelectedIndex = 1; // Transferência térmica (ribbon)
```

Para imprimir sem abrir nenhuma UI (ex.: impressão em lote disparada por código), monte o raster
direto e envie os bytes — `PrintTransport.Windows` funciona igual em `net48`:

```csharp
using LabelSharpDesignerCore.Layout;
using LabelSharpDesignerCore.Rendering.ArgoxPpla;
using LabelSharpDesignerCore.PrintTransport.Windows;

var fileiras = new LayoutEngine().ResolveBatch(entry.Document, registros);
var opcoes = new ArgoxRendererOptions { Darkness = 10, TransferType = ArgoxTransferType.ThermalTransfer };
foreach (var fileira in fileiras)
{
    var bytes = PplaRasterBuilder.Build(fileira, new ArgoxRasterOptions { Base = opcoes, FullResolution = true, MirrorHorizontal = true });
    new WindowsRawPrintTransport().Send(bytes, nomeDaImpressora);
}
```

## Checklist

- [ ] Seu projeto é `net48` (ou 4.6.1+) com `UseWindowsForms=true`, referenciando `App` (e o que mais
      precisar) direto — sem processo satélite.
- [ ] Todo `EditorForm`/`LibraryForm` aberto passa `allowedElementKinds: ElementosPermitidos` e
      `showLayersPanel: false`.
- [ ] Você reaproveita `LibraryForm`/`LibraryRepository` para gerenciar etiquetas — não reimplementa
      listar/criar/duplicar/excluir na mão.
- [ ] Toda impressão (via `PrintDialogForm` ou sua própria tela) começa em "PPLA raster" +
      "Transferência térmica (ribbon)", nunca em PDF/térmica direta.
- [ ] Você **não** resolve/rasteriza um documento na mão — sempre `LayoutEngine.Resolve`/
      `ResolveBatch` primeiro (ver [ARCHITECTURE.md](ARCHITECTURE.md)).
