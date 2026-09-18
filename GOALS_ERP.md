# MAPA ESTRATÉGICO & GOALS DO ERP (ARQUITETURA COMPLETA)

Este documento define a taxonomia, arquitetura funcional e a régua de maturidade (Goals) de um ERP moderno, robusto e escalável para comércio varejista/atacadista de pequeno e médio porte.

---

## 1. Visão Geral dos Módulos do ERP

```
                        ┌──────────────────────────────────────────────┐
                        │              CORE ERP ENGINE                 │
                        │ (Entidades, Auditoria, Permissões, Banco DB) │
                        └──────────────────────┬───────────────────────┘
                                               │
    ┌──────────────────────┬───────────────────┼───────────────────┬──────────────────────┐
    │                      │                   │                   │                      │
┌───▼─────────────┐ ┌──────▼──────────┐ ┌──────▼──────────┐ ┌──────▼──────────┐ ┌─────────▼────────┐
│ 1. COMERCIAL    │ │ 2. ESTOQUE &    │ │ 3. FINANCEIRO   │ │ 4. FISCAL &     │ │ 5. GESTÃO &      │
│    & VENDAS     │ │    COMPRAS      │ │                 │ │    COMPLIANCE   │ │    RELATÓRIOS    │
├─────────────────┤ ├─────────────────┤ ├─────────────────┤ ├─────────────────┤ ├──────────────────┤
│ • Balcão (Pré)  │ │ • Entrada XML   │ │ • Contas Pagar  │ │ • NF-e / NFC-e  │ │ • DRE Real       │
│ • Caixa Central │ │ • Custo Real    │ │ • Contas Receber│ │ • Regras CFOP   │ │ • Curva ABC      │
│ • Crediário     │ │ • Curva Giro    │ │ • Fluxo Caixa   │ │ • Impostos/CST  │ │ • Ticket Médio   │
│ • Devoluções    │ │ • Inventário/Aud│ │ • Conciliação   │ │ • SPED Fiscal   │ │ • Metas Vendas   │
└─────────────────┘ └─────────────────┘ └─────────────────┘ └─────────────────┘ └──────────────────┘
```

---

## 2. Detalhamento dos Módulos Funcionais

### MÓDULO 1: Comercial & Frente de Loja (Vendas e PDV)
- **1.1. Topologia Balcão $\rightarrow$ Caixa:** Terminais rápidos de pré-venda gerando comandas e boca de caixa central com gaveta física e recebimento concentrado.
- **1.2. Operação 100% Teclado (Zero Mouse):** Multiplicador de quantidade (`5*arroz`), atalhos padronizados (`[F1]` a `[F12]`, `[ENTER]`, `[ESC]`).
- **1.3. Gestão de Caixa & Turnos:** Abertura com suprimento/fundo de troco, sangrias para cofre e fechamento cego (apuração sem revelar o valor teórico).
- **1.4. Meios de Pagamento:** Múltiplos cartões, Dinheiro (com troco), Pix (QR Code dinâmico/estático), Vale e Faturado/Crediário.
- **1.5. Trocas e Devoluções:** Emissão de vale-crédito para o cliente, estorno de comanda e reincorporação imediata ao estoque.
- **1.6. Crediário / "Fiado" da Loja:** Bloqueio por limite de crédito, ficha financeira do cliente e liquidação na boca do caixa.

---

### MÓDULO 2: Suprimentos, Estoque & Compras
- **2.1. Importação Física de XML (NF-e de Compra):** Leitura de arquivo `.xml` da SEFAZ, cadastramento automático de novos produtos e vinculação por código de barras (EAN/GTIN).
- **2.2. Precificação & Formação de Custo Real:** Rateio de Frete, Seguro, Outras Despesas, IPI e Substituição Tributária (ST) no custo unitário. Sugestão de Preço de Venda por Margem de Contribuição e Markup desejado.
- **2.3. Controle de Grade e Unidades:** Conversão de unidades (Ex: Compra caixa com 24, vende unidade avulsa) e variações (Cor, Tamanho, Voltagem).
- **2.4. Gestão de Estoque Mínimo & Sugestão de Compra:** Alerta de produtos em ponto de pedido com base no histórico de consumo médio diário e lead time do fornecedor.
- **2.5. Auditoria & Balanço (Inventário Periódico):** Contagem cega via leitor de código de barras portátil, ajuste de quebras/perdas com motivo e histórico de auditoria.

---

### MÓDULO 3: Financeiro & Fluxo de Caixa
- **3.1. Contas a Pagar (Passivo):** Geração automática de duplicatas a partir do XML de compra, contas fixas (aluguel, folha, luz, água) e juros/multas por atraso.
- **3.2. Contas a Receber (Ativo):** Controle de recebíveis de cartão de crédito/débito (com dedução da taxa da adquirente), parcelamentos de crediário e boletos bancários.
- **3.3. Fluxo de Caixa (Realizado vs. Projetado):** Visualização diária, semanal e mensal das entradas e saídas previstas, antecipando necessidade de capital de giro.
- **3.4. Controle Bancário & Tesouraria:** Cadastro de múltiplas contas bancárias e conciliação de transferências entre gaveta física do caixa e banco.

