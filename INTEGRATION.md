# Guia de integração — como usar o LabelSharpDesignerCore na sua aplicação

Este guia é para quem vai **consumir** o LabelSharpDesignerCore a partir de outra aplicação — não para
quem vai mexer no editor em si. Para entender como o pipeline interno funciona (por que só o
`LayoutEngine` resolve layout, como Canvas/PDF/PPLA nunca divergem, etc.), veja
[ARCHITECTURE.md](ARCHITECTURE.md). Aqui o foco é: "eu tenho uma aplicação com produtos/pedidos/
ativos/o-que-for e quero cadastrar e imprimir etiquetas para eles — o que eu preciso fazer?". Para a
referência objetiva de cada elemento/API disponível (sem o passo a passo de montar uma tela do zero),
veja [USAGE.md](USAGE.md).

Se você quer um passo a passo curto (implementar o editor, gerenciar etiquetas, imprimir) já com
decisões de produto fixadas (elementos restritos, painel de camadas oculto, impressão sempre em
raster), veja [GUIA_RAPIDO_MODERNO.md](GUIA_RAPIDO_MODERNO.md) (Caminho A) ou
[GUIA_RAPIDO_FRAMEWORK.md](GUIA_RAPIDO_FRAMEWORK.md) (Caminho B) em vez deste guia completo.

Existe um exemplo completo e funcional no próprio repositório —
[`src/LabelSharpDesignerCore.SampleApp`](src/LabelSharpDesignerCore.SampleApp) — que implementa
exatamente o que este guia descreve, com um catálogo de produtos fictício no lugar da sua
entidade de domínio. Todo trecho de código abaixo foi tirado (ou simplificado) direto dele; quando
tiver dúvida, abra o projeto e compare.

## 1. Escolha o seu caminho de integração

| Sua aplicação é... | Caminho | Como |
|---|---|---|
| .NET moderno (net8+/net9, qualquer UI) rodando no mesmo processo/host Windows | **Caminho A — referência direta** | `ProjectReference` (ou pacote, se você empacotar as libs) para as libs que precisar |
| ASP.NET Framework 4.x (ou qualquer coisa que não possa referenciar `net9.0-windows` diretamente) | **Caminho B — processo satélite** | `LabelSharpDesignerCore.Legacy.Bridge` + `LegacyLauncher` |

A diferença só importa para **abrir o editor visual**. Gerar/imprimir etiquetas a partir dos seus
próprios dados (seção 4 em diante) é sempre feito em processo — se você está no Caminho B, essa
parte roda dentro do `LabelSharpDesignerCore.App.exe` satélite, não na sua aplicação legada.

## 2. Caminho A — referência direta (aplicação .NET moderna)

### 2.1 Quais projetos referenciar

Todo projeto de domínio/lógica multi-targeta `netstandard2.0;net9.0`; só a UI (WinForms) e o
`PrintTransport.Windows` exigem `net9.0-windows`. Referencie apenas o que for usar:

| Você precisa de... | Referencie |
|---|---|
| Modelo de documento (`LabelDocument`, `PageConfig`, ...) | `LabelSharpDesignerCore.Core` |
| Carregar/salvar arquivos `.label` | `LabelSharpDesignerCore.Serialization` |
| Resolver um documento para impressão/preview (`LayoutEngine`) | `LabelSharpDesignerCore.Layout` |
| Renderizar (preview ao vivo, PNG, base do PPLA raster) | `LabelSharpDesignerCore.Rendering.Canvas` |
| Exportar/imprimir em PDF | `LabelSharpDesignerCore.Rendering.Pdf` |
| Exportar/imprimir em PPLA (impressoras térmicas Argox) | `LabelSharpDesignerCore.Rendering.ArgoxPpla` |
| Enviar bytes para uma impressora Windows | `LabelSharpDesignerCore.PrintTransport.Windows` |
| **Reaproveitar as telas prontas de biblioteca/editor de etiquetas** (`LibraryForm`, `EditorForm`, `LibraryRepository`) | `LabelSharpDesignerCore.App` |

