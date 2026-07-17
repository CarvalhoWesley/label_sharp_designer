# LabelSharpDesignerCore.Expressions

## O que é

O "motor de cálculo" das expressões `{{ }}` que aparecem dentro dos textos da etiqueta. Quando você
escreve numa etiqueta algo como `{{ preco * 1.2 }}` ou `{{ produto.nome }}`, é este projeto que
entende esse texto e devolve o resultado calculado.

Ele é totalmente independente — não depende nem do `LabelSharpDesignerCore.Core`. Só sabe interpretar
texto e calcular expressões, sem saber nada sobre "o que é uma etiqueta".

## Como funciona (pipeline clássico de interpretador)

```
"preco * 1.2"  →  Lexer  →  Parser  →  Evaluator  →  1.2 * valor de "preco"
                 (tokens)    (árvore/AST)  (calcula)
```

1. **`Tokenizing/Lexer`** — quebra o texto em pedaços (*tokens*): números, nomes de variável,
   operadores (`+`, `-`, `*`, `.`), parênteses etc.
2. **`Parsing/Parser`** — organiza esses tokens numa árvore (`Ast/ExpressionNode`) que representa a
   estrutura da expressão (o que é multiplicado por o quê, em que ordem).
3. **`Evaluation/Evaluator`** — percorre essa árvore e calcula o valor final, usando um
   **`EvaluationContext`** (as variáveis disponíveis, tipo `preco = 49.9`).

Erros de digitação na expressão (`{{ preco * }}`, por exemplo) geram uma
`ExpressionSyntaxException`; erros ao calcular (variável que não existe) geram uma
`ExpressionEvaluationException`.

## As duas classes que você realmente vai usar

- **`ExpressionEngine`** — ponto de entrada para calcular uma expressão "pura", sem texto ao redor:
  `engine.Evaluate("preco * 1.2", contexto)`.
- **`TemplateResolver`** — para textos livres com `{{ }}` misturado, tipo
  `"{{descricao}} — R$ {{preco}}"`. Ele encontra cada trecho entre chaves, resolve com o
  `ExpressionEngine` e junta tudo de volta como uma única string. É isso que roda por trás de
  `TextElement.Content`, `BarcodeElement.Data`, `QrCodeElement.Data` e `ImageElement.Source`.

## Funções embutidas

Em `Evaluation/Functions/` ficam as funções que podem ser chamadas dentro de uma expressão, como
`FormatFunction` (formatar um número/data) e `CurrencyFunction` (formatar como moeda). Novas funções
entram nesse mesmo padrão e são registradas em `FunctionRegistry`.

## Quem usa este projeto

O `LabelSharpDesignerCore.Layout` é o único consumidor direto — é lá que o `LayoutEngine` chama o
`TemplateResolver`/`ExpressionEngine` para resolver cada elemento da etiqueta antes de desenhar.

## Compatibilidade

`netstandard2.0;net9.0` — roda em qualquer plataforma, sem dependência de Windows.
