# REV-003: Especificação Técnica e Diretrizes de Implementação da Operação Comercial & Inteligência de Estoque

| Metadado | Detalhe |
| :--- | :--- |
| **Identificador do Documento** | `REV-003` |
| **Título** | Especificação Técnica Executiva: Trocas & Devoluções (Vale-Crédito), Gestão Cadastral de Clientes com Limite de Fiado e Inteligência Comercial (Sem Giro & Comissões) |
| **Data de Emissão** | 2026-09-18 |
| **Status** | Aprovado para Implementação / Handoff Técnico para o Agente Executor |
| **Documentos Relacionados** | [`REV-001`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/docs/REV-001-AUDITORIA-E-REDESENHO.md), [`REV-002`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/docs/REV-002-TRIADE-CRITICA-IMPLEMENTACAO.md), [`GOALS_ERP.md`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/GOALS_ERP.md), [`DOCUMENTACAO_SISTEMA.md`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/DOCUMENTACAO_SISTEMA.md) |
| **Ambiente Alvo** | .NET 10 (C# 14), Avalonia UI 11.2, SQLite (EF Core 9 / Microsoft.Data.Sqlite) |

---

## 1. Contexto e Seleção Estratégica da Segunda Tríade

Com a segurança e a conformidade fiscal garantidas pelo `REV-002` (Cancelamento NFC-e, Fechamento Fiscal .ZIP e Backup SQLite), o ERP agora requer maturidade na **rotina comercial da loja e inteligência de caixa**. 

A tríade de evolução do `REV-003` aborda as 3 maiores lacunas funcionais da operação de varejo:

```
                 ┌─────────────────────────────────────────────────────────┐
                 │       REV-003: TRÍADE OPERACIONAL E DE INTELIGÊNCIA     │
                 └────────────────────────────┬────────────────────────────┘
                                              │
         ┌────────────────────────────────────┼────────────────────────────────────┐
         │                                    │                                    │
         ▼                                    ▼                                    ▼
┌──────────────────┐                ┌──────────────────┐                ┌──────────────────┐
│  1. TROCAS E     │                │ 2. CLIENTES &    │                │ 3. SEM GIRO &    │
│     DEVOLUÇÕES   │                │    CREDIÁRIO COM │                │    COMISSÕES     │
│  (VALE-CRÉDITO)  │                │    LIMITES REAIS │                │   DE ATENDENTES  │
├──────────────────┤                ├──────────────────┤                ├──────────────────┤
│ Reincorporação ao│                │ Entidade mestre  │                │ Capital parado   │
│ estoque e emissão│                │ com trava de     │                │ (sem venda há    │
│ de vale para uso │                │ inadimplência    │                │ 60/90 dias) e    │
│ no checkout PDV  │                │ e limite no PDV  │                │ comissão no DRE  │
└──────────────────┘                └──────────────────┘                └──────────────────┘
```

---

## 2. Especificação do Módulo 1: Trocas, Devoluções & Emissão de Vale-Crédito

### 2.1. O que deve ser feito
Implementar o fluxo formal de **Trocas e Devoluções de Mercadorias**, permitindo que o operador de caixa ou atendente de balcão receba itens devolvidos pelo cliente, estorne automaticamente os itens ao estoque físico e gere uma entidade e cupom de **Vale-Crédito (Vale-Troca)** com código alfanumérico único. No PDV, o Vale-Crédito deve ser aceito como forma de abatimento ou quitação no fechamento da venda.

### 2.2. Comportamento e Fluxo Operacional (UI/UX)
1. **Ponto de Entrada:** No PDV via atalho dedicado `[F10] Trocas & Vales` ou na tela de Gestão de Vendas.
2. **Modal de Devolução:**
   - Permite informar o número da Venda original (opcional) ou selecionar produtos avulsos devolvidos.
   - Lista os itens devolvidos com quantidade, preço unitário e motivo (ex.: "Defeito / Avaria", "Tamanho Inadequado", "Desistência").
   - Opção: *Destino da Mercadoria*:
     - **Estoque Comercial Disponível** (reincorpora saldo para nova venda imediata).
     - **Quarentena / Avaria** (registra saída por perda/defeito sem disponibilizar para venda).
   - Identificação do Cliente (Nome e CPF opcional ou obrigatório para vales acima de R$ 100,00).
3. **Desfecho da Emissão:**
   - Gera a entidade `ValeCredito` no banco de dados.
   - Imprime o comprovante de Vale-Crédito térmico (ESC/POS 80mm/58mm) contendo: Código de Barras / Token do Vale (ex.: `VALE-2026-X89J2`), Valor, Data de Validade (default: 30 dias) e Regras de Uso.
4. **Liquidação no Checkout do PDV:**
   - No modal de fechamento de venda `[F5]`, o operador pode selecionar a forma de pagamento `[V] Vale-Crédito`.
   - Um campo solicita o código do Vale.
   - O sistema valida a autenticidade, saldo disponível e validade do cupom:
     - Se o valor da compra for **maior** que o vale: O saldo do vale é zerado e o restante é pago em dinheiro/cartão/PIX.
     - Se o valor da compra for **menor** que o vale: O vale é atualizado com o saldo remanescente para compras futuras.

### 2.3. Modelagem e Assinaturas de Código Esperadas
```csharp
namespace GetStartedApp.Models;

public class ValeCredito
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty; // Ex: VALE-2026-XXXXX
    public decimal ValorOriginal { get; set; }
    public decimal SaldoDisponivel { get; set; }
    
    public int? VendaOrigemId { get; set; }
    public int? ClienteId { get; set; }
    public string? ClienteNome { get; set; }
    public string? ClienteCpf { get; set; }
    
    public DateTime DataEmissao { get; set; } = DateTime.Now;
    public DateTime DataValidade { get; set; } = DateTime.Now.AddDays(30);
    public DateTime? DataUtilizacaoTotal { get; set; }
    
    public string Status { get; set; } = "ATIVO"; // ATIVO, UTILIZADO, EXPIRADO, CANCELADO
    public string MotivoDevolucao { get; set; } = string.Empty;
}

public record ItemDevolucaoDto(int ProdutoId, int Quantidade, decimal PrecoUnitario, bool DestinarAvaria, string Motivo);

public interface ITrocaDevolucaoService
{
    Task<ValeCredito> EmitirValeTrocaAsync(
        List<ItemDevolucaoDto> itensDevolvidos,
        string motivoGeral,
        string? clienteNome,
        string? clienteCpf,
        int? vendaOrigemId = null);

    Task<(bool Sucesso, string Mensagem, decimal ValorAbatido)> ResgatarValeCreditoAsync(
        string codigoVale,
        decimal valorNecessario,
        int vendaDestinoId);
        
    Task<ValeCredito?> ConsultarValeAsync(string codigoVale);
}
```

### 2.4. Tratamento de Erros e Casos Extremos (Edge Cases)
* **Vale com Saldo Insuficiente ou Já Utilizado:** Bloqueio imediato com aviso *"Vale-Crédito já liquidado integralmente em DD/MM/AAAA"*.
* **Vale Expirado:** Se `DataValidade < DateTime.Now`, acusar *"Vale-Crédito expirado em DD/MM/AAAA. Revalidação requer autorização de supervisor."*
* **Devolução com Quantidade Maior que a Vendida:** Se informada a venda de origem, validar se a quantidade devolvida não excede o total faturado no cupom fiscal original.
* **Resgate Concorrente:** Atualização de saldo com controle de concorrência atômico para impedir que o mesmo vale seja passado em dois caixas simultâneos.

### 2.5. Casos Básicos de Teste a Cobrir (xUnit)
1. `EmitirValeTroca_ComProdutosValidos_DeveAtualizarEstoque_E_GerarCodigoUnico()`
2. `EmitirValeTroca_ComDestinoAvaria_DeveRegistrarPerdaSemIncrementarEstoqueVenda()`
3. `ResgatarValeCredito_ComValorMenorQueCompra_DeveZerarSaldoVale_E_RetornarAbatimento()`
4. `ResgatarValeCredito_ComValorMaiorQueCompra_DeveManterSaldoRestanteNoVale()`
5. `ResgatarValeCredito_ExpiradoOuJaUtilizado_DeveRecusarOperacao()`

---

## 3. Especificação do Módulo 2: Gestão Cadastral de Clientes & Limites de Fiado

### 3.1. O que deve ser feito
Transformar o cliente em uma **Entidade Mestre Estruturada (`Cliente`)** no banco de dados SQLite, eliminando o preenchimento de texto solto. O cadastro deve conter validação de documento (CPF/CNPJ), limite de crédito pré-aprovado para Crediário ("Fiado"), e trava automática no PDV que impede novas vendas a prazo caso o cliente atinja seu teto ou possua parcelas vencidas em atraso.

### 3.2. Comportamento e Fluxo Operacional (UI/UX)
1. **Nova Aba ou Painel de Clientes:**
   - Tela de gestão de clientes com busca instantânea por Nome, CPF/CNPJ ou Telefone.
   - Formulário com: `Razão Social / Nome Completo`, `CPF / CNPJ` (com formatação e cálculo do dígito verificador), `Telefone / WhatsApp`, `Endereço`, `Limite de Crédito Fiado (R$)` e `Status` (ATIVO, BLOQUEADO).
2. **Consulta Rápida no PDV / Balcão:**
   - Ao selecionar cliente no checkout (`[F2]` ou identificação na venda fiada), o sistema exibe um card com:
     - Limite Total: `R$ 500,00`
     - Saldo Utilizado (Contas em Aberto): `R$ 320,00`
     - Saldo Disponível: `R$ 180,00`
     - Títulos em Atraso: `0 títulos (Em dia)` ou `⚠️ 1 título vencido há 8 dias`
3. **Políticas de Bloqueio Automático:**
   - **Regra 1 (Excesso de Limite):** Se a compra ultrapassar o saldo disponível, o sistema bloqueia a finalização no Crediário com a mensagem: *"Limite de crédito insuficiente. Disponível: R$ 180,00, Tentativa: R$ 220,00."*
   - **Regra 2 (Inadimplência):** Se o cliente tiver qualquer fatura vencida há mais de $N$ dias (default: 5 dias), a venda no fiado é bloqueada até a quitação das pendências.
   - **Liberação Gerencial:** Um supervisor pode desbloquear a venda inserindo sua senha master.

### 3.3. Modelagem e Assinaturas de Código Esperadas
```csharp
namespace GetStartedApp.Models;

public class Cliente
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string CpfCnpj { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    
    public decimal LimiteCredito { get; set; } = 0m;
    public bool BloqueadoManualmente { get; set; } = false;
    public string? MotivoBloqueio { get; set; }
    
    public DateTime DataCadastro { get; set; } = DateTime.Now;
}

public record StatusCreditoClienteDto(
    int ClienteId,
    string Nome,
    decimal LimiteTotal,
    decimal SaldoDevedor,
    decimal LimiteDisponivel,
    int QuantidadeTitulosVencidos,
    bool AptoParaCrediario,
    string? MotivoRestricao);

public interface IClienteService
{
    Task<List<Cliente>> PesquisarClientesAsync(string termo);
    Task<Cliente> SalvarClienteAsync(Cliente cliente);
    Task<StatusCreditoClienteDto> AvaliarCreditoAsync(int clienteId, decimal valorNovaCompra);
}
```

### 3.4. Tratamento de Erros e Casos Extremos (Edge Cases)
* **CPF / CNPJ Inválido:** Algoritmo de validação de dígitos verificadores padrão Receita Federal (rejeita CPFs fictícios como `111.111.111-11`).
* **Vínculo com Contas a Receber Existentes:** Ao cadastrar um cliente cujo CPF já constava em contas a receber históricas, o sistema deve unificar o histórico financeiro.
* **Cliente Sem Limite de Crédito:** Se `LimiteCredito == 0`, o sistema não autoriza vendas fiadas para ele por padrão, a menos que autorizado pelo gerente.

### 3.5. Casos Básicos de Teste a Cobrir (xUnit)
1. `SalvarCliente_ComCpfInvalido_DeveLancarExcecaoValidacao()`
2. `AvaliarCredito_ComValorAbaixoDoLimiteESemAtrasos_DeveAutorizarCompra()`
3. `AvaliarCredito_ComValorExcedendoLimiteDisponivel_DeveRecusarOperacao()`
4. `AvaliarCredito_ComTitulosVencidosHaMaisDe5Dias_DeveBloquearPorInadimplencia()`
5. `SalvarCliente_DuplicidadeCpf_DeveBloquearCadastro()`

---

## 4. Especificação do Módulo 3: Relatório de Produtos Sem Giro & Comissões de Atendentes

### 4.1. O que deve ser feito
Implementar duas ferramentas essenciais de inteligência para o comerciante:
1. **Radar de Estoque Sem Giro (Capital Imobilizado):** Mapeamento analítico de produtos com estoque positivo que não tiveram nenhuma venda nos últimos 30, 60 ou 90+ dias, calculando o valor financeiro do capital estagnado na prateleira.
2. **Gestão e Apuração de Comissões de Vendedores:** Inclusão de percentual de comissão padrão por vendedor (ou por categoria) e cálculo automático da remuneração variável no período com base nas vendas efetivamente faturadas.

### 4.2. Comportamento e Fluxo Operacional (UI/UX)
1. **Visualização no Módulo de Dashboard / Inteligência:**
   - **Aba "Estoque Sem Giro":**
     - Filtros de corte: `[30 Dias]`, `[60 Dias]`, `[90+ Dias]`.
     - Indicador destacado no topo (KPI Card): *"Capital Total Parado: R$ 14.850,00 em 42 produtos"*.
     - Grade de itens: Código, Descrição, Saldo em Estoque, Custo Unitário, Capital Parado (Qtd $\times$ Custo) e Data da Última Venda registrada.
     - Botão `[Exportar CSV / Relatório]`.
   - **Aba "Comissões & Equipe":**
     - Seleção de período (Início e Fim).
     - Tabela por Vendedor: Vendedor, Total de Vendas Concluídas, Faturamento Bruto, Alíquota de Comissão (`%`), Total de Comissão a Pagar (`R$`).
     - Integração automática com a DRE Gerencial existente (`PdvService.Inteligencia.cs`), descontando as comissões como despesa variável da operação.

### 4.3. Modelagem e Assinaturas de Código Esperadas
```csharp
namespace GetStartedApp.Services;

public record ItemProdutoSemGiroDto(
    int ProdutoId,
    string Nome,
    string CodigoBarras,
    int SaldoEstoque,
    decimal CustoUnitario,
    decimal CapitalParadoTotal,
    DateTime? DataUltimaVenda,
    int DiasSemGiro);

public record ComissaoVendedorDto(
    int VendedorId,
    string VendedorNome,
    decimal PercentualComissao,
    int TotalVendas,
    decimal FaturamentoTotal,
    decimal ValorComissaoTotal);

public interface IInteligenciaComercialService
{
    Task<List<ItemProdutoSemGiroDto>> ObterProdutosSemGiroAsync(int diasMinimosSemVenda = 60);
    Task<List<ComissaoVendedorDto>> CalcularComissoesAsync(DateTime inicio, DateTime fim);
}
```

### 4.4. Tratamento de Erros e Casos Extremos (Edge Cases)
* **Produtos Cadastrados Recentemente:** Um produto recém-cadastrado (há 5 dias) não deve ser classificado como "sem giro há 60 dias" só porque não vendeu. A consulta deve considerar `MIN(DataUltimaVenda, DataCadastro)`.
* **Produtos com Saldo Zerado:** Produtos esgotados (estoque 0) não podem figurar no relatório de capital parado.
* **Vendas Canceladas:** Vendas estornadas ou canceladas (inclusive via `REV-002`) não podem ser contabilizadas para fins de cálculo de comissão.

### 4.5. Casos Básicos de Teste a Cobrir (xUnit)
1. `ObterProdutosSemGiro_ComProdutoSemVendaHaMaisDe60Dias_DeveListarComCalculoDeCapitalParado()`
2. `ObterProdutosSemGiro_ComProdutoVendidoRecentemente_NaoDeveListar()`
3. `ObterProdutosSemGiro_ComEstoqueZerado_NaoDeveConsiderarCapitalParado()`
4. `CalcularComissoes_DeveIgnorarVendasCanceladas()`
5. `CalcularComissoes_DeveAplicarPercentualParametrizadoPorVendedor()`

---

## 5. Requisitos Técnicos para Homologação e Aprovação

Para que a entrega do agente executor seja considerada aprovada:

1. **Compilação sem Falhas nem Warnings:**
   - A solução deve compilar integralmente com `dotnet build` sem nenhum warning de nulabilidade ou compilação.
2. **Manutenção dos Testes Existentes + Novos Testes:**
   - Todos os **208 testes existentes** devem continuar passando sem regressões.
   - Devem ser adicionados ao menos **15 novos testes unitários** cobrindo os cenários descritos nas seções 2.5, 3.5 e 4.5 (total esperado: $\ge 223$ testes).
3. **Injeção de Dependências:**
   - Registrar `ITrocaDevolucaoService`, `IClienteService` e `IInteligenciaComercialService` em [`App.axaml.cs`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/App.axaml.cs).
4. **Ergonomia de Teclado (Zero Mouse):**
   - Modais no PDV devem manter suporte a `[ESC]` para retorno e `[ENTER]` para submissão, com foco automático em campos de texto.

---
*Documento homologado pelo Revisor Técnico. Pronto para handoff e execução imediata.*