Na prática, se você vai oferecer "cadastrar e imprimir etiquetas" como o `SampleApp` faz, referencie
todos eles — veja
[`LabelSharpDesignerCore.SampleApp.csproj`](src/LabelSharpDesignerCore.SampleApp/LabelSharpDesignerCore.SampleApp.csproj)
como modelo pronto de `ItemGroup`. Seu projeto precisa ser `net9.0-windows10.0.19041.0` com
`UseWindowsForms=true` para poder referenciar `App`/`Rendering.Canvas`/`PrintTransport.Windows`.

> Sim, dá para referenciar o projeto `LabelSharpDesignerCore.App` (que é `OutputType=WinExe`) como
> qualquer outra lib — o `Main` dele só roda se você chamar; sua aplicação continua dona do próprio
> `Program.Main`.

### 2.2 Tela de "gerenciar etiquetas" — não reimplemente, reaproveite

`LabelSharpDesignerCore.App.Library.LibraryForm` já é a tela completa de listar/criar/editar/duplicar/
excluir/exportar/imprimir etiquetas `.label`, com undo/redo e tudo mais do editor por trás. Não
existe motivo para reconstruir isso — basta abrir a tela com um `LibraryRepository`:

```csharp
using LabelSharpDesignerCore.App.Library;

var labelRepository = LibraryRepository.Open(); // %APPDATA%\LabelSharpDesignerCore\Labels
new LibraryForm(labelRepository).ShowDialog(this);
```

