# LabelSharpDesignerCore.Serialization

## O que é

O responsável por **salvar e carregar** um `LabelDocument` como arquivo `.label` (que, por dentro, é
um JSON). Sempre que o editor salva uma etiqueta no disco, ou abre uma etiqueta salva
anteriormente, é este projeto que faz a conversão entre o objeto C# e o texto JSON.

## Peças principais

- **`LabelDocumentCodec`** — a porta de entrada, com dois métodos:
  ```csharp
  string json = LabelDocumentCodec.Save(document);
  File.WriteAllText("etiqueta.label", json);

  LabelDocument carregado = LabelDocumentCodec.Load(File.ReadAllText("etiqueta.label"));
  ```
- **`Converters/LabelElementJsonConverter`** — `LabelElement` é uma classe-base com várias
  subclasses (`TextElement`, `BarcodeElement`, `TableElement`...). JSON puro não sabe, sozinho,
  "que tipo de elemento é esse objeto" — esse conversor customizado lê/escreve um campo de tipo no
  JSON para reconstruir a subclasse certa ao carregar.
- **`JsonOptionsFactory`** — centraliza a configuração do `System.Text.Json` usada em todo o
  projeto (formatação, conversores registrados etc.), para que `Save`/`Load` sejam sempre
  consistentes.
- **`Migrations/IMigration` + `MigrationChain`** — resolvem um problema comum em qualquer app que
  salva arquivos: o formato do `.label` muda com o tempo (uma nova versão do app adiciona um campo,
  por exemplo). Cada `IMigration` sabe transformar um JSON de uma versão antiga na versão seguinte;
  a `MigrationChain` aplica essas migrações em sequência até chegar na versão atual. Isso acontece
  automaticamente dentro de `LabelDocumentCodec.Load` — quem chama não precisa se preocupar com
  versão nenhuma.

## Por que isso importa para quem só quer usar o editor

Se você está integrando o LabelSharpDesignerCore numa aplicação própria, não precisa entender o formato
do JSON — só usar `Save`/`Load`. E arquivos `.label` antigos, salvos por versões anteriores do
sistema, continuam abrindo normalmente graças às migrações.

## Dependências

Depende só de `LabelSharpDesignerCore.Core` (o modelo que ele serializa). Multi-targeta
`netstandard2.0;net9.0`, sem dependência de Windows — pode ser referenciado até por uma aplicação
legada em .NET Framework 4.6.1+ que só precise ler/gravar arquivos `.label`.