---

### MÓDULO 4: Fiscal & Tributário (Compliance Brasil)
- **4.1. Emissão de NFC-e / Cupom Fiscal Eletrônico:** Comunicação direta com a SEFAZ via WebService, assinatura digital por certificado A1/A3 e contingência offline.
- **4.2. Emissão de NF-e (Modelo 55):** Notas de devolução, remessa para conserto, transferência entre filiais e venda interestadual.
- **4.3. Tributação Parametrizada por Produto:** NCM, CEST, CFOP, Origem da Mercadoria, CST/CSOSN de ICMS, PIS e COFINS.
- **4.4. Fechamento Fiscal:** Exportação de lote de XMLs emitidos e cancelados do mês para a contabilidade em 1 clique (arquivo `.zip`).

---

### MÓDULO 5: Inteligência Gerencial & Relatórios
- **5.1. DRE Gerencial Simplificado (Demonstrativo do Resultado do Exercício):** Receita Bruta - Devoluções/Taxas - CMV (Custo da Mercadoria Vendida) = Lucro Bruto - Despesas Operacionais = Lucro Líquido Real.
- **5.2. Análise de Curva ABC:** Classificação de produtos por faturamento (Curva A = 80% do dinheiro) e por volume de giro.
- **5.3. Performance de Vendedores:** Total vendido por atendente, ticket médio por comanda, comissões calculadas automaticamente.
- **5.4. Produtos Críticos:** Itens sem giro há mais de 60/90 dias (capital parado) e produtos com margem negativa ou zerada.

---

### MÓDULO 6: Segurança, Auditoria & Cadastros Base
- **6.1. Controle de Acesso por Níveis:** Gerente, Operador de Caixa, Vendedor de Balcão e Conferente de Estoque.
- **6.2. Log de Auditoria (Trilha de Rastreabilidade):** Registro de quem excluiu item do cupom, quem autorizou desconto especial, quem efetuou sangria e alterações de preço.
- **6.3. Cadastros Estruturados:** Clientes, Fornecedores, Transportadoras, Funcionários/Vendedores, Bancos e Formas de Pagamento.
- **6.4. Backup e Recuperação:** Rotina de salvamento automático do banco de dados local com compactação e expurgo programado.

---

## 3. Matriz de Objetivos (Goals) e Roadmap de Entrega

| Fase | Objetivo Principal | Entregáveis Chave | Status |
| :--- | :--- | :--- | :---: |
| **Fase 1** | **Fundação & Frente de Loja** | - Arquitetura desacoplada MVVM + EF Core<br>- Terminal Balcão Pré-Venda (100% Teclado)<br>- Boca de Caixa Central com Fila `[F4]` e Turnos<br>- Rateio manual de frete/custo na entrada de nota | **CONCLUÍDO** ✅ |
| **Fase 2** | **Automação de Entrada de Mercadorias** | - Leitura e Parser de XML de NF-e (SEFAZ)<br>- De-para de produtos / Cadastro automático por EAN<br>- Atualização atômica de estoque e custo real<br>- Geração automática das duplicatas no financeiro | **CONCLUÍDO** ✅ |
| **Fase 3** | **Gestão Financeira & Crediário** | - Contas a Pagar (Baixa, Vencimento, Multa/Juros)<br>- Contas a Receber & Crediário de Clientes (Fiado)<br>- Recebimento de parcelas na Boca do Caixa<br>- Extrato e Fluxo de Caixa Diário | **CONCLUÍDO** ✅ |
| **Fase 4** | **Inteligência & Gestão de Estoque** | - Alerta de Estoque Mínimo & Sugestão de Compra<br>- Inventário Periódico com contagem cega por leitor<br>- Curva ABC de produtos e histórico de lucratividade<br>- DRE Gerencial automático | **CONCLUÍDO** ✅ |
| **Fase 5** | **Fiscal Oficial & Comunicação Contábil** | - Geração e assinatura de NFC-e (Certificado A1)<br>- Impressão térmica de DANFE NFC-e (ESC/POS 80mm)<br>- Exportador de pacote mensal para o contador (ZIP) | **CONCLUÍDO** ✅ |
| **Fase 6** | **Tríade Crítica de Produção (REV-002)** | - Cancelamento Oficial NFC-e (Evento 110111, Estorno Atômico)<br>- Fechamento Fiscal Mensal (.ZIP Contábil com Autorizadas/Canceladas/CSV)<br>- Backup SQLite Resiliente (`pdv.db` via `VACUUM INTO` + Auto-backup Caixa) | **CONCLUÍDO** ✅ |