Se quiser editar um único documento sem a biblioteca inteira ao redor (ex.: um botão "editar
etiqueta deste pedido" na sua própria tela), use o `EditorForm` diretamente:

```csharp
using var editor = new EditorForm(document, doc => labelRepository.Save(entry, doc));
editor.ShowDialog(this);
```

`LibraryRepository.List()` devolve `IReadOnlyList<LibraryEntry>` (`Id`, `FilePath`, `Document`) — é
esse `Id` que você vai usar como chave estável para qualquer configuração sua vinculada a uma
etiqueta específica (seção 4.2).

### 2.3 O que você precisa saber do `LabelDocument`

Você não precisa entender o pipeline inteiro — só estes três pontos, para o que vem a seguir:

- **`LabelDocument.Variables`** (`IReadOnlyList<LabelVariable>`): as variáveis `{{ }}` que a
  etiqueta declara (`Name`, `Type`, `DefaultValue`). Quem decide os nomes é a pessoa que desenhou a
  etiqueta no editor — a sua aplicação não controla isso, só **preenche** os valores na hora de
  imprimir (seção 4).
- **`LabelDocument.Page.Columns`**: quantas etiquetas cabem lado a lado numa fileira física (rolo
  multi-coluna). É uma propriedade do *documento*, não da sua impressão — trate como sugestão
  inicial, não como verdade absoluta (veja 4.4).
- **Tudo é imutável** (`record`, use `with` pra alterar): `document.Page.Columns` não muda com um
  setter, você cria uma cópia: `document with { Page = document.Page with { Columns = 2 } }`.

### 2.4 Vinculando os dados da sua entidade às variáveis da etiqueta

Este é o ponto central de qualquer integração: a etiqueta só sabe que existe uma variável chamada,
por exemplo, `preco` — ela não sabe nada sobre a sua classe `Product`/`Order`/`Asset`. É sua
aplicação que faz essa ponte, e o jeito recomendado (implementado no `SampleApp`) é deixar o
**usuário configurar esse vínculo**, em vez de cravar nomes de variável no código:

1. **Um enum com as origens possíveis** — os campos da sua entidade que podem alimentar uma
   variável, mais uma opção "usar o valor padrão da própria etiqueta":

   ```csharp
   public enum ProductFieldSource { LabelDefault, Description, Price, Barcode }
   ```

2. **Uma tela de vínculo, montada dinamicamente a partir de `document.Variables`** — uma linha por
   variável que a etiqueta *atualmente* declara, cada uma com um combo escolhendo a origem. Veja
   [`VariableMappingForm`](src/LabelSharpDesignerCore.SampleApp/Printing/VariableMappingForm.cs)
   na íntegra; o miolo é só isto:

   ```csharp
   foreach (var variable in document.Variables)
   {
       var combo = new ComboBox { /* ... */ };
       combo.Items.AddRange(["Valor padrão da etiqueta", "Descrição", "Preço", "Código de barras"]);
       combo.SelectedIndex = current.TryGetValue(variable.Name, out var source) ? (int)source : 0;
       // guarde (variable.Name, combo) para ler de volta no Salvar
   }
   ```

3. **Persista por etiqueta**, indexando pelo `LibraryEntry.Id` (nunca pelo nome de exibição — esse
   o usuário pode renomear a qualquer momento):

   ```csharp
   // %APPDATA%\<SuaAplicação>\variable-field-mappings.json
   Dictionary<string /* labelId */, Dictionary<string /* variableName */, ProductFieldSource>>
   ```

   Veja
   [`VariableFieldMappingStore`](src/LabelSharpDesignerCore.SampleApp/Printing/VariableFieldMappingStore.cs)
   — carregar/salvar com `try/catch` silencioso (arquivo ausente ou corrompido nunca deve travar a
   tela), igual ao resto das preferências de app deste projeto.

4. **Na hora de montar o registro para impressão**, resolva cada variável pelo vínculo salvo,
   caindo no `DefaultValue` da própria variável quando não há vínculo configurado:

   ```csharp
   var record = new Dictionary<string, object?>();
   foreach (var variable in document.Variables)
   {
       var source = mapping.TryGetValue(variable.Name, out var v) ? v : ProductFieldSource.LabelDefault;
       record[variable.Name] = source switch
       {
           ProductFieldSource.Description => product.Description,
           ProductFieldSource.Price       => (double)product.Price,
           ProductFieldSource.Barcode     => product.Barcode,
           _                              => variable.DefaultValue,
       };
   }
   ```

Isso é o que torna a integração **dinâmica**: funciona com qualquer etiqueta, qualquer conjunto de
variáveis, sem você precisar saber de antemão como a pessoa que desenhou a etiqueta nomeou as
coisas — e o usuário final pode reconfigurar o vínculo a qualquer momento pela própria UI, sem
precisar de você (desenvolvedor) mudar uma linha de código.

> Uma sugestão automática por *alias* (ex.: uma variável chamada `preco`/`price`/`valor` sugere
> `ProductFieldSource.Price` a primeira vez que a tela de vínculo abre para aquela etiqueta) é um
> bônus de UX opcional — só preencha o valor inicial do combo, nunca sobrescreva um vínculo que o
> usuário já salvou. Veja `GuessDefaultSource` no `PrintProductsForm` do SampleApp.

### 2.5 Preview

Reaproveite `LabelCanvasRenderer` (o mesmo desenho usado no export/impressão de verdade — nunca
diverge) dentro de um `SKControl`:

```csharp
using LabelSharpDesignerCore.Rendering.Canvas;
using SkiaSharp.Views.Desktop;

// Copie LabelPreviewControl.cs do SampleApp — é internal no assembly App, então não dá
// pra referenciar de fora; a classe tem ~35 linhas, é só desenhar `ResolvedDocument? Document`
// centralizado/escalado dentro de OnPaintSurface.
_preview.Document = layoutEngine.ResolveBatch(document, records).FirstOrDefault();
_preview.Invalidate();
```

### 2.6 Resolvendo em lote e imprimindo

Sua tela de impressão sempre trabalha em modo lote — mesmo para "uma etiqueta de cada produto",
porque no fim das contas você está imprimindo N registros, um por etiqueta física:

```csharp
using LabelSharpDesignerCore.Layout;

var layoutEngine = new LayoutEngine();
var document = entry.Document with { Page = entry.Document.Page with { Columns = colunasEscolhidas } };
var records = /* um IReadOnlyDictionary<string, object?> por etiqueta física, ver 2.4 */;
var rows = layoutEngine.ResolveBatch(document, records); // uma ResolvedDocument por fileira física
```

Depois, mande cada fileira pra saída que fizer sentido:

```csharp
// PDF — funciona com qualquer impressora instalada
var bytes = PdfExporter.ExportBatch(rows);
new WindowsPdfPrintTransport { Copies = 1 }.Send(bytes, printerName); // null = impressora padrão

// PPLA nativo/raster — impressoras térmicas Argox
foreach (var row in rows)
{
    var bytes = PplaCommandBuilder.Build(row, new ArgoxRendererOptions { Darkness = 10, Copies = 1 });
    new WindowsRawPrintTransport().Send(bytes, printerName);
}
```

Não existe um segundo multiplicador de "cópias" por cima disso — a quantidade de registros que
você monta em `records` **é** a quantidade de etiquetas impressas. Ver
[ARCHITECTURE.md §5](ARCHITECTURE.md#5-impressão-em-colunas-mala-direta) se sua etiqueta usa
`Page.Columns > 1` e você quiser entender por que é assim.

### 2.7 Persistindo as preferências da sua aplicação

Nunca guarde preferências de app (impressora usada, formato, quantidade de colunas) dentro do
`LabelDocument` — isso é sobre *como você usa o app*, não sobre o documento. Siga o padrão já usado
em todo o repositório (`LibraryRepository`, `PrintSettingsStore`, `PrintColumnsSettingsStore`):

```csharp
internal static class MinhasPreferenciasStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SuaAplicação", "minhas-preferencias.json");

    public static MinhasPreferencias Load()
    {
        try { return JsonSerializer.Deserialize<MinhasPreferencias>(File.ReadAllText(FilePath)) ?? new(); }
        catch { return new(); } // arquivo ausente/corrompido nunca deve travar a tela
    }

    public static void Save(MinhasPreferencias prefs)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(prefs));
        }
        catch { } // falha ao persistir nunca deve derrubar uma ação que já deu certo
    }
}
```

## 3. Caminho B — aplicação legada (ASP.NET Framework / .NET Framework 4.x)

Se a sua aplicação não pode referenciar `net9.0-windows` diretamente, ela abre o
`LabelSharpDesignerCore.App.exe` como processo satélite só para editar **um** arquivo `.label` por vez
— sem a biblioteca inteira, sem as telas de produto/impressão em lote deste guia (essas continuam
sendo responsabilidade da sua própria aplicação, chamando o satélite só pela parte visual do
editor). Esta seção é o passo a passo completo para quando você só quer **a tela de desenhar a
etiqueta**, nada mais — nenhuma outra parte deste guia (biblioteca, vínculo de campos, impressão em
lote) é necessária para isso.

### 3.0 Antes de começar: onde esse código vai rodar

`LegacyLauncher.Launch` faz um `Process.Start` de um `.exe` do Windows Forms **e bloqueia até o
usuário fechar a janela**. Isso só mostra uma janela de verdade pra alguém quando o processo que
chama roda numa **sessão de desktop interativa** — a mesma máquina, o mesmo usuário logado que vai
ver o editor.

- **Funciona direto**: IIS Express local (debug no Visual Studio), uma aplicação intranet onde cada
  estação roda seu próprio IIS/IIS Express como o usuário logado, ou qualquer cenário onde "o
  servidor" é, na prática, o computador da pessoa que vai desenhar a etiqueta.
- **Não funciona**: IIS "de verdade" hospedado num servidor, atendendo usuários remotos pelo
  navegador. Um site de produção roda o worker process do IIS na *Session 0* do Windows (isolada de
  qualquer área de trabalho desde o Vista/Server 2008) — `Process.Start` ali não mostra nada para o
  usuário do navegador, só abre uma janela invisível na sessão 0 (ou falha, dependendo da versão do
  Windows). Se for esse o seu caso, esse caminho por `Process.Start` não serve como está documentado
  aqui — o editor precisaria rodar na máquina do usuário e ser disparado por outro mecanismo (ex.:
  instalado localmente e acionado por um esquema de URI customizado registrado no Windows), o que
  está fora do escopo deste guia.

Os passos abaixo assumem o primeiro cenário.

### 3.1 Publique o `LabelSharpDesignerCore.App.exe`

```powershell
dotnet publish src/LabelSharpDesignerCore.App -c Release -r win-x64 --self-contained false
```

Copie a pasta de saída (`LabelSharpDesignerCore.App.exe` + DLLs ao lado) para um caminho fixo acessível
pela sua aplicação — por exemplo `C:\LabelSharpDesignerCore\LabelSharpDesignerCore.App.exe`. Esse caminho é
inteiramente decisão sua (rede, `Program Files`, o que fizer sentido no seu ambiente).

### 3.2 Compile e referencie o `Legacy.Bridge`

```powershell
dotnet build src/LabelSharpDesignerCore.Legacy.Bridge -f netstandard2.0 -c Release
```

Isso gera `src/LabelSharpDesignerCore.Legacy.Bridge/bin/Release/netstandard2.0/LabelSharpDesignerCore.Legacy.Bridge.dll`
— sem nenhuma dependência externa (só BCL), então basta referenciar essa única DLL no seu projeto
ASP.NET Framework:

```xml
<!-- no .csproj do projeto ASP.NET Framework (formato clássico) -->
<Reference Include="LabelSharpDesignerCore.Legacy.Bridge">
  <HintPath>C:\LabelSharpDesignerCore\LabelSharpDesignerCore.Legacy.Bridge.dll</HintPath>
</Reference>
```

Requer .NET Framework **4.6.1 ou superior** (é o mínimo que consome `netstandard2.0`; 4.7.2+
recomendado). Não precisa de NuGet nem de `bindingRedirect` — a DLL não traz dependências
transitivas.

### 3.3 Decida onde os `.label` ficam

Você não precisa do `LibraryRepository`/`LibraryForm` nem de nada parecido com "biblioteca" — só um
arquivo por etiqueta, no caminho que fizer sentido no seu domínio (uma coluna `CaminhoEtiqueta` na
sua tabela de produtos/pedidos, uma pasta `~/App_Data/Labels/{id}.label`, um compartilhamento de
rede — o `LegacyLauncher` aceita qualquer caminho absoluto). Se o arquivo ainda não existe, crie um
documento em branco antes de chamar o editor:

```csharp
using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Serialization;

if (!File.Exists(caminhoDoLabel))
{
    var documentoEmBranco = new LabelDocument
    {
        Name = "Nova etiqueta",
        Page = new PageConfig { WidthMm = 100, HeightMm = 60, Dpi = 203 },
        Layers = [new LabelLayer { Id = "layer-1", Name = "Base", Order = 0 }],
    };
    File.WriteAllText(caminhoDoLabel, LabelDocumentCodec.Save(documentoEmBranco));
}
```

(`LabelSharpDesignerCore.Core`/`Serialization` também são `netstandard2.0` — podem ser referenciados do
mesmo jeito, se você quiser montar/ler o documento sem abrir o editor. Ver [USAGE.md §2/§8](USAGE.md).)

### 3.4 Chame o editor

`LegacyLauncher` é uma classe normal, não estática — instancie com o caminho do `.exe` publicado no
passo 3.1:

```csharp
using LabelSharpDesignerCore.Legacy.Bridge;

var launcher = new LegacyLauncher(@"C:\LabelSharpDesignerCore\LabelSharpDesignerCore.App.exe");
var request = new LaunchRequest { FilePath = caminhoDoLabel, ReadOnly = false };
LaunchResult result = launcher.Launch(request); // bloqueia até o usuário fechar a janela do editor

switch (result.Outcome)
{
    case LaunchOutcome.Saved:
        // o arquivo em caminhoDoLabel foi sobrescrito — recarregue/atualize sua miniatura
        break;
    case LaunchOutcome.Cancelled:
        // fechou sem salvar (ou a sessão era ReadOnly = true) — nada mudou no arquivo
        break;
    case LaunchOutcome.Error:
        // não deu para abrir o arquivo (corrompido, formato inválido) — avise o usuário
        break;
}
```

**Exemplo — ASP.NET MVC**, um botão "Desenhar etiqueta" na tela de um produto:

```csharp
public class EtiquetaController : Controller
{
    private const string AppExePath = @"C:\LabelSharpDesignerCore\LabelSharpDesignerCore.App.exe";

    [HttpPost]
    public ActionResult Desenhar(int produtoId)
    {
        var caminho = CaminhoDoLabel(produtoId); // sua própria convenção de nomeação
        GarantirDocumentoEmBranco(caminho);       // passo 3.3

        var launcher = new LegacyLauncher(AppExePath);
        var result = launcher.Launch(new LaunchRequest { FilePath = caminho });

        TempData["EtiquetaSalva"] = result.Outcome == LaunchOutcome.Saved;
        return RedirectToAction("Detalhes", new { id = produtoId });
    }
}
```

**Exemplo — Web Forms**, no code-behind de um botão:

```csharp
protected void BtnDesenharEtiqueta_Click(object sender, EventArgs e)
{
    var caminho = CaminhoDoLabel(ProdutoIdAtual);
    GarantirDocumentoEmBranco(caminho);

    var launcher = new LegacyLauncher(AppExePath);
    var result = launcher.Launch(new LaunchRequest { FilePath = caminho });

    lblStatus.Text = result.Outcome == LaunchOutcome.Saved ? "Etiqueta salva." : "Nada foi alterado.";
}
```

Nos dois casos a requisição/postback só retorna depois que o usuário fecha a janela do editor — é
esperado (ver 3.0); não tente tornar isso assíncrono/"fire and forget", já que a resposta HTTP
depende do `LaunchOutcome`.

### 3.5 (Opcional) Miniatura da etiqueta sem abrir o editor

Pra mostrar um preview no seu grid/lista sem instanciar nenhuma UI, resolva e exporte PNG
diretamente (`Core`/`Layout`/`Rendering.Png`, todos `netstandard2.0`):

```csharp
using LabelSharpDesignerCore.Layout;
using LabelSharpDesignerCore.Rendering.Png;

var documento = LabelDocumentCodec.Load(File.ReadAllText(caminhoDoLabel));
var amostra = documento.Variables.ToDictionary(v => v.Name, object? (v) => v.DefaultValue);
var resolvido = new LayoutEngine().Resolve(documento, new LayoutOptions { SampleData = amostra });
byte[] png = PngExporter.ExportScaled(resolvido, targetWidthPx: 240);
```

### 3.6 Checklist — Caminho B

- [ ] O código que chama `LegacyLauncher.Launch` roda numa sessão de desktop interativa da máquina
      do usuário (3.0) — não num IIS de produção atendendo clientes remotos.
- [ ] `LabelSharpDesignerCore.App.exe` está publicado (3.1) num caminho que o processo da sua aplicação
      consegue alcançar.
- [ ] Você referenciou só a DLL `netstandard2.0` do `Legacy.Bridge` — nada de `net9.0-windows`, isso
      nunca compilaria contra .NET Framework.
- [ ] `new LegacyLauncher(caminhoDoExe)` — é instância, não `LegacyLauncher.Launch(...)` estático.
- [ ] Você trata os três `LaunchOutcome` (`Saved`/`Cancelled`/`Error`), não só o caminho feliz.

Detalhes do contrato completo (formato exato dos argumentos de linha de comando, como o satélite
detecta modo de edição direta vs. modo biblioteca) estão em
[ARCHITECTURE.md §7](ARCHITECTURE.md#7-integração-com-o-legado--labelsharpdesignerlegacybridge).

## 4. Checklist antes de integrar

- [ ] Você referenciou só as libs que precisa (2.1) — não precisa de `Rendering.ArgoxPpla`/
      `PrintTransport.Windows` se só vai exportar PDF, por exemplo.
- [ ] Você **não** está tentando resolver/rasterizar um documento na mão — sempre
      `LayoutEngine.Resolve`/`ResolveBatch` primeiro, e os `ResolvedDocument` resultantes para
      qualquer renderer/exporter. Nenhum outro lugar do pipeline recalcula layout.
- [ ] O vínculo campo-da-entidade → variável-da-etiqueta é configurável pelo usuário e persistido
      por `LibraryEntry.Id` (seção 2.4) — não codificado como `if (variable.Name == "preco")` fixo
      no seu código, o que quebraria assim que alguém desenhasse uma etiqueta com nomes diferentes.
- [ ] Preferências de app (impressora, formato, colunas) ficam em `%APPDATA%\<SuaAplicação>\*.json`
      via `Open`/carregar-com-fallback-silencioso — nunca dentro do `LabelDocument`.
- [ ] Se a etiqueta usa `Page.Columns > 1`, sua tela de impressão tem **um único** campo de
      quantidade, nunca dois multiplicadores empilhados (seção 2.6).
