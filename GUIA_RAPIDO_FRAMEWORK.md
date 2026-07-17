# Guia rápido — editor de etiquetas para aplicação .NET Framework 4.x

Versão enxuta de [INTEGRATION.md §3](INTEGRATION.md#3-caminho-b--aplicação-legada-aspnet-framework--net-framework-4x)
com só o essencial: **implementar o editor**, **gerenciar as etiquetas criadas** e **imprimir**, já
com as três decisões de produto abaixo fixadas em código (não são opção do usuário final). 
Existe um exemplo funcionando de ponta a ponta em
[`src/LabelSharpDesignerCore.LegacySampleApp`](src/LabelSharpDesignerCore.LegacySampleApp) — todo trecho
abaixo foi tirado (ou simplificado) direto dele.

## Padrões fixados neste guia

| Ponto | Decisão |
|---|---|
| Elementos disponiveis que editor oferece | Só **Texto, Linha, Código de barras, QR Code, Imagem** — o resto (retângulo, elipse, tabela, data/hora...) não aparece |
| Formato de impressão | Diálogo sempre abre em **PPLA raster** com **Transferência térmica (ribbon)** já selecionados — o usuário ainda pode trocar na hora, mas nunca é PDF/térmica direta por padrão |

## Antes de começar: onde esse código roda

O editor abre como processo separado (`Process.Start`) — só mostra uma janela de verdade
numa **sessão de desktop interativa** (a própria máquina do usuário, IIS/IIS Express local). Não
funciona atrás de um IIS de produção atendendo clientes remotos (Session 0). Ver
[INTEGRATION.md §3.0](INTEGRATION.md#30-antes-de-começar-onde-esse-código-vai-rodar) se esse for o
seu caso.

## Passo 1 — Publicar o editor e referenciar a ponte

```powershell
dotnet publish src/LabelSharpDesignerCore.App -c Release -r win-x64 --self-contained false
```

Copie a pasta de saída para um caminho fixo (ex. `C:\LabelSharpDesignerCore\LabelSharpDesignerCore.App.exe`)
e referencie só a DLL `netstandard2.0` do `Legacy.Bridge` no seu projeto clássico:

```xml
<Reference Include="LabelSharpDesignerCore.Legacy.Bridge">
  <HintPath>C:\LabelSharpDesignerCore\LabelSharpDesignerCore.Legacy.Bridge.dll</HintPath>
</Reference>
```

Requer .NET Framework 4.6.1+ (4.7.2+ recomendado); sem NuGet, sem `bindingRedirect`.

## Passo 2 — Nova etiqueta (documento em branco)

Uma etiqueta nova é só um `LabelDocument` em branco salvo como `.label` antes de chamar o editor —
o mesmo padrão de [USAGE.md §2](USAGE.md#2-modelo-de-documento-core):

```csharp
using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Serialization;

var documento = new LabelDocument
{
    Name = "Nova etiqueta",
    Page = new PageConfig { WidthMm = 100, HeightMm = 60, Dpi = 203 },
    Layers = [new LabelLayer { Id = "layer-1", Name = "Base", Order = 0 }],
};
File.WriteAllText(caminhoDoLabel, LabelDocumentCodec.Save(documento));
```

`LabelSharpDesignerCore.Core`/`Serialization` são `netstandard2.0` — referenciáveis direto do seu projeto
Framework 4.x, sem precisar do satélite para isso.

## Passo 3 — Implementar o editor (elementos restritos, sem painel de camadas)

Chame `LegacyLauncher` passando os dois campos que travam os padrões da tabela acima:

```csharp
using LabelSharpDesignerCore.Legacy.Bridge;

private static readonly string[] ElementosPermitidos = ["Text", "Line", "Barcode", "QrCode", "Image"];

var launcher = new LegacyLauncher(@"C:\LabelSharpDesignerCore\LabelSharpDesignerCore.App.exe");
var request = new LaunchRequest
{
    FilePath = caminhoDoLabel,
    AllowedElementKinds = ElementosPermitidos,
    ShowLayersPanel = false,
};
LaunchResult result = launcher.Launch(request); // bloqueia até fechar a janela do editor

switch (result.Outcome)
{
    case LaunchOutcome.Saved:     /* arquivo sobrescrito — recarregue a miniatura */ break;
    case LaunchOutcome.Cancelled: /* fechou sem salvar — nada mudou */ break;
    case LaunchOutcome.Error:     /* arquivo corrompido/inválido — avise o usuário */ break;
}
```

Os nomes em `AllowedElementKinds` são texto puro (`"Text"`, `"Barcode"`, `"QrCode"`, `"Line"`,
`"Image"` — precisam bater com `NewElementKind.ToString()` do lado do plugin) porque este projeto
não pode referenciar o enum de verdade, que vive no `App` (`net9.0-windows`, inalcançável a partir de
`net48`). Um nome que o satélite não reconhecer é só ignorado, nunca derruba o launch inteiro.

> Se preferir deixar essa restrição configurável por um administrador em vez de fixa no código, o
> `LegacySampleApp` já traz uma tela pronta pra isso —
> [`EditorLauncherSettingsForm`](src/LabelSharpDesignerCore.LegacySampleApp/Labels/EditorLauncherSettingsForm.cs)
> — mas para os padrões fixos deste guia o array hardcoded acima é o caminho mais simples.

## Passo 4 — Gerenciar as etiquetas criadas

O Caminho B não usa a biblioteca do plugin (`LibraryForm`/`LibraryRepository` são `net9.0-windows`,
inalcançáveis daqui) — listar/criar/renomear/duplicar/excluir é responsabilidade da sua própria
aplicação, indexado por qualquer chave que fizer sentido no seu domínio (um `LabelId` na tabela de
produtos, por exemplo). O padrão pronto, um JSON por etiqueta em disco:

```csharp
using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Serialization;

public sealed class LabelRepository
{
    // Open()/OpenAt(dir), List(), Create(name, page), Rename(entry, novoNome),
    // Duplicate(entry), Delete(entry), Reload(entry) — implementação completa em
    // src/LabelSharpDesignerCore.LegacySampleApp/Labels/LabelRepository.cs, copiável como está.
}
```

A tela de listagem (grid com "+ Nova etiqueta" / "Editar" / "Renomear" / "Duplicar" / "Excluir") já
existe pronta em
[`LabelListForm`](src/LabelSharpDesignerCore.LegacySampleApp/Labels/LabelListForm.cs) — `+ Nova etiqueta`
cria o documento em branco (Passo 2) e já abre o editor em seguida (Passo 3); os outros botões só
mexem no arquivo `.label` direto via `LabelDocumentCodec`, sem precisar do satélite.

## Passo 5 — Imprimir (sempre PPLA raster)

O satélite só edita — imprimir é sempre código da sua própria aplicação, e dá pra fazer 100% em
`netstandard2.0` (sem precisar do `net9.0-windows`), porque `Core`/`Layout`/`Rendering.ArgoxPpla`/
`Rendering.Pdf` multi-targetam `netstandard2.0;net9.0`:

```csharp
using LabelSharpDesignerCore.Layout;
using LabelSharpDesignerCore.Rendering.ArgoxPpla;

var documento = LabelDocumentCodec.Load(File.ReadAllText(caminhoDoLabel));
var registros = /* um IReadOnlyDictionary<string, object?> por etiqueta física */;
var fileiras = new LayoutEngine().ResolveBatch(documento, registros);

// Padrão fixo: raster + ribbon — nunca PPLA nativo/térmica direta por padrão.
var opcoes = new ArgoxRendererOptions { Darkness = 10, TransferType = ArgoxTransferType.ThermalTransfer };
foreach (var fileira in fileiras)
{
    var bytes = PplaRasterBuilder.Build(fileira, new ArgoxRasterOptions { Base = opcoes, FullResolution = true, MirrorHorizontal = true });
    // envio dos bytes crus — ver abaixo
}
```

`LabelSharpDesignerCore.PrintTransport.Windows` é `net9.0-windows`-only, então não dá pra referenciar do
Framework 4.x — o envio dos bytes crus pro spooler precisa da sua própria versão P/Invoke de
`winspool.drv`. Já existe pronta e testada em
[`RawPrinterHelper`](src/LabelSharpDesignerCore.LegacySampleApp/Printing/RawPrinterHelper.cs) +
[`WindowsRawPrintTransport`](src/LabelSharpDesignerCore.LegacySampleApp/Printing/WindowsRawPrintTransport.cs)
(equivalente clássico do `WindowsRawPrintTransport` do plugin) — copiável como está:

```csharp
new WindowsRawPrintTransport().Send(bytes, nomeDaImpressora); // null = impressora padrão
```

Se a sua tela de impressão oferece um combo "Formato" (PDF/PPLA nativo/PPLA raster) como o
[`PrintProductsForm`](src/LabelSharpDesignerCore.LegacySampleApp/Printing/PrintProductsForm.cs) do
exemplo, deixe-o sempre abrir com **PPLA raster** + **Transferência térmica (ribbon)** já
selecionados (`_formatCombo.SelectedIndex = (int)PrintFormat.PplaRaster` /
`_transferTypeCombo.SelectedIndex = 1`) — é exatamente o que o exemplo faz.

## Checklist

- [ ] O código que chama `LegacyLauncher.Launch` roda numa sessão de desktop interativa (não IIS de
      produção remoto).
- [ ] `LaunchRequest.AllowedElementKinds` = `["Text", "Line", "Barcode", "QrCode", "Image"]` e
      `ShowLayersPanel = false` em toda chamada ao editor.
- [ ] Gerenciamento de etiquetas (listar/criar/renomear/duplicar/excluir) é feito pela sua própria
      aplicação — nunca tentando referenciar `LibraryForm`/`LibraryRepository` do plugin.
- [ ] A impressão nunca abre em PDF/PPLA nativo por padrão — o combo de formato começa sempre em
      "PPLA raster" com "Transferência térmica (ribbon)".
- [ ] Você trata os três `LaunchOutcome` (`Saved`/`Cancelled`/`Error`).
