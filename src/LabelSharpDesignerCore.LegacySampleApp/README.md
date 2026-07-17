# LabelSharpDesignerCore.LegacySampleApp

## O que é

Um segundo aplicativo de **exemplo funcional**, irmão do [`LabelSharpDesignerCore.SampleApp`](../LabelSharpDesignerCore.SampleApp),
mas com um propósito diferente: mostrar uma integração onde a aplicação hospedeira roda em
**.NET Framework 4.x** (aqui, `net48`) e **gerencia seu próprio catálogo de etiquetas**, em vez de
reaproveitar as telas prontas do plugin (`LibraryForm`/`LibraryRepository`). O único pedaço do
LabelSharpDesignerCore que este app usa é o **editor visual**, aberto como processo satélite via
`LabelSharpDesignerCore.Legacy.Bridge`.

> **Nota**: `LabelSharpDesignerCore.App`/`UI.WinForms`/`PrintTransport.Windows` hoje multi-targetam
> `net48;net9.0-windows(...)` e podem ser referenciados diretamente por um host `net48`, exatamente
> como o [`SampleApp`](../LabelSharpDesignerCore.SampleApp) faz para `net9.0-windows` — ver
> [INTEGRATION.md](../../INTEGRATION.md) e [GUIA_RAPIDO_FRAMEWORK.md](../../GUIA_RAPIDO_FRAMEWORK.md).
> Esse é o caminho recomendado hoje. Este projeto continua existindo porque ainda ilustra dois
> padrões válidos por si só, independente de qualquer limitação de runtime: (1) processo satélite via
> `Legacy.Bridge`, útil quando você quer isolar o editor do processo hospedeiro (ver
> [`Legacy.Bridge/README.md`](../LabelSharpDesignerCore.Legacy.Bridge/README.md#quando-ele-ainda-é-necessário)
> para os cenários em que isso ainda faz sentido), e (2) gerenciar seu próprio catálogo de etiquetas
> em vez de reaproveitar `LibraryForm`/`LibraryRepository`.

## Em que difere do `SampleApp`

| | `SampleApp` (`net9.0-windows`) | `LegacySampleApp` (`net48`) |
|---|---|---|
| Tela "Etiquetas" | `LibraryForm` do próprio plugin | `Labels/LabelListForm`, própria — lista/cria/renomeia/duplica/exclui contra `Labels/LabelRepository` |
| Armazenamento das etiquetas | `%APPDATA%\LabelSharpDesignerCore\Labels` (do plugin) | `%APPDATA%\LabelSharpDesignerCore\LegacySampleApp\Labels` (deste app) |
| Abrir o editor visual | `new EditorForm(document, onSave)` in-process | `LegacyLauncher.Launch(...)` — processo satélite `LabelSharpDesignerCore.App.exe` |
| Transporte de impressão | `LabelSharpDesignerCore.PrintTransport.Windows` (referenciado direto) | `Printing/WindowsRawPrintTransport` e `WindowsPdfPrintTransport` — reimplementados aqui à mão, de quando aquele projeto era `net9.0-windows`-only; hoje ele também compila para `net48` e poderia ser referenciado direto (ver nota acima) |

## Peças principais

- **`Labels/LabelRepository`** — catálogo próprio de `.label`, um arquivo JSON por documento. Não
  tem nenhuma relação com o `LibraryRepository` do plugin.
- **`Labels/LabelListForm`** — a tela de "gerenciar etiquetas" **desta aplicação**: novo, editar,
  renomear, duplicar, excluir. "Editar" é o único botão que toca o plugin, chamando
  `EditorLauncherLocator` + `LegacyLauncher` para abrir o `.exe` satélite sobre um único arquivo.
- **`Labels/EditorLauncherSettingsForm`** — onde o caminho do `LabelSharpDesignerCore.App.exe` publicado
  é configurado (ver INTEGRATION.md §3.1); `EditorLauncherLocator.TryAutoDetect` só existe como
  conveniência de desenvolvimento, para este exemplo funcionar direto de dentro deste repositório.
- **`Printing/PrintProductsForm`** — mesma ideia de impressão em lote do `SampleApp` (vínculo
  dinâmico produto ↔ variável, `LayoutEngine.ResolveBatch`, PDF/PPLA), mas contra o catálogo próprio
  de etiquetas e com transporte de impressão reimplementado localmente para `net48`.

## Como rodar

Publique primeiro o satélite (uma vez só; veja INTEGRATION.md §3.1):

```powershell
dotnet publish src/LabelSharpDesignerCore.App -c Release -r win-x64 --self-contained false
```

Depois rode este app — na primeira vez que "Editar" for usado sem um caminho configurado, ele tenta
localizar automaticamente o `.exe` publicado acima (conveniência de desenvolvimento) ou pede para
configurar manualmente em "Configurações":

```powershell
dotnet run --project src/LabelSharpDesignerCore.LegacySampleApp
```

## Dependências

`Core`, `Serialization`, `Layout`, `Rendering.Canvas`, `Rendering.Pdf`, `Rendering.ArgoxPpla` e
`Legacy.Bridge` — todos `netstandard2.0;net9.0`. Deliberadamente **não** referencia
`LabelSharpDesignerCore.App`, `PrintTransport.Windows` nem `UI.WinForms`: não por limitação de
runtime (os três já compilam para `net48` também), mas porque `App` é exatamente a peça
(biblioteca/gerenciador de etiquetas) que este exemplo existe para não reaproveitar — ver a nota no
topo deste README. Alvo `net48`: qualquer .NET Framework 4.6.1+ instalado funciona para os projetos
referenciados, mas o `.csproj` deste app fixa `net48` por ser o que está instalado na máquina de
desenvolvimento.
