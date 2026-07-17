# LabelSharpDesignerCore.History

## O que é

Implementa o **undo/redo** (desfazer/refazer) do editor de etiquetas, usando o padrão de projeto
*Command*. Cada ação que o usuário faz no editor (mover um elemento, redimensionar, mudar uma
propriedade, apagar) vira um objeto de comando que sabe como se desfazer.

## Como o padrão Command funciona aqui

Como todo o modelo de `LabelSharpDesignerCore.Core` é imutável (`record`, sempre `with { ... }` para
"alterar" algo), cada comando aqui é só um par de instantâneos: o documento **antes** e o documento
**depois** da ação. Desfazer é simplesmente voltar para o "antes"; refazer é ir de novo para o
"depois". Não existe lógica de "reverter passo a passo" — é sempre um snapshot completo, o que evita
bugs sutis de estado compartilhado.

## Peças principais

- **`ICommand`** — o contrato: todo comando sabe se `Execute()`/desfazer.
- **`DocumentCommand`** — classe-base para comandos que guardam um documento "antes"/"depois".
- Comandos concretos: **`AddCommand`**, **`DeleteCommand`**, **`MoveCommand`**,
  **`ResizeCommand`**, **`RotateCommand`**, **`ChangePropertyCommand`** (mudar uma propriedade
  qualquer de um elemento), **`ChangeDocumentCommand`** (mudança no documento inteiro, ex.:
  configurações de página) e **`CompositeCommand`** (agrupa vários comandos como se fossem um só —
  útil para uma ação do usuário que na prática mexe em várias coisas ao mesmo tempo).
- **`HistoryManager`** — mantém as duas pilhas (undo/redo) e dois jeitos de aplicar mudança:
  - `Execute` — grava um passo desfazível de verdade (aparece no undo).
  - `PreviewChange` — atualiza o documento atual e notifica quem estiver ouvindo, **sem** empilhar
    undo. Usado tanto para feedback visual "ao vivo" durante um arraste (antes de soltar o mouse)
    quanto para mudanças de configuração que não fazem sentido como um passo de undo (grade, snap,
    guias).

## Quem usa este projeto

O `LabelSharpDesignerCore.UI.WinForms` (especificamente `LabelCanvasControl`) é o principal consumidor —
toda interação do usuário no editor (arrastar, redimensionar, apagar) passa por um `HistoryManager`.

## Dependências

Depende só de `LabelSharpDesignerCore.Core` (o `LabelDocument` que ele guarda em cada snapshot).
Multi-targeta `netstandard2.0;net9.0`, sem dependência de Windows.
