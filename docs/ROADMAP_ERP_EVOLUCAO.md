# ROADMAP ESTRATÉGICO DE EVOLUÇÃO DO ERP & AUTOMAÇÃO COMERCIAL

| Metadado | Detalhe |
| :--- | :--- |
| **Documento** | `ROADMAP_ERP_EVOLUCAO.md` |
| **Data de Criação** | 2026-09-20 |
| **Status** | Aprovado para Planejamento e Arquitetura de Fases Futuras |
| **Base Arquitetural** | .NET 10 (C# 14), Avalonia UI 11.2, SQLite / EF Core 9, Zeus Automação, QuestPDF |
| **Documentos Precursores** | [`REV-001`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/docs/REV-001-AUDITORIA-E-REDESENHO.md), [`REV-002`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/docs/REV-002-TRIADE-CRITICA-IMPLEMENTACAO.md), [`REV-003`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/docs/REV-003-OPERACAO-COMERCIAL-E-INTELIGENCIA.md) |

---

## 1. Visão Geral da Maturidade do ERP

Após a conclusão e homologação das revisões `REV-001`, `REV-002` e `REV-003` (226 testes unitários com 100% de sucesso), o ERP atingiu o nível de **Fundação Sólida e Estabilidade Operacional**:
- **PDV / Frente de Caixa:** Modo Zero Mouse, turnos de caixa com fechamento cego, fila balcão FIFO `[F4]`, split payment e devoluções/trocas com vales `[F10]`.
- **Fiscal:** Emissão oficial de NFC-e 4.00 (Zeus Automação), DANFE A4 em PDF (QuestPDF), cancelamento oficial SEFAZ `[F7]` e pacote mensal `.zip`.
- **Inteligência e Backoffice:** Backup atômico SQLite (`VACUUM INTO`), cadastro mestre de clientes com trava de fiado por inadimplência, Curva ABC, DRE Gerencial, radar de estoque sem giro e apuração de comissões.

A próxima etapa é transformar este sistema em um **ERP de Varejo de Alta Performance e Competitividade de Mercado**, nivelando-o às soluções líderes do varejo brasileiro (Linx, Totvs Moda/Varejo, Hiper, Alterdata, Bematech).

---

## 2. O que ERPs Modernos de Varejo Implementam (Benchmark de Mercado)

Pesquisando o ecossistema brasileiro de automação comercial e sistemas de gestão de lojas, identificamos 5 grandes domínios funcionais que agregam valor imediato:

```
┌───────────────────────────────────────────────────────────────────────────────────────────────────┐
│                           DOMÍNIOS DE EVOLUÇÃO DO ERP DE VAREJO BRASILEIRO                         │
└─────────────────────────────────┬─────────────────────────────────────────────────────────────────┘
                                  │
      ┌───────────────────────────┼───────────────────────────┬───────────────────────────┐
      │                           │                           │                           │
      ▼                           ▼                           ▼                           ▼
┌───────────────┐           ┌───────────────┐           ┌───────────────┐           ┌───────────────┐
│ 1. HARDWARE & │           │ 2. MEIOS DE   │           │ 3. PROMOÇÕES, │           │ 4. COMÉRCIO   │
│ PERIFÉRICOS   │           │ PAGAMENTO 2.0 │           │ CLUBE & CRM   │           │ MERCANTIL B2B │
├───────────────┤           ├───────────────┤           ├───────────────┤           ├───────────────┤
│ • Balança EAN │           │ • Pix Dinâmico│           │ • Motor Leve3 │           │ • Emissor NF-e│
│   pesável (2) │           │   via Webhook │   Pague 2     │   Mod. 55 A4  │
│ • Impressão de│           │ • TEF Integrado│          │ • Cashback e  │           │ • Devolução a │
│   Etiquetas de│           │   (Cappta/    │   Fidelidade  │   fornecedores│
│   Gôndola A4  │           │   SiTef)      │ • Tabela por  │ • Carta de    │
│ • Gaveta RJ12 │           │ • Conciliação │   Faixa Qtd   │   Correção    │
└───────────────┘           └───────────────┘           └───────────────┘           └───────────────┘
```

---

## 3. Matriz de Priorização do Roadmap (Fases de Implementação)

Cruzando **impacto no faturamento do lojista** com a **complexidade técnica de implementação**, estruturamos as próximas 4 fases:

```
                  Complexidade Técnica
           Baixa                        Alta
        ┌──────────────────────────┬──────────────────────────┐
        │ FASE 4 (Imediata)        │ FASE 5 (Faturamento B2B) │
        │ • Etiquetas de Gôndola   │ • Emissão NF-e Mod. 55   │
 Alto   │ • Balança Checkout &     │ • Devolução Fornecedor   │
        │   Código de Barras EAN-2 │ • Carta de Correção CC-e │
        ├──────────────────────────┼──────────────────────────┤
 Impacto│ FASE 6 (Fidelização)     │ FASE 7 (Fintech / TEF)   │
 Comercial│ • Motor de Descontos     │ • TEF IP Integrado       │
        │   (Atacarejo / Combos)   │ • Pix Dinâmico c/ Pix API│
        │ • Cashback do Cliente    │ • Conciliação Adquirentes│
        └──────────────────────────┴──────────────────────────┘
```

---

## 4. Detalhamento Fino das Fases de Evolução

### 🎯 FASE 4: Hardware de Varejo, Balanças & Impressão de Etiquetas de Gôndola
*Objetivo: Permitir que o ERP atenda mercadinhos, padarias, hortifrutis, açougues e papelarias sem nenhuma adaptação manual.*

1. **Parser de Código de Barras Pesável (Padrão Nacional EAN-13 iniciado por `2`):**
   - No Brasil, balanças de retaguarda (Toledo Prix, Filizola, Urano) emitem etiquetas com EAN `2 CCCCC VVVVV D` ou `2 CCCCC QQQQQ D`.
   - O leitor do PDV identifica o prefixo `2`, extrai o código do produto (`CCCCC`) e o peso/valor (`VVVVV`), calculando a quantidade e preço fracionado instantaneamente.
2. **Gerador de Folha de Etiquetas de Gôndola (Prateleira) com QuestPDF:**
   - Permite selecionar produtos com alteração de preço recente e imprimir folhas A4 padrão (ex.: Pimaco 6180, 6182 ou rolos térmicos Zebra/Argox).
   - Exibe: Nome do Produto, Código de Barras EAN legível por scanner, Preço à Vista em destaque e Preço por Quilo/Litro fracionado.
3. **Comunicação Serial Direta com Balança de Checkout:**
   - Protocolo serial contínuo RS232 / USB Virtual COM (Toledo / Elgin) para captura automática do peso no momento de bipar produto fracionado (`KG`).
4. **Pulso de Abertura de Gaveta Automática (ESC/POS pin RJ12):**
   - Envio do byte `ESC p 0 25 250` na finalização de pagamentos em dinheiro para saltar a gaveta sem necessidade de chave manual.

---

### 🚀 FASE 5: Emissão Mercantil NF-e (Modelo 55) & Devoluções a Fornecedor
*Objetivo: Atendimento corporativo B2B e regularização de devoluções interestaduais com fornecedores.*

1. **Emissor de NF-e Mod. 55 de Saída:**
   - Complementar o `NfceEmissaoService` reaproveitando a infraestrutura do Zeus Automação para gerar o XML modelo 55.
   - Suporte a transportadora (frete CIF/FOB, placa, volumes, peso líquido/bruto).
2. **Devolução Automática de Compras:**
   - A partir de uma NF-e importada via XML, criar a nota de devolução referenciando a chave de acesso original (`NFref`), estornando impostos destacados (ICMS, IPI, ST) e gerando o DANFE Mod. 55 em folha A4.
3. **Carta de Correção Eletrônica (CC-e):**
   - Transmissão do evento 110110 para correção de dados não tributários em notas já emitidas.

---

### 💎 FASE 6: Motor Promocional (Atacarejo), Cashback & Clube de Fidelidade
*Objetivo: Aumentar o ticket médio da loja e criar recorrência de compra dos clientes.*

1. **Motor de Promoções Dinâmicas (Promo Engine):**
   - Regra de Atacarejo por Faixa de Volume: *"A partir de 6 unidades, o preço cai de R$ 5,00 para R$ 4,20"*.
   - Promoções tipo Combo: *"Leve 3 Pague 2"* ou *"Compre o hambúrguer e leve o refrigerante com 50% de desconto"*.
   - Aplicação em tempo real no PDV com recálculo automático no subtotal.
2. **Cashback e Carteira Digital de Fidelidade da Loja:**
   - Parametrização de % de retorno (ex.: 3% do valor da compra creditado na conta do cliente cadastrado).
   - Abatimento do saldo de cashback no checkout mediante confirmação por SMS/WhatsApp ou senha do cliente.

---

### 🏦 FASE 7: Pagamentos Integrados (Pix Dinâmico via API & TEF Multiprovedor)
*Objetivo: Eliminar fraudes no caixa (falsos comprovantes de Pix) e acelerar a fila.*

1. **Pix Dinâmico na Tela com Confirmação Automática (Webhook):**
   - Integração direta com APIs bancárias (Open Finance / Banco do Brasil, Itaú, Santander, Mercado Pago, Efí/Gerencianet).
   - Geração de QR Code exclusivo por venda exibido na tela ou no segundo monitor do cliente.
   - Assim que o cliente paga no smartphone, a API recebe a confirmação em menos de 1 segundo e conclui a venda no PDV sem intervenção humana.
2. **TEF Dedicado (Cappta / SiTef / Stone):**
   - Comunicação com pinpads dedicados via biblioteca TEF para cartão de débito, crédito e voucher alimentação/refeição.
   - Vinculação obrigatória do NSU/Comprovante na NFC-e (exigência fiscal em estados como RS e SC).
3. **Conciliação de Cartões (Recebíveis):**
   - Download de extrato de vendas das adquirentes e confronto automático com as vendas registradas no PDV, alertando sobre divergência de taxas (MDR) e datas de depósito na conta bancária.

---

## 5. Ferramentas & Bibliotecas Recomendadas para Execução

Para garantir coerência técnica com a arquitetura moderna atual do projeto:

| Domínio | Ferramenta / Biblioteca | Justificativa Técnica |
| :--- | :--- | :--- |
| **PDF & Impressão de Etiquetas** | `QuestPDF` | Já utilizado com maestria no DANFE A4 (`DanfeA4PdfService.cs`). Suporta grids flexíveis milimétricos (`cm`, `inch`, `pt`) para folhas Pimaco. |
| **Comunicação Serial (Balanças)** | `System.IO.Ports` nativo | Padrão oficial do .NET 10 para portas seriais COM/TTY sem dependências externas. |
| **Emissão Fiscal NF-e 55** | `NFe.Classes` / `Zeus.Net.NFe.NFCe` | Já instalado e homologado nos testes de contingência do sistema. |
| **Pix Dinâmico** | `HttpClient` + `System.Text.Json` | Integração direta com endpoints REST autenticados via mTLS (Certificado A1) com os bancos. |
| **Compactação & Arquivos** | `System.IO.Compression` nativo | Já utilizado com zero erro no Fechamento Fiscal (`REV-002`). |

---

## 6. Próximo Passo Recomendado

Formalizar a **FASE 4** como a especificação técnica oficial **`REV-004`**, abordando:
1. Leitor e Parser inteligente de etiquetas pesáveis (EAN-13 iniciado por `2`);
2. Gerador oficial de Etiquetas de Gôndola (folha de prateleira A4 Pimaco) via QuestPDF;
3. Pulso elétrico de gaveta de dinheiro no cupom térmico ESC/POS.

Esse pacote colocará o ERP no mesmo patamar dos PDVs de grandes redes de supermercados e hortifrutis.
