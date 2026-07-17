# LabelSharpDesignerCore.Rendering.Abstractions

## O que é

Um projeto pequeno e propositalmente "vazio de lógica": define só o **contrato** (interface) que
todo "desenhador" de etiquetas (Canvas, PDF, PPLA...) precisa implementar. Não tem nenhuma
implementação de desenho real — só a interface e uma classe auxiliar para despachar o tipo certo.

## Por que ele existe separado

Um `ResolvedElement` (a saída do `LayoutEngine`) carrega um `ResolvedPayload` que pode ser de vários
tipos: texto, forma, código de barras, imagem, tabela... Cada "desenhador" (`Rendering.Canvas`,
`Rendering.Pdf`) precisa saber, para cada payload, qual rotina de desenho chamar. Em vez de cada
projeto de renderização reinventar esse despacho por tipo, este projeto define uma vez só o
contrato — assim todo mundo segue o mesmo padrão (Visitor).

## Peças principais

- **`IResolvedPayloadVisitor<TResult>`** — a interface: um método `Visit...` para cada tipo de
  `ResolvedPayload` (texto, forma, código de barras, imagem, tabela). Cada renderer implementa essa
  interface e devolve, por exemplo, "nada" (`Rendering.Canvas`, que só desenha direto no canvas) ou
  um resultado (dependendo da necessidade).
- **`ResolvedPayloadDispatch`** — código utilitário que, dado um `ResolvedPayload` e um visitor,
  chama o método `Visit...` certo automaticamente — evita um `switch`/`if` repetido em cada
  renderer.

## Quem usa este projeto

`Rendering.Canvas` e `Rendering.Pdf` implementam `IResolvedPayloadVisitor` para saber desenhar cada
tipo de elemento. `Rendering.Png` e `Rendering.ArgoxPpla` usam essas implementações por baixo dos
panos (não implementam a interface diretamente).

## Dependências

Depende só de `LabelSharpDesignerCore.Core` (os tipos `ResolvedElement`/`ResolvedPayload` que ele
despacha). Multi-targeta `netstandard2.0;net9.0`, sem dependência de Windows.
