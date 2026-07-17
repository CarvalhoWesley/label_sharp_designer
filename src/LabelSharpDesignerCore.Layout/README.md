# LabelSharpDesignerCore.Layout

## O que é

O "cérebro" que transforma o desenho abstrato de uma etiqueta (`LabelDocument`, em milímetros, com
`{{ variáveis }}` ainda não calculadas) num resultado concreto e pronto para desenhar
(`ResolvedDocument`, em pixels/dots, com todo texto já calculado). É o único projeto do repositório
autorizado a fazer esse cálculo — essa é a regra mais importante de toda a arquitetura.

## Por que "só ele resolve" importa

Existem vários jeitos de mostrar uma etiqueta: a pré-visualização na tela, um PNG exportado, um PDF,
os comandos de uma impressora térmica Argox. Se cada um desses caminhos calculasse a posição dos
elementos e o valor das variáveis por conta própria, um pequeno bug faria a pré-visualização mostrar
uma coisa e o PDF imprimir outra. Ao centralizar esse cálculo aqui, todos os outros projetos de
renderização (`Rendering.*`) recebem exatamente o mesmo resultado já pronto — eles só desenham, não
decidem.

## Como usar

```csharp
using LabelSharpDesignerCore.Layout;

var options = new LayoutOptions
{
    SampleData = new Dictionary<string, object?>
    {
        ["descricao"] = "Camiseta azul",
        ["preco"] = 49.9,
        ["codigobarras"] = "7891234567890",
    },
};

ResolvedDocument resolved = new LayoutEngine().Resolve(document, options);
// resolved.WidthDots / HeightDots / Dpi / Elements — tudo já em pixels, com {{ }} calculado
```

## Peças principais

- **`LayoutEngine.Resolve`** — resolve **um** documento: converte milímetros para pixels (dots) de
  acordo com o DPI da página, calcula `{{ expressões }}` (via `LabelSharpDesignerCore.Expressions`),
  aplica z-order (ordem de sobreposição) e rotação — inclusive rotação "composta" quando um
  elemento está dentro de um `GroupElement` rotacionado.
- **`LayoutEngine.ResolveBatch`** — a mesma coisa, mas para impressão em lote/mala direta: recebe
  uma lista de registros de dados e devolve um `ResolvedDocument` por fileira física, já lidando com
  `PageConfig.Columns` (várias etiquetas lado a lado no mesmo rolo). Veja a seção 5 do
  [ARCHITECTURE.md](../../ARCHITECTURE.md) para entender por que a contagem de registros já **é** a
  quantidade de etiquetas — nunca some um multiplicador de "cópias" em cima disso.
- **`ElementResolvingVisitor`** — implementa `IElementVisitor` (de `Core`) para saber, tipo a tipo,
  como resolver cada `LabelElement` (texto, código de barras, tabela...) em um `ResolvedElement`.
- **`MmConversion`** — a matemática de converter milímetros ↔ pixels/dots dado um DPI.
- **`LayoutOptions`** — o que você fornece para o cálculo: `SampleData` (valores das variáveis),
  `Now` (data/hora usada por `DateElement`/`TimeElement` com origem "Now") e, se precisar de funções
  customizadas nas expressões, um `ExpressionEngine` próprio.

## Dependências

Depende de `LabelSharpDesignerCore.Core` (o modelo que ele resolve) e
`LabelSharpDesignerCore.Expressions` (para calcular as `{{ }}`). Multi-targeta
`netstandard2.0;net9.0`, sem dependência de Windows.

## Quem usa este projeto

Todo mundo que precisa desenhar/exportar/imprimir uma etiqueta: `Rendering.Canvas`,
`Rendering.Png`, `Rendering.Pdf`, `Rendering.ArgoxPpla` — todos recebem o `ResolvedDocument`
produzido aqui, nunca recalculam nada por conta própria.
