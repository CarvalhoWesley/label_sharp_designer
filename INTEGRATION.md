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
raster), veja [GUIA_RAPIDO_MODERNO.md](GUIA_RAPIDO_MODERNO.md) (.NET moderno) ou
[GUIA_RAPIDO_FRAMEWORK.md](GUIA_RAPIDO_FRAMEWORK.md) (.NET Framework 4.x) em vez deste guia completo.

Existe um exemplo completo e funcional no próprio repositório —
[`src/LabelSharpDesignerCore.SampleApp`](src/LabelSharpDesignerCore.SampleApp) — que implementa
exatamente o que este guia descreve, com um catálogo de produtos fictício no lugar da sua
entidade de domínio. Todo trecho de código abaixo foi tirado (ou simplificado) direto dele; quando
tiver dúvida, abra o projeto e compare.

## 1. Como referenciar os projetos

Todo projeto de domínio/lógica multi-targeta `netstandard2.0;net9.0`. `UI.WinForms`, `App` e
`PrintTransport.Windows` multi-targetam `net48;net9.0-windows(...)` — qualquer aplicação Windows
Forms referencia o que precisar direto, seja ela **.NET moderno** (net8+/net9) ou **.NET Framework
4.6.1+** (4.7.2+ recomendado; é o mesmo mínimo que consome `netstandard2.0`). Não existe um caminho
de integração diferente por TFM — a API é idêntica dos dois lados.

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
como modelo pronto de `ItemGroup`. Seu projeto precisa ter `UseWindowsForms=true` para poder
referenciar `App`/`Rendering.Canvas`/`PrintTransport.Windows`:

- **.NET moderno**: `TargetFramework` `net9.0-windows10.0.19041.0` (ou o TFM Windows equivalente do
  seu SDK) — `ProjectReference` funciona normalmente.
- **.NET Framework 4.x**: `TargetFramework` `net48` (ou qualquer 4.6.1+). Se o seu projeto já é
  SDK-style (`<Project Sdk="Microsoft.NET.Sdk">`, mesmo mirando `net48`), `ProjectReference` também
  funciona direto — o MSBuild resolve sozinho a perna `net48` de cada dependência multi-targetada. Se
  o seu projeto é do formato clássico (`packages.config`, sem `Sdk=`), referencie os `.dll`
  compilados em `bin/Release/net48/` de cada projeto via `<Reference><HintPath>` — publique antes com
  `dotnet publish src/LabelSharpDesignerCore.App -c Release -f net48` e copie a pasta de saída
  inteira (o `App` traz várias dependências transitivas: `SkiaSharp`, `PdfSharp`, `ZXing.Net`,
  `System.Text.Json` — não é só um `.dll`).

> Sim, dá para referenciar o projeto `LabelSharpDesignerCore.App` (que é `OutputType=WinExe`) como
> qualquer outra lib — o `Main` dele só roda se você chamar; sua aplicação continua dona do próprio
> `Program.Main`.

**Única diferença de comportamento entre as duas pernas**: o tema escuro (`Application.SetColorMode`)
é uma API exclusiva do WinForms .NET 9+, sem equivalente no .NET Framework — a build `net48` sempre
roda no tema claro clássico do Windows, independente da preferência de tema do usuário.

**Isso vale para aplicações desktop** (WinForms/WPF, ou qualquer processo com uma sessão de desktop
interativa) — `EditorForm`/`LibraryForm` são janelas WinForms de verdade, `ShowDialog` bloqueia até o
usuário fechar. Se a sua aplicação é **web** (ASP.NET/ASP.NET Framework atrás de IIS de produção
atendendo clientes remotos), referenciar os projetos diretamente **não resolve** abrir a tela do
editor: o worker process do IIS roda na Session 0 (isolada de qualquer área de trabalho desde o Vista/
Server 2008), então nenhuma UI WinForms consegue aparecer para o usuário do navegador dali — nem em
processo, nem em processo separado. Se esse for o seu caso, veja
[`LabelSharpDesignerCore.Legacy.Bridge`](src/LabelSharpDesignerCore.Legacy.Bridge/README.md): abrir o
editor como processo satélite ainda funciona quando "o servidor" é, na prática, o computador da
pessoa que vai desenhar a etiqueta (IIS Express local, uma estação rodando seu próprio IIS local como
o usuário logado) — mas isso é uma exceção para esse cenário específico, não o caminho recomendado
para o resto deste guia.

## 2. Tela de "gerenciar etiquetas" — não reimplemente, reaproveite

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
etiqueta específica (seção 4).

## 3. O que você precisa saber do `LabelDocument`

Você não precisa entender o pipeline inteiro — só estes três pontos, para o que vem a seguir:

