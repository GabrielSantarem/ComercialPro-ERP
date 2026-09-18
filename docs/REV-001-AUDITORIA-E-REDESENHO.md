# REV-001: Auditoria Técnica de Módulos, Inconsistências e Diretrizes de Redesenho do ERP

| Metadado | Detalhe |
| :--- | :--- |
| **Identificador do Documento** | `REV-001` |
| **Título** | Relatório de Auditoria Pente-Fino e Especificação Técnica para Refatoração do ERP |
| **Data de Emissão** | 2026-09-18 |
| **Status** | ✅ 100% Concluído e Validado (191 Testes Aprovados, 0 Falhas) |
| **Projeto** | Comercial Pro ERP (`/home/tomate/Lixeira/dotnet/C#/GetStartedApp`) |
| **Stack Tecnológica** | .NET 10 (C# 14), Avalonia UI 11.2 (FluentTheme), EF Core 9 / SQLite, CommunityToolkit.Mvvm, Zeus Automação |
| **Cobertura Atual** | 191 Testes Automatizados (Unitários, Integração e Avalonia Headless UI) |

---

## 1. Objetivo deste Documento

Este documento formalizou o diagnóstico de revisão técnica realizada no ERP comercial e serviu como plano diretor para a refatoração completa do sistema. Todos os apontamentos foram integralmente implementados e cobertos por testes automatizados (unitários, integração e testes visuais headless).

---

## 2. Mapa Pente-Fino de Inconsistências por Módulo

### 2.1. Módulo Configurações
- **Status Anterior**: Apenas campos fiscais eram gravados. As opções de impressora de cupom térmico e leitor/balança eram mocks estáticos.
- **Resolução**: Criada a entidade `ConfiguracaoTerminal`, persistência no SQLite, dropdowns dinâmicos de portas/largura e integração com emissão de cupom de teste via `CupomTermicoService`.

### 2.2. Módulo Entrada de NF-e (Compras)
- **Status Anterior**: Carregava dados mas utilizava `ListBox` limitada e não permitia editar preço de venda ou calcular markup.
- **Resolução**: Substituído por `DataGrid` com colunas de Markup (%) e Novo Preço de Venda, com recálculo automático bidirecional e bloqueio de gravação caso haja preço de venda zero ou negativo.

### 2.3. Módulo Estoque
- **Status Anterior**: Possuía uma "Aba 2: Entrada Manual" redundante e simplista.
- **Resolução**: Aba redundante removida, unificando a entrada de mercadorias no módulo oficial com atalho direto e rastreabilidade total.

### 2.4. Módulo Balcão / Pré-Venda
- **Status Anterior**: Modal de simulação travava a velocidade de atendimento do operador.
- **Resolução**: Venda concluída e comanda impressa de forma direta com notificação fluida e foco imediato restaurado no campo de busca para o próximo cliente.

### 2.5. Módulo PDV / Checkout
- **Status Anterior**: Sem suporte a pagamento misto (Split Payment) e sem atalho de teclado para fechar o comprovante NFC-e.
- **Resolução**: Implementado Split Payment completo com tabela de parcelas, validação de total pendente e atalhos `[ENTER]`/`[ESC]` no comprovante fiscal.

### 2.6. Módulo Fiscal
- **Status Anterior**: NCM, CFOP e CSOSN utilizavam fallbacks fixos no XML da NFC-e.
- **Resolução**: Vinculação estrita aos metadados fiscais cadastrados em cada item do produto.

### 2.7. Navegação e Atalhos Globais
- **Status Anterior**: Conflitos entre atalhos de janelas e o PDV.
- **Resolução**: `MainWindow` desobstruída para permitir interceptação hierárquica e navegação segura por abas e modais.

---

## 3. Matriz de Prioridades Técnicas (Plano de Ação)

| ID | Prioridade | Módulo | Descrição da Ação | Status | Validação / Testes |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **ACT-01** | 🔴 Crítica | Fiscal / PDV | Utilizar NCM, CFOP e CSOSN reais do cadastro do produto ao emitir NFC-e. | ✅ Concluído | `FiscalServiceTests.cs`, `PdvNfceIntegrationTests.cs` |
| **ACT-02** | 🔴 Crítica | Navegação | Remover atalhos conflitantes em `MainWindow.axaml.cs` preservando o fluxo do PDV. | ✅ Concluído | `MainWindow.axaml.cs`, navegação limpa |
| **ACT-03** | 🔴 Crítica | Gerencial | Corrigir cálculo de patrimônio no Dashboard multiplicando por `CustoUltimaCompra`. | ✅ Concluído | `DashboardServiceTests.cs` |
| **ACT-04** | 🟠 Alta | PDV / Zero Mouse | Implementar `[ENTER]`/`[ESC]` no PDV para fechar modal de comprovante NFC-e. | ✅ Concluído | `AvaloniaHeadlessTests.cs` |
| **ACT-05** | 🟠 Alta | PDV / Checkout | Implementar pagamento misto (Split Payment com abates sucessivos). | ✅ Concluído | `SplitPaymentTests.cs`, `AvaloniaHeadlessTests.cs` |
| **ACT-06** | 🟠 Alta | NF-e / Compras | Implementar DataGrid com Markup (%) e Preço de Venda na Entrada de NF-e. | ✅ Concluído | `EntradaNfeTests.cs`, `EntradaMercadoriaTests.cs` |
| **ACT-07** | 🟡 Média | Balcão | Desobstruir fluxo do operador liberando o balcão imediatamente após emitir comanda. | ✅ Concluído | `PedidoBalcaoTests.cs`, `AvaloniaHeadlessTests.cs` |
| **ACT-08** | 🟡 Média | Estoque | Remover aba redundante de entrada manual em `EstoqueView.axaml`. | ✅ Concluído | `EstoqueView.axaml`, `EstoqueViewModel.cs` |
| **ACT-09** | 🟢 Baixa | Configurações | Conectar configurações de impressora térmica e balança à tabela `ConfiguracaoTerminal`. | ✅ Concluído | `ConfiguracoesTerminalTests.cs`, `AvaloniaHeadlessTests.cs` |

---

## 4. Especificações Técnicas Implementadas

### 4.1. Split Payment no Checkout do PDV
- Lista de pagamentos parciais vinculada reativamente (`PagamentosAdicionados`).
- Notificação dinâmica de `ValorRestante`, `PodeConfirmarPagamento` e troco.
- Atalho `[F3]` adiciona a parcela atual; `[ENTER]` confirma quando `ValorRestante == 0`.

### 4.2. DataGrid com Markup e Validação na Entrada de Mercadoria
- Cada item lido do XML ou inserido manualmente calcula automaticamente o `NovoPrecoVenda` baseado no `MarkupPercentual` (ou o inverso).
- Bloqueio rígido: `PodeConcluirEntrada` retorna falso se algum item possuir Preço de Venda nulo ou zero.

### 4.3. Configuração de Hardware e Terminal
- Tabela `ConfiguracoesTerminal` com defaults pré-configurados para evitar nulos.
- Suporte a modelos ESC/POS (80mm e 58mm), portas COM e corte automático de papel.
- Geração e pré-visualização de cupom de teste via modal.

### 4.4. Suíte de Testes Avalonia Headless
- Testes automatizados executando o loop de eventos e visual tree do Avalonia sem necessidade de servidor gráfico (`Avalonia.Headless.XUnit`).
- Verificação de foco de controles, atalhos de teclado de função (`F1`, `F12`, `ESC`, `ENTER`) e sincronização bidirecional de dados.
