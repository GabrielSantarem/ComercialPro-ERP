# 🏪 ComercialPro ERP & PDV (.NET 10 + Avalonia UI)

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Avalonia UI](https://img.shields.io/badge/Avalonia%20UI-11.2-8E44AD?logo=avalonia)](https://avaloniaui.net/)
[![SQLite](https://img.shields.io/badge/Database-SQLite%203-003B57?logo=sqlite)](https://www.sqlite.org/)
[![Unit Tests](https://img.shields.io/badge/Tests-243%20Passed%20(100%25)-27AE60)](#-qualidade--testes-automatizados)
[![Zero Mouse PDV](https://img.shields.io/badge/UX-100%25%20Zero%20Mouse-E67E22)](#-ergonomia-de-frente-de-caixa-zero-mouse)
[![SEFAZ NFC-e](https://img.shields.io/badge/Fiscal-NFC--e%204.00%20(Zeus)-2980B9)](#-conformidade-fiscal-brasileira)

> **Case de Engenharia de Software Orientada por Agentes Autônomos de IA:**  
> Este projeto demonstra a concepção, desenvolvimento, auditoria técnica e homologação do **ComercialPro ERP**, uma plataforma completa de ERP e PDV Comercial de nível profissional para o mercado varejista brasileiro, construída e evoluída através de uma esteira rigorosa de **Agentes de IA (Arquitetura Agente-Executor / Agente-Auditor)** com especificações formais (`REV-001` a `REV-004`), conformidade fiscal real (NFC-e / SEFAZ) e cobertura massiva de testes de unidade sem regressões.

---

## 🎯 Sobre o Projeto

O **ComercialPro ERP** foi desenhado para resolver as dores reais do comércio varejista físico brasileiro (mercados, padarias, açougues, hortifrutis, papelarias e distribuidores), aliando:
1. **Velocidade Extrema no Checkout:** Operação de caixa 100% por teclado (*Zero Mouse*), fila balcão pré-venda com comanda FIFO e contingência offline resiliente.
2. **Conformidade Tributária Rigorosa:** Motor NFC-e 4.00 (Zeus Automação), DANFE térmico ESC/POS e DANFE simplificado A4 em PDF vetorial (QuestPDF), cancelamento oficial SEFAZ (Evento 110111) e pacote contábil mensal `.zip`.
3. **Hardware & Automação Sem Atrito:** Parser inteligente de etiquetas pesáveis de balança (EAN-13 iniciado por `2`), acionamento automático de gaveta de dinheiro (RJ12 ESC/POS) e gerador de etiquetas de gôndola adesivas A4 (Pimaco).
4. **Inteligência de Varejo:** Análise de Curva ABC, DRE Gerencial em tempo real, radar de estoque sem giro (capital parado) e gestão de crediário/fiado com algoritmo oficial de validação de CPF/CNPJ e trava automática por inadimplência.

---

## 🤖 Engenharia com Agentes de IA: O Diferencial Deste Repositório

Diferente de projetos gerados por prompts casuais que acumulam dívida técnica e quebras silenciosas, este ERP foi construído utilizando um **Ciclo Formal de Especificação, Execução e Auditoria Cruzada**:

```
 ┌──────────────────┐         ┌──────────────────┐         ┌──────────────────┐
 │  AGENTE AUDITOR  │ ──────> │ AGENTE EXECUTOR  │ ──────> │  AGENTE AUDITOR  │
 │ (Pesquisa & REV) │         │ (Implementação)  │         │ (Pente Fino/Test)│
 └──────────────────┘         └──────────────────┘         └──────────────────┘
   • Análise de mercado         • C# 14 / EF Core 9          • Execução dotnet test
   • Redação técnica formal     • MVVM Avalonia XAML         • Inspeção Git Diff
   • Especificação de testes    • Clean Code & DI            • Homologação do REV
```

### Esteira de Especificações Oficiais Homologadas:
* [📄 `docs/REV-001`: Auditoria Arquitetural e Redesenho do ERP](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/docs/REV-001-AUDITORIA-E-REDESENHO.md)
* [📄 `docs/REV-002`: Tríade Crítica (Cancelamento NFC-e, Fechamento Contábil ZIP e Backup Atômico SQLite)](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/docs/REV-002-TRIADE-CRITICA-IMPLEMENTACAO.md)
* [📄 `docs/REV-003`: Operação Comercial (Trocas/Vales, Cadastro Mestre de Clientes, Limites e Produtos Sem Giro)](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/docs/REV-003-OPERACAO-COMERCIAL-E-INTELIGENCIA.md)
* [📄 `docs/REV-004`: Hardware de Varejo (Balanças EAN-13 Prefixo 2, Etiquetas Pimaco QuestPDF e Gaveta RJ12)](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/docs/REV-004-HARDWARE-E-ETIQUETAS.md)
* [📄 `docs/ROADMAP_ERP_EVOLUCAO`: Visão Estratégica das Fases Futuras (TEF, Pix Dinâmico e NF-e 55)](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/docs/ROADMAP_ERP_EVOLUCAO.md)

---

## ⚡ Principais Módulos do Sistema

### 🛒 1. Frente de Caixa (PDV) & Pré-Venda Balcão
* **Paradigma Zero Mouse:** Todos os fluxos operados estritamente por atalhos (`[F1]` ao `[F12]`), isolamento de modais com captura de teclado e autofoco.
* **Leitura Híbrida de Código de Barras:** Detecta produtos unitários (EAN-13, EAN-8) e decodifica etiquetas pesáveis de balanças de retaguarda (Toledo, Filizola, Urano) com cálculo de tara, peso fracionado e valor total.
* **Múltiplos Meios de Pagamento (Split Payment):** Pagamento fracionado em Dinheiro, Cartões, PIX, Crediário (Fiado) e Vale-Crédito.
* **Turno e Gestão de Caixa:** Suprimento, sangria, conferência cega e fechamento atômico de turno com backup automático do banco de dados.

### 📜 2. Motor Fiscal & Comprovantes
* **NFC-e 4.00 Oficial:** Integração nativa com a biblioteca Zeus Automação (Ambiente de Produção e Homologação).
* **Cancelamento Homologado:** Evento 110111 SEFAZ com trava de tempo regulamentar ($\le 30$ min), senha de supervisor e estorno atômico de estoque e caixa.
* **DANFE Duplo Formato:** Cupom térmico ESC/POS (80mm e 58mm) e DANFE Simplificado A4 em PDF vetorial de alta definição via QuestPDF.
* **Fechamento Contábil:** Exportação em 1 clique do pacote `.zip` mensal contendo pastas `Autorizadas/`, `Canceladas/` e planilha CSV de conferência contábil.

### 🔄 3. Trocas, Devoluções & Vales-Crédito
* Registro formal de devoluções com direcionamento inteligente: **Estoque Disponível** (reincorporação imediata) ou **Quarentena / Avaria** (perda sem reestoque).
* Emissão de cupom de Vale-Troca com código único (`VALE-YYYY-XXXXX`), validade e abatimento direto no checkout do PDV.

### 👥 4. Cadastro de Clientes & Gestão de Crediário (Fiado)
* Validação matemática oficial dos dígitos verificadores de CPF e CNPJ (Receita Federal).
* Controle de limite de crédito pré-aprovado e saldo devedor em tempo real.
* Trava automática no PDV contra vendas fiadas para clientes inadimplentes (títulos vencidos há mais de 5 dias).

### 🏷️ 5. Etiquetas de Gôndola (Prateleira) com QuestPDF
* Gerador integrado de folhas adesivas A4 padrão **Pimaco 6180** (30 etiquetas) e **Pimaco 6182** (14 etiquetas).
* Exibição de código de barras legível, preço à vista em destaque e preço por quilo/litro fracionado (conformidade com o CDC).

### 📊 6. Inteligência Comercial & Backoffice
* **Curva ABC de Produtos:** Classificação automática em classes A, B e C por faturamento e margem de contribuição.
* **DRE Gerencial em Tempo Real:** Receita bruta, dedução de CMV real, despesas operacionais pagas e lucro líquido do exercício.
* **Radar de Estoque Sem Giro:** Diagnóstico de capital imobilizado por filtros de 30, 60 e 90+ dias sem movimentação.
* **Apuração de Comissões:** Cálculo automático da remuneração variável dos vendedores com expurgo de vendas canceladas.

---

## 🛠️ Stack Tecnológica

| Camada | Tecnologia | Destaque |
| :--- | :--- | :--- |
| **Linguagem / Runtime** | C# 14 / .NET 10 | Recursos modernos de records, pattern matching e performance assíncrona. |
| **Interface Gráfica (UI)** | Avalonia UI 11.2 | Multiplataforma nativa (Linux / Windows / macOS), XAML fluido e MVVM CommunityToolkit. |
| **Banco de Dados** | SQLite 3 + EF Core 9 | Local, transacional, atômico e com snapshot online via `VACUUM INTO`. |
| **Motor Fiscal** | Zeus.Net.NFe.NFCe | Comunicação oficial com webservices SEFAZ, assinatura digital X509 e contingência offline. |
| **Motor de Documentos** | QuestPDF | Renderização milimétrica de relatórios, DANFE A4 e folhas de etiquetas de gôndola. |
| **Logging & Auditoria** | Serilog | Gravação estruturada em arquivo rolante e console. |
| **Testes de Unidade** | xUnit + Avalonia.Headless | 243 testes unitários e de integração de interface sem necessidade de hardware físico. |

---

## 🧪 Qualidade & Testes Automatizados

O repositório adota a premissa de **Zero Dependência de Hardware Físico** para desenvolvimento e execução em esteiras de integração contínua (CI/CD). O ecossistema conta com drivers emuladores para balança, impressora térmica e gaveta:

```bash
# Executar a suíte completa de testes da solução ComercialPro
dotnet test ComercialPro.sln
```

### Cobertura da Bateria de Testes (243 testes aprovados com 100% de êxito):
* ✅ **Fiscal & SEFAZ:** Emissão, contingência offline, rejeição legal de cancelamento fora do prazo de 30 min e estorno de caixa.
* ✅ **Trocas & Vales:** Emissão de vale com avaria, liquidação parcial, liquidação total e bloqueio de vales expirados.
* ✅ **Clientes & Crédito:** Algoritmo oficial de CPF/CNPJ, recusa por teto estourado e bloqueio automático por inadimplência.
* ✅ **Inteligência de Vendas:** Cálculo de capital parado, expurgo de estoque zerado e apuração de comissões.
* ✅ **Hardware & Periféricos:** Parser EAN-13 (preço e peso), geração de PDF de etiquetas Pimaco e bytes ESC/POS de gaveta RJ12.
* ✅ **Resiliência do SQLite:** Backup transacional com snapshot `VACUUM INTO` e expurgo de histórico após 30 dias.

---

## 🚀 Como Executar o Projeto

### Pré-requisitos
* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) instalado.
* Linux (Ubuntu, Debian, Fedora, Arch) ou Windows 10/11.

### Passos de Instalação e Execução
```bash
# 1. Clonar o repositório
git clone https://github.com/seu-usuario/ComercialPro-ERP.git
cd ComercialPro-ERP

# 2. Restaurar dependências
dotnet restore ComercialPro.sln

# 3. Executar os testes automatizados
dotnet test ComercialPro.sln

# 4. Rodar o ERP
dotnet run --project ComercialPro.csproj
```

---

## 📄 Licença e Uso

Este software é disponibilizado para fins de demonstração técnica, estudo de engenharia de software orientada a agentes de inteligência artificial e base de aceleração para automação comercial.
