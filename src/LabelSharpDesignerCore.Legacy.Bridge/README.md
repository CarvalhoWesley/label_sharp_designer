# LabelSharpDesignerCore.Legacy.Bridge

## O que é

Uma "ponte" bem fininha que permite abrir o editor de etiquetas (`LabelSharpDesignerCore.App`) como
**processo separado**, conversando só por argumentos de linha de comando e código de saída do
processo, em vez de referenciá-lo como biblioteca no mesmo processo.

## Quando ele ainda é necessário

`LabelSharpDesignerCore.App` multi-targeta `net48;net9.0-windows(...)` — qualquer host, .NET moderno
ou .NET Framework 4.6.1+, já consegue referenciá-lo **diretamente** (ver
[INTEGRATION.md](../../INTEGRATION.md) e [ARCHITECTURE.md §7](../../ARCHITECTURE.md#7-integração-com-hosts-net-framework-4x)).
Isso deixou de ser um problema de incompatibilidade de runtime — não existe mais essa barreira.

Este projeto continua útil só quando abrir o editor **no mesmo processo** não é uma opção:

- **Host web atrás de IIS de produção** atendendo clientes remotos pela internet: o worker process
  roda na Session 0 (isolada de qualquer desktop desde o Vista/Server 2008), então nenhuma UI
  WinForms consegue aparecer para o usuário do navegador dali, referenciando `App` direto ou não. Um
  processo satélite ainda funciona quando "o servidor" é, na prática, o computador da pessoa que vai
  desenhar a etiqueta (IIS Express local, uma estação rodando seu próprio IIS local como o usuário
  logado).
- **Isolamento de processo por outros motivos** — ex.: um crash do editor não deve derrubar o host.

Fora desses cenários, prefira referenciar `LabelSharpDesignerCore.App` direto — é mais simples e não
depende de publicar/versionar um `.exe` satélite à parte. Este projeto compila para `netstandard2.0`
justamente para continuar utilizável dos dois lados dessa conversa quando ela ainda fizer sentido.

## Como a conversa funciona

1. O sistema legado referencia este projeto e usa `LegacyLauncher` para abrir o
   `LabelSharpDesignerCore.App.exe` (publicado antecipadamente) passando `--edit <caminho> [--readonly]`.
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
using LabelSharpDesignerCore.Legacy.Bridge;

var launcher = new LegacyLauncher(@"C:\LabelSharpDesignerCore\LabelSharpDesignerCore.App.exe");
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
num IIS de produção atendendo clientes remotos pela internet — veja a nota no fim da
[seção 1 do INTEGRATION.md](../../INTEGRATION.md#1-como-referenciar-os-projetos) para mais contexto.

## Dependências

Nenhuma — nem de `LabelSharpDesignerCore.Core`. É deliberadamente isolado, só BCL (biblioteca padrão do
.NET), para não trazer nenhuma dependência transitiva para dentro de um projeto .NET Framework
legado. Multi-targeta `netstandard2.0;net9.0`, compatível com .NET Framework 4.6.1+.
