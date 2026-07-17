# LabelSharpDesignerCore.PrintTransport.Windows

## O que é

A camada final que efetivamente **envia bytes para uma impressora física** no Windows. Enquanto os
projetos `Rendering.*` sabem *gerar* PDF/PPLA, este projeto sabe *entregar* esses bytes à
impressora — seja via driver do Windows (para impressoras comuns), seja via bytes crus direto no
spooler (para impressoras térmicas PPLA).

## Peças principais

- **`IPrintTransport`** — contrato comum: "envie estes bytes para esta impressora (ou a padrão)".
- **`WindowsPdfPrintTransport`** — envia um PDF (gerado por `Rendering.Pdf`) para impressão via
  driver do Windows. Funciona com qualquer impressora instalada normalmente no sistema.
- **`WindowsRawPrintTransport`** — envia bytes crus (gerados por `Rendering.ArgoxPpla`) direto para
  o spooler via P/Invoke em `winspool.drv` — equivalente ao clássico `RawPrinterHelper` que existe
  em vários projetos .NET Framework antigos. É o único caminho para imprimir PPLA, já que ele não
  passa pelo driver comum do Windows.
- **`RawPrinterHelper`** — a implementação P/Invoke por trás do envio de bytes crus.
- **`IPrinterDiscovery`** / **`WindowsPrinterDiscovery`** — lista as impressoras instaladas na
  máquina (`ListAvailable()`), para preencher, por exemplo, um combo de seleção de impressora numa
  tela de impressão.
- **`PrintTransportException`** — erro lançado quando o envio para a impressora falha (impressora
  offline, driver com problema etc.).

## Como usar

```csharp
using LabelSharpDesignerCore.PrintTransport.Windows;

// PDF via driver do Windows
new WindowsPdfPrintTransport { Copies = 1 }.Send(pdf, target: null); // null = impressora padrão

// Bytes crus (PPLA) direto no spooler
new WindowsRawPrintTransport().Send(pplaBytes, target: "Nome da impressora");

// Listar impressoras instaladas
IReadOnlyList<string> impressoras = new WindowsPrinterDiscovery().ListAvailable();
```

## Dependências

Não depende de nenhum outro projeto do LabelSharpDesignerCore — é um projeto "burro" no bom sentido, só
sabe mandar bytes para o Windows. Alvo `net48;net9.0-windows`: **exige Windows**, já que usa APIs
Win32 (`winspool.drv`) e do driver de impressão do sistema — mas roda tanto em .NET moderno quanto em
.NET Framework 4.6.1+ (ver [INTEGRATION.md](../../INTEGRATION.md)).
