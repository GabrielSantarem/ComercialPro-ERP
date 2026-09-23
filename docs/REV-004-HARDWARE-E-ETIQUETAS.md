# REV-004: Especificação Técnica e Diretrizes de Implementação da Fase 4 - Automação Comercial, Hardware & Etiquetas

| Metadado | Detalhe |
| :--- | :--- |
| **Identificador do Documento** | `REV-004` |
| **Título** | Especificação Técnica Executiva: Automação Comercial de Loja, Parser de Balanças EAN-13 (Prefixo 2), Gerador de Folhas de Etiquetas de Gôndola (QuestPDF), Pulso de Gaveta ESC/POS e Camada de Emulação de Periféricos |
| **Data de Emissão** | 2026-09-22 |
| **Data de Homologação** | 2026-09-22 |
| **Status** | Homologado e Concluído (100% Implementado) |
| **Documentos Relacionados** | [`ROADMAP_ERP_EVOLUCAO.md`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/docs/ROADMAP_ERP_EVOLUCAO.md), [`REV-001`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/docs/REV-001-AUDITORIA-E-REDESENHO.md), [`REV-002`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/docs/REV-002-TRIADE-CRITICA-IMPLEMENTACAO.md), [`REV-003`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/docs/REV-003-OPERACAO-COMERCIAL-E-INTELIGENCIA.md) |
| **Ambiente Alvo** | .NET 10 (C# 14), Avalonia UI 11.2, SQLite (EF Core 9), QuestPDF |

---

## 1. Contexto e Objetivo Estratégico

Com as fundações fiscais (`REV-002`) e de inteligência comercial (`REV-003`) consolidadas, o ERP necessita de **aderência total ao ambiente físico do varejo** (açougues, padarias, hortifrutis, minimercados e papelarias). 

O `REV-004` introduz a capacidade de:
1. Decompor etiquetas de peso e preço emitidas por balanças comerciais sem dependência de hardware proprietário;
2. Produzir folhas de etiquetas de gôndola/prateleira para precificação física em padrão gráfico profissional (QuestPDF);
3. Acionar eletromecanicamente gavetas de dinheiro via comandos padrão ESC/POS;
4. Fornecer uma **Arquitetura de Drivers Plugáveis com Modo Emulação (Mocking)** para que todo o sistema possa ser desenvolvido, testado e validado em ambiente local **sem custos com aquisição de equipamentos físicos**.

```
                 ┌─────────────────────────────────────────────────────────┐
                 │    REV-004: HARDWARE, BALANÇAS & ETIQUETAS DE GÔNDOLA   │
                 └────────────────────────────┬────────────────────────────┘
                                              │
         ┌────────────────────────────────────┼────────────────────────────────────┐
         │                                    │                                    │
         ▼                                    ▼                                    ▼
┌──────────────────┐                ┌──────────────────┐                ┌──────────────────┐
│  1. PARSER EAN-13│                │ 2. ETIQUETAS DE  │                │ 3. PERIFÉRICOS,  │
│     BALANÇAS (2) │                │    GÔNDOLA A4    │                │    GAVETA &      │
│  & BALANÇA SERIAL│                │    (QUESTPDF)    │                │    EMULADORES    │
├──────────────────┤                ├──────────────────┤                ├──────────────────┤
│ • Decodificação  │                │ • Folhas Pimaco  │                │ • Pulso RJ12     │
│   de peso e valor│                │   (ex: 6180/82)  │   ESC/POS gaveta │
│ • Suporte a preço│                │ • Código barras  │ • Driver Serial  │
│   ou peso no EAN │                │   EAN legível    │   Real vs Emulado│
│ • Protocolo Prix3│                │ • Preço à vista  │ • Testes 100%    │
│   / Filizola     │                │   e fracionado   │   sem hardware   │
└──────────────────┘                └──────────────────┘                └──────────────────┘
```

---

## 2. Especificação do Módulo 1: Parser de Balança (EAN-13 com Prefixo 2) e Balança de Checkout

### 2.1. O que deve ser feito
1. **Parser de Código de Barras Pesável (EAN-13 iniciado por `2`):**
   - No padrão GS1 Brasil / ABRAS para pesáveis na retaguarda, o código de 13 dígitos tem a estrutura:
     - `2` (Dígito 1): Identificador de pesável.
     - `CCCCC` (Dígitos 2 a 6 ou 2 a 5): Código interno do produto cadastrado no ERP.
     - `VVVVV` (Dígitos 7 a 11 ou 7 a 12): Carga útil, que pode representar o **Valor Total** (ex.: `01450` = R$ 14,50) ou o **Peso Líquido** (ex.: `01500` = 1,500 kg), dependendo da configuração da loja.
     - `D` (Último dígito): Dígito verificador EAN.
   - Ao bipar ou digitar um código iniciado por `2` no PDV, o sistema deve detectar automaticamente a estrutura, localizar o produto pelo código interno, calcular a quantidade/peso correspondente e lançar o item com precisão monetária.
2. **Interface de Leitura Direta de Balança de Checkout (Balança Serial):**
   - Criação do serviço de leitura de balança acoplada no caixa com tratamento dos protocolos Toledo Prix 3 e Filizola.
   - Implementação de driver real (porta serial COM/TTY) e **driver emulador (mock configurável)** para desenvolvimento e testes.

### 2.2. Assinatura de Código e Contratos
```csharp
namespace GetStartedApp.Services.Hardware;

public enum ModoCodigoBalanca
{
    ValorTotal, // Os dígitos representam o valor a pagar (R$)
    PesoLiquido // Os dígitos representam o peso em gramas/kg
}

public record ResultadoParserBalanca(
    bool IsCodigoBalanca,
    string CodigoProduto,
    decimal QuantidadeOuPeso,
    decimal ValorTotalCalculado,
    string MensagemErro = "");

public interface IBalancaEtiquetaParserService
{
    ResultadoParserBalanca DecodificarCodigo(
        string codigoBarras, 
        decimal precoUnitarioCadastro, 
        ModoCodigoBalanca modo = ModoCodigoBalanca.ValorTotal,
        int tamanhoCodigoProduto = 4);
}

public interface IBalancaCheckoutService
{
    Task<decimal> LerPesoAsync(CancellationToken cancellationToken = default);
    bool IsConectada { get; }
}
```

### 2.3. Tratamento de Casos Extremos (Edge Cases)
* **Preço Zero no Cadastro:** Se o produto lido pelo código interno estiver com `PrecoVenda == 0`, acusar erro impeditivo de lançamento no PDV.
* **Tamanho Incorreto do Código:** Se o código iniciado por `2` possuir menos ou mais de 12/13 dígitos, rejeitar o parser e tratar como código alfanumérico comum.
* **Divisão por Zero / Fração Inválida:** No modo `ValorTotal`, calcular `Quantidade = Math.Round(ValorTotal / PrecoUnitario, 3)`. Garantir que `PrecoUnitario > 0`.
* **Divergência de Arredondamento:** Ajustar o subtotal do item na venda para bater exatamente com o valor impresso na etiqueta da balança (`ValorTotal = PrecoUnitario * Quantidade`).

### 2.4. Casos de Teste xUnit Obrigatórios
1. `DecodificarCodigo_ModoValorTotal_DeveCalcularPesoCorretoParaProduto()`
2. `DecodificarCodigo_ModoPesoLiquido_DeveExtrairQuantidadeEPrecoTotalCorretos()`
3. `DecodificarCodigo_ComPrecoCadastroZerado_DeveRetornarErro()`
4. `DecodificarCodigo_CodigoSemPrefixo2_DeveIdentificarComoNaoBalanca()`
5. `DecodificarCodigo_TamanhoInvalido_NaoDeveDispararExcecao()`
6. `BalancaEmulada_DeveRetornarPesoSimuladoEstavel()`

---

## 3. Especificação do Módulo 2: Gerador de Folhas de Etiquetas de Gôndola (QuestPDF)

### 3.1. O que deve ser feito
Implementar gerador profissional de etiquetas de prateleira/gôndola em formato **PDF A4** utilizando a infraestrutura existente do **QuestPDF**. O gerador deve atender modelos consagrados de papel adesivo (ex.: **Pimaco 6180** com 3 colunas $\times$ 10 linhas = 30 etiquetas, e **Pimaco 6182** com 2 colunas $\times$ 7 linhas = 14 etiquetas).

### 3.2. Estrutura Visual da Etiqueta de Gôndola
Cada etiqueta gerada deve conter:
1. **Nome do Produto** (tipografia bold, até 2 linhas com corte inteligente);
2. **Código Interno & Código de Barras (EAN-13)** renderizado visualmente em barras e texto legível;
3. **Preço de Venda à Vista** em destaque com fonte extra-grande (`R$ XX,XX`);
4. **Preço por Unidade Fracionada** (ex.: `Preço por KG: R$ XX,XX` ou `Preço por LITRO: R$ XX,XX`) — exigência legal do Código de Defesa do Consumidor;
5. **Data da Emissão / Alteração de Preço** (para controle do operador de loja).

### 3.3. Assinatura de Código e Contratos
```csharp
namespace GetStartedApp.Services.Etiquetas;

public record ModeloFolhaEtiqueta(
    string Nome,
    int Colunas,
    int Linhas,
    float MargemSuperiorMm,
    float MargemLateralMm,
    float LarguraEtiquetaMm,
    float AlturaEtiquetaMm,
    float EspacamentoHorizontalMm,
    float EspacamentoVerticalMm);

public record ItemEtiquetaGondolaDto(
    int ProdutoId,
    string Nome,
    string CodigoBarras,
    string CodigoInterno,
    decimal PrecoVenda,
    string UnidadeMedida,
    decimal? PrecoPorQuiloOuLitro = null);

public interface IEtiquetaGondolaService
{
    byte[] GerarFolhaEtiquetasPdf(
        List<ItemEtiquetaGondolaDto> produtos, 
        ModeloFolhaEtiqueta modelo);
        
    List<ModeloFolhaEtiqueta> ObterModelosSuportados();
}
```

### 3.4. Modelos Padrão Nativos
* **Pimaco 6180 / Colacril:** Folha A4, 30 etiquetas (3 colunas $\times$ 10 linhas), tamanho 66,7 mm $\times$ 25,4 mm.
* **Pimaco 6182:** Folha A4, 14 etiquetas (2 colunas $\times$ 7 linhas), tamanho 101,6 mm $\times$ 38,1 mm (ideal para destaque com código de barras expandido).

### 3.5. Tratamento de Casos Extremos (Edge Cases)
* **Texto de Nome Longo:** Não estourar a célula da etiqueta; truncar ou ajustar o `FontSize` proporcionalmente.
* **Produto Sem Código de Barras:** Se o produto não possui EAN, exibir apenas o código interno e aviso legível no local do código de barras.
* **Múltiplas Páginas:** O gerador deve paginar automaticamente ao exceder o número de etiquetas por folha (ex.: 45 produtos em modelo de 30 etiquetas geram 2 páginas A4).
* **Produtos Repetidos para Múltiplas Cópias:** Permitir especificar a quantidade de etiquetas a imprimir para o mesmo item (ex.: 5 etiquetas de Sabão em Pó para diferentes prateleiras).

### 3.6. Casos de Teste xUnit Obrigatórios
1. `GerarFolhaEtiquetasPdf_ComListaProdutos_DeveRetornarBytesValidos()`
2. `GerarFolhaEtiquetasPdf_MaisProdutosQueCapacidadeFolha_DeveCriarMultiplasPaginas()`
3. `GerarFolhaEtiquetasPdf_ProdutoSemEan_DeveImprimirSemQuebra()`
4. `ObterModelosSuportados_DeveConterModelosPimacoPadrao()`

---

## 4. Especificação do Módulo 3: Pulso de Gaveta ESC/POS e Camada de Emulação de Periféricos

### 4.1. O que deve ser feito
1. **Acionamento Automático de Gaveta de Dinheiro:**
   - Adicionar comando elétrico de abertura de gaveta via porta RJ12 da impressora térmica.
   - Padrão ESC/POS de pulso de gaveta: byte array `[ 0x1B, 0x70, 0x00, 0x19, 0xFA ]` (`ESC p m t1 t2`).
   - Disparo automático configurável ao finalizar vendas com a forma de pagamento **Dinheiro** ou ao realizar operações de **Suprimento/Sangria**.
2. **Camada de Emulação e Virtualização de Hardware (Sem Custo):**
   - Disponibilizar `BalancaMockService` que gera pesos plausíveis com oscilação controlada ou leitura estática configurável para testes e demonstrações.
   - Adicionar opção nas configurações: `Modo Emulação / Demonstração` habilitada por padrão em ambientes de teste.

### 4.2. Assinatura de Código e Contratos
```csharp
namespace GetStartedApp.Services.Hardware;

public interface IGavetaDinheiroService
{
    byte[] ObterComandoAberturaGaveta();
    Task<bool> AcionarAberturaAsync(string portaOuImpressora);
}

public class BalancaMockService : IBalancaCheckoutService
{
    public decimal PesoConfigurado { get; set; } = 1.250m;
    public bool IsConectada => true;
    public Task<decimal> LerPesoAsync(CancellationToken cancellationToken = default) 
        => Task.FromResult(PesoConfigurado);
}
```

### 4.3. Casos de Teste xUnit Obrigatórios
1. `ObterComandoAberturaGaveta_DeveConterBytesPadraoEscPos()`
2. `CupomTermico_ComAberturaGavetaHabilitada_DeveAnexarPulsoNaFinalizacaoEmDinheiro()`
3. `BalancaMockService_DeveFornecerPesoEstavelSemDependerDePortaSerialFisica()`

---

## 5. Requisitos Não-Funcionais e Ergonomia Zero Mouse

1. **Desempenho de Renderização PDF:** A compilação da folha A4 com 30 ou 60 etiquetas via QuestPDF deve ocorrer em menos de 400 milissegundos.
2. **Integração no PDV Sem Mouse:** 
   - Ao digitar um código de balança de 13 dígitos iniciado por `2` no campo de busca do PDV e pressionar `[ENTER]`, o produto deve ser lançado instantaneamente sem caixas de diálogo adicionais.
   - Tecla de atalho `[F3]` ou botão rápido no PDV para acionar a leitura da balança de checkout (captura do peso em tempo real).
3. **Persistência de Preferências:** Atualizar [`ConfiguracaoTerminal.cs`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/Models/ConfiguracaoTerminal.cs) com as novas propriedades:
   - `ModoBalancaEtiqueta` (`ValorTotal` ou `PesoLiquido`);
   - `AcionarGavetaAutomaticamente` (`true`/`false`);
   - `UsarEmuladorBalanca` (`true`/`false`).

---

## 6. Requisitos Técnicos para Homologação e Aprovação

A entrega será considerada homologada quando:
1. **Compilação Limpa:** `dotnet build` sem erros e sem warnings.
2. **Bateria de Testes:**
   - Todos os **226 testes existentes** devem permanecer passando com 100% de sucesso.
   - Devem ser adicionados no mínimo **13 novos testes unitários** dedicados aos módulos do REV-004 (total esperado: $\ge 239$ testes).
3. **Injeção de Dependências:** Serviços registrados como Singletons em [`App.axaml.cs`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/App.axaml.cs).
4. **Independência de Hardware:** 100% dos testes e fluxos devem rodar em ambiente limpo sem nenhum periférico físico conectado.

---

## 7. Relatório de Homologação e Conclusão da Execução

Todas as diretrizes do documento REV-004 foram integralmente implementadas e homologadas na base de código:

1. **Módulo 1 - Parser de Balança (Prefixo 2) e Balança de Checkout:**
   - Implementado em [`BalancaEtiquetaParserService.cs`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/Services/Hardware/BalancaEtiquetaParserService.cs) e [`BalancaMockService.cs`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/Services/Hardware/BalancaMockService.cs).
   - Suporte aos modos `ValorTotal` e `PesoLiquido` com configuração dinâmica de dígitos (4 a 6).
   - Atalho Zero Mouse `[F3]` adicionado no PDV ([`PdvView.axaml.cs`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/Views/PdvView.axaml.cs)) para leitura imediata de balança.
   - Lançamento inteligente por scanner de código pesável integrado em [`PdvViewModel.cs`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/ViewModels/PdvViewModel.cs).

2. **Módulo 2 - Gerador de Folhas de Etiquetas de Gôndola (QuestPDF):**
   - Implementado em [`EtiquetaGondolaService.cs`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/Services/Etiquetas/EtiquetaGondolaService.cs) e [`IEtiquetaGondolaService.cs`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/Services/Etiquetas/IEtiquetaGondolaService.cs).
   - Suporte nativo aos modelos Pimaco 6180 (30 etiquetas por folha) e Pimaco 6182 (14 etiquetas por folha).
   - Exibição de preço à vista, código legível e preço fracionado por KG/Litro (exigência do CDC).

3. **Módulo 3 - Pulso de Gaveta ESC/POS e Emulação:**
   - Implementado em [`GavetaDinheiroService.cs`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/Services/Hardware/GavetaDinheiroService.cs) e [`CupomTermicoService.cs`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/Services/Impressao/CupomTermicoService.cs).
   - Pulso elétrico ESC/POS (`0x1B, 0x70, 0x00, 0x19, 0xFA`) anexado automaticamente no buffer em finalizações em dinheiro.

4. **Bateria de Testes Automatizados:**
   - Arquivo [`HardwareEtiquetasTests.cs`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/tests/GetStartedApp.Tests/HardwareEtiquetasTests.cs) com **17 novos testes unitários**.
   - Total de testes da solução saltou de 226 para **243 testes**, todos aprovados com **100% de êxito**.
