# LabelSharpDesigner.SampleApp

## O que é

Um aplicativo de **exemplo funcional**, não uma biblioteca a ser referenciada por outros projetos.
Ele mostra, de ponta a ponta, como uma aplicação própria (aqui, um catálogo fictício de produtos)
integra o LabelSharpDesigner para desenhar etiquetas e imprimir dados reais nelas. Se você está
integrando o LabelSharpDesigner num sistema seu, este é o primeiro lugar para olhar código real
funcionando — o guia [INTEGRATION.md](../../INTEGRATION.md) foi escrito descrevendo exatamente o que
este projeto faz.

## O cenário simulado

Um catálogo simples de produtos (`Products/Product`, `ProductRepository`), com telas para
listar/editar produtos (`ProductListForm`, `ProductEditForm`) e para imprimir etiquetas para eles
(`Printing/PrintProductsForm`).

## O ponto central: vínculo dinâmico entre produto e variáveis da etiqueta

O maior problema que qualquer integração real precisa resolver é: a etiqueta só sabe que existe uma
variável chamada, por exemplo, `preco` — ela não sabe nada sobre a classe `Product` da sua
aplicação. Este exemplo resolve isso deixando o **usuário configurar esse vínculo** pela própria UI,
em vez de cravar nomes de variável fixos no código:

- **`Printing/ProductFieldSource`** — enum com os campos do produto que podem alimentar uma
  variável da etiqueta (`Description`, `Price`, `Barcode`, ou `LabelDefault` para usar o valor
  padrão da própria etiqueta).
- **`Printing/VariableMappingForm`** — tela montada dinamicamente a partir de
  `document.Variables`: uma linha por variável que a etiqueta declara, cada uma com um combo
  escolhendo de onde vem o valor.
- **`Printing/VariableFieldMappingStore`** — persiste esse vínculo em
  `%APPDATA%\...\variable-field-mappings.json`, indexado pelo `Id` estável da etiqueta (nunca pelo
  nome de exibição, que o usuário pode renomear a qualquer momento).

Isso é o que torna a integração genérica: funciona com **qualquer** etiqueta desenhada no editor,
sem a aplicação precisar saber de antemão quais nomes de variável a pessoa que desenhou a etiqueta
escolheu.

## Impressão em lote (`Printing/`)

- **`PrintProductsForm`** — a tela de impressão: escolhe produtos, resolve o vínculo de campos,
  monta os registros e chama `LayoutEngine.ResolveBatch` + os exportadores/transportes reais
  (`Rendering.Pdf`/`PrintTransport.Windows`).
- **`ProductPrintRow`** — um item da fila de impressão.
- **`PrintColumnsSettings`/`PrintColumnsSettingsStore`** — preferência de quantas colunas usar na
  impressão (quando a etiqueta tem `Page.Columns > 1`), persistida como configuração de app.
- **`LabelPreviewControl`** — pré-visualização ao vivo reaproveitando `LabelCanvasRenderer` do
  `Rendering.Canvas` dentro de um `SKControl` — a mesma técnica recomendada para qualquer app que
  queira preview sem reimplementar desenho.

## Como rodar

```powershell
dotnet run --project src/LabelSharpDesigner.SampleApp
```

## Dependências

Referencia o mesmo conjunto de projetos que o `LabelSharpDesigner.App` real usaria numa integração
completa: `Core`, `Layout`, `Serialization`, `Rendering.Canvas`, `Rendering.Pdf`,
`PrintTransport.Windows` e `App` (para reaproveitar `LibraryForm`/`LibraryRepository` prontos — veja
[`LabelSharpDesigner.SampleApp.csproj`](LabelSharpDesigner.SampleApp.csproj) como modelo de
`ItemGroup` para o seu próprio projeto). Alvo `net9.0-windows10.0.19041.0`: exige Windows.