- **`LabelDocument.Variables`** (`IReadOnlyList<LabelVariable>`): as variáveis `{{ }}` que a
  etiqueta declara (`Name`, `Type`, `DefaultValue`). Quem decide os nomes é a pessoa que desenhou a
  etiqueta no editor — a sua aplicação não controla isso, só **preenche** os valores na hora de
  imprimir (seção 4).
- **`LabelDocument.Page.Columns`**: quantas etiquetas cabem lado a lado numa fileira física (rolo
  multi-coluna). É uma propriedade do *documento*, não da sua impressão — trate como sugestão
  inicial, não como verdade absoluta (veja 6).
- **Tudo é imutável** (`record`, use `with` pra alterar): `document.Page.Columns` não muda com um
  setter, você cria uma cópia: `document with { Page = document.Page with { Columns = 2 } }`.

Se você só precisa criar um documento em branco antes de abrir o editor (sem nenhuma tela de
biblioteca ao redor), o padrão é:

```csharp
using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Serialization;

var documentoEmBranco = new LabelDocument
{
    Name = "Nova etiqueta",
    Page = new PageConfig { WidthMm = 100, HeightMm = 60, Dpi = 203 },
    Layers = [new LabelLayer { Id = "layer-1", Name = "Base", Order = 0 }],
};
File.WriteAllText(caminhoDoLabel, LabelDocumentCodec.Save(documentoEmBranco));
```

## 4. Vinculando os dados da sua entidade às variáveis da etiqueta

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

## 5. Preview

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

## 6. Resolvendo em lote e imprimindo

Sua tela de impressão sempre trabalha em modo lote — mesmo para "uma etiqueta de cada produto",
porque no fim das contas você está imprimindo N registros, um por etiqueta física:

```csharp
using LabelSharpDesignerCore.Layout;

var layoutEngine = new LayoutEngine();
var document = entry.Document with { Page = entry.Document.Page with { Columns = colunasEscolhidas } };
var records = /* um IReadOnlyDictionary<string, object?> por etiqueta física, ver 4 */;
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

## 7. Persistindo as preferências da sua aplicação

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

No .NET Framework 4.x, `System.Text.Json` não vem no framework compartilhado (como vem no .NET
9) — adicione o pacote NuGet `System.Text.Json` ao seu projeto `net48` (ver
[`LabelSharpDesignerCore.App.csproj`](src/LabelSharpDesignerCore.App/LabelSharpDesignerCore.App.csproj)
como referência de versão).

## 8. Miniatura da etiqueta sem abrir o editor

Pra mostrar um preview no seu grid/lista sem instanciar nenhuma UI, resolva e exporte PNG
diretamente (`Core`/`Layout`/`Rendering.Png`, todos `netstandard2.0` — funciona igual em .NET
moderno ou .NET Framework 4.x, sem precisar de `UseWindowsForms`):

```csharp
using LabelSharpDesignerCore.Layout;
using LabelSharpDesignerCore.Rendering.Png;

var documento = LabelDocumentCodec.Load(File.ReadAllText(caminhoDoLabel));
var amostra = documento.Variables.ToDictionary(v => v.Name, object? (v) => v.DefaultValue);
var resolvido = new LayoutEngine().Resolve(documento, new LayoutOptions { SampleData = amostra });
byte[] png = PngExporter.ExportScaled(resolvido, targetWidthPx: 240);
```

## 9. Checklist antes de integrar

- [ ] Você referenciou só as libs que precisa (seção 1) — não precisa de `Rendering.ArgoxPpla`/
      `PrintTransport.Windows` se só vai exportar PDF, por exemplo.
- [ ] Se o seu host é web (ASP.NET/ASP.NET Framework atrás de IIS de produção), você confirmou que
      não é esse o cenário antes de tentar abrir `EditorForm`/`LibraryForm` — veja a nota no fim da
      seção 1.
- [ ] Você **não** está tentando resolver/rasterizar um documento na mão — sempre
      `LayoutEngine.Resolve`/`ResolveBatch` primeiro, e os `ResolvedDocument` resultantes para
      qualquer renderer/exporter. Nenhum outro lugar do pipeline recalcula layout.
- [ ] O vínculo campo-da-entidade → variável-da-etiqueta é configurável pelo usuário e persistido
      por `LibraryEntry.Id` (seção 4) — não codificado como `if (variable.Name == "preco")` fixo
      no seu código, o que quebraria assim que alguém desenhasse uma etiqueta com nomes diferentes.
- [ ] Preferências de app (impressora, formato, colunas) ficam em `%APPDATA%\<SuaAplicação>\*.json`
      via `Open`/carregar-com-fallback-silencioso — nunca dentro do `LabelDocument`.
- [ ] Se a etiqueta usa `Page.Columns > 1`, sua tela de impressão tem **um único** campo de
      quantidade, nunca dois multiplicadores empilhados (seção 6).
