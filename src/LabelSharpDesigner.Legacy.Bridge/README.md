# LabelSharpDesigner.Legacy.Bridge

## O que é

Uma "ponte" bem fininha que permite uma aplicação legada em **ASP.NET Framework 4.x** (ou qualquer
coisa que não possa referenciar `net9.0-windows` diretamente) abrir o editor de etiquetas
(`LabelSharpDesigner.App`) mesmo sem conseguir referenciá-lo como biblioteca.

## O problema que ele resolve

`LabelSharpDesigner.App` é um app WinForms `net9.0-windows` — um sistema em .NET Framework 4.x
clássico não consegue referenciar isso diretamente (são runtimes incompatíveis). A solução é rodar o
`App` como um **processo separado** (um `.exe` publicado à parte) e conversar com ele só por
argumentos de linha de comando e código de saída do processo. Este projeto é o "dicionário comum"
dos dois lados dessa conversa — ele mesmo compila para `netstandard2.0`, então tanto o app legado
quanto o `App` moderno conseguem referenciá-lo.

## Como a conversa funciona

1. O sistema legado referencia este projeto e usa `LegacyLauncher` para abrir o
   `LabelSharpDesigner.App.exe` (publicado antecipadamente) passando `--edit <caminho> [--readonly]`.
2. O `App` detecta esses argumentos e entra em modo de edição direta: abre o editor só naquele
   arquivo `.label`, sem mostrar a biblioteca inteira.
3. Quando o usuário fecha o editor, o processo termina com um código de saída que representa o
   resultado (`LaunchOutcome`), e o `LegacyLauncher` traduz esse código de volta para o sistema
   legado.

## Peças principais

- **`LegacyLauncher`** — classe principal (não estática — instancie com o caminho do `.exe`
  publicado). O método `Launch(LaunchRequest)` inicia o processo e **bloqueia** até o usuário fechar
  a janela do editor.
- **`LaunchRequest`** — o que enviar: `FilePath` (caminho do `.label` a editar) e `ReadOnly`
  (abrir só para visualização, sem permitir salvar).
- **`LaunchResult`** / **`LaunchOutcome`** — o que volta depois que o editor fecha:
  - `Saved` — o arquivo foi sobrescrito com as alterações.
  - `Cancelled` — fechou sem salvar (ou a sessão era `ReadOnly`).
  - `Error` — não deu para abrir o arquivo (corrompido, formato inválido).

## Como usar (lado do sistema legado)

```csharp
using LabelSharpDesigner.Legacy.Bridge;

var launcher = new LegacyLauncher(@"C:\LabelSharpDesigner\LabelSharpDesigner.App.exe");
var resultado = launcher.Launch(new LaunchRequest { FilePath = caminhoDoLabel, ReadOnly = false });

switch (resultado.Outcome)
{
    case LaunchOutcome.Saved: /* recarregue a miniatura */ break;
    case LaunchOutcome.Cancelled: /* nada mudou */ break;
    case LaunchOutcome.Error: /* avise o usuário */ break;
}
```

**Importante**: isso só funciona quando o código chamador roda numa sessão de desktop interativa da
máquina do usuário (ex.: IIS Express local, uma estação rodando seu próprio servidor). Não funciona
num IIS de produção atendendo clientes remotos pela internet — veja a seção 3 do
[INTEGRATION.md](../../INTEGRATION.md) para o passo a passo completo, incluindo os casos em que este
caminho não se aplica.

## Dependências

Nenhuma — nem de `LabelSharpDesigner.Core`. É deliberadamente isolado, só BCL (biblioteca padrão do
.NET), para não trazer nenhuma dependência transitiva para dentro de um projeto .NET Framework
legado. Multi-targeta `netstandard2.0;net9.0`, compatível com .NET Framework 4.6.1+.
