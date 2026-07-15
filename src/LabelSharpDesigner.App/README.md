# LabelSharpDesigner.App

## O que é

O **aplicativo final** do LabelSharpDesigner — o composition root que junta praticamente todos os
outros projetos do repositório em telas prontas: biblioteca de etiquetas, editor visual, exportação,
impressão e configurações. Se você só quer usar o LabelSharpDesigner "como está", é este `.exe` que
você roda (ou referencia como biblioteca a partir da sua própria aplicação — veja
[INTEGRATION.md](../../INTEGRATION.md) seção 2.2).

## Dois modos de execução (`Program.cs`)

- **Modo biblioteca** (sem argumentos) — abre a `LibraryForm` sobre um `LibraryRepository`
  (`%APPDATA%\LabelSharpDesigner\Labels`): a tela de listar/criar/editar/duplicar/excluir/exportar/
  imprimir etiquetas `.label`.
- **Modo edição direta** (`--edit <caminho> [--readonly]`) — abre o `EditorForm` direto sobre um
  arquivo específico, ignorando a biblioteca inteira. É o modo usado quando o app é chamado como
  processo satélite por um sistema legado, via `LabelSharpDesigner.Legacy.Bridge`
  (`LaunchRequest.TryParse` é quem reconhece esses argumentos).

## Telas principais

- **`LibraryForm`** — tela completa da biblioteca de etiquetas.
- **`EditorForm`** — o editor visual de um documento (usa `LabelCanvasControl` do `UI.WinForms`,
  mais os painéis: camadas, propriedades, régua).
- **`ExportDialogForm`** — exportar a etiqueta atual como PNG ou PDF.
- **`PrintDialogForm`** — imprimir (PDF via driver do Windows, PPLA nativo ou PPLA raster para
  impressoras Argox), com **um único** campo de quantidade — nunca dois multiplicadores empilhados
  (veja a seção 5 do [ARCHITECTURE.md](../../ARCHITECTURE.md) para entender por quê).
- **`PageSettingsForm`** — editar `PageConfig` (tamanho, DPI, colunas etc.) do documento.
- **`VariablesForm`** — declarar/editar as `{{ variáveis }}` (`LabelDocument.Variables`) que a
  etiqueta usa, com validação do valor padrão contra o tipo declarado.
- **`LibraryForm`/`EditorForm`** usam **`NewElementFactory`** para o menu "+ Adicionar" de novos
  elementos no editor.
- **`RenderPreviewControl`** — o controle de pré-visualização ao vivo (usa o pipeline real de
  `Layout` + `Rendering.Canvas` — é a fonte de verdade sobre "o que vai sair impresso").

## Biblioteca de etiquetas (`Library/`)

- **`LibraryRepository`** — catálogo em disco de arquivos `.label`
  (`%APPDATA%\LabelSharpDesigner\Labels`): `Open()`/`OpenAt(dir)`, `List()`, `Create(pageConfig)`,
  `Save(entry, doc)`, `Duplicate(entry)`, `Delete(entry)`.
- **`LibraryEntry`** — um item da biblioteca (`Id`, `FilePath`, `Document`). O `Id` é a chave
  estável para vincular configurações próprias a uma etiqueta específica, mesmo se o nome de
  exibição mudar.
- **`LibraryCard`** — o cartão visual de uma etiqueta na grade da `LibraryForm`.
- **`AppThemeMode`** — tema claro/escuro/sistema (trocar de tema reabre a janela — repintar o tema
  ao vivo em controles WinForms já na tela não é confiável).

## Configurações do app (não do documento)

Preferências como impressora/formato/última quantidade usada (`PrintSettings`/
`PrintSettingsStore`) e tamanho/visibilidade dos painéis do editor (`EditorLayoutSettings`/
`EditorLayoutSettingsStore`) ficam em `%APPDATA%\LabelSharpDesigner\*.json` — são sobre *como você
usa o app*, nunca dentro do `LabelDocument` em si (grade/snap/guias, ao contrário, ficam dentro do
próprio documento porque fazem parte dele).

## Dependências

Referencia quase todos os projetos do repositório: `Core`, `Layout`, `Expressions`, `History`,
`Serialization`, `Barcode`, `Rendering.Canvas`, `Rendering.Png`, `Rendering.Pdf`,
`Rendering.ArgoxPpla`, `PrintTransport.Windows`, `UI.WinForms` e `Legacy.Bridge`. Alvo
`net9.0-windows10.0.19041.0` (`WinExe`): **exige Windows**.
