# REV-002: Especificação Técnica e Diretrizes de Implementação da Tríade Crítica do ERP

| Metadado | Detalhe |
| :--- | :--- |
| **Identificador do Documento** | `REV-002` |
| **Título** | Especificação Técnica Executiva: Cancelamento Fiscal, Fechamento Contábil e Backup Resiliente |
| **Data de Emissão** | 2026-09-18 |
| **Status** | Aprovado para Implementação / Handoff Técnico para o Agente Executor |
| **Documentos Relacionados** | [`REV-001`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/docs/REV-001-AUDITORIA-E-REDESENHO.md), [`GOALS_ERP.md`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/GOALS_ERP.md), [`DOCUMENTACAO_SISTEMA.md`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/DOCUMENTACAO_SISTEMA.md) |
| **Ambiente Alvo** | .NET 10 (C# 14), Avalonia UI 11.2, SQLite (EF Core 9 / Microsoft.Data.Sqlite), Zeus Automação |

---

## 1. Contexto e Seleção Estratégica da Tríade

A auditoria do `REV-001` comprovou que o núcleo do ERP é funcional, mas o lojista corre sérios riscos fiscais, contábeis e de perda de dados se o sistema for colocado em produção no varejo sem os 3 pilares a seguir. 

Por unanimidade de análise de risco e impacto no negócio, foram selecionados os **3 módulos indispensáveis de sobrevivência operacional**:

```
                 ┌─────────────────────────────────────────────────────────┐
                 │       TRÍADE CRÍTICA DE SOBREVIVÊNCIA DO VAREJO         │
                 └────────────────────────────┬────────────────────────────┘
                                              │
         ┌────────────────────────────────────┼────────────────────────────────────┐
         │                                    │                                    │
         ▼                                    ▼                                    ▼
┌──────────────────┐                ┌──────────────────┐                ┌──────────────────┐
│  1. CANCELAMENTO │                │  2. FECHAMENTO   │                │   3. BACKUP &    │
│     DE NFC-e     │                │   FISCAL .ZIP    │                │   RESILIÊNCIA    │
├──────────────────┤                ├──────────────────┤                ├──────────────────┤
│ Evita pagar      │                │ Pacote mensal    │                │ Tolerância a     │
│ impostos sobre   │                │ obrigatório para │                │ falhas de disco  │
│ vendas desfeitas │                │ o contador       │                │ e corrupção SQLite│
└──────────────────┘                └──────────────────┘                └──────────────────┘
```

---

## 2. Especificação do Módulo 1: Cancelamento Oficial de NFC-e (Evento 110111)

### 2.1. O que deve ser feito
Implementar o serviço de **Cancelamento de NFC-e homologado perante a SEFAZ** (Evento `110111`), integrando-o ao motor do Zeus Automação já instalado no projeto. O cancelamento deve estornar atomicamente o estoque, marcar a venda como cancelada e atualizar a gaveta do caixa.

### 2.2. Comportamento e Fluxo Operacional (UI/UX)
1. **Ponto de Entrada:** No PDV ou na Consulta de Vendas Recentes, haverá a ação `[F7] Cancelar Última Venda / NFC-e`.
2. **Modal de Confirmação e Segurança:**
   - Exibe dados da venda: Número da NFC-e, Chave de Acesso, Horário e Valor Total.
   - Campo obrigatório: `Justificativa do Cancelamento` (mínimo de 15 caracteres exigido pela regra SEFAZ).
   - Campo obrigatório: `Senha do Gerente/Supervisor` (impede cancelamentos indevidos pelo operador).
3. **Execução:**
   - Botão `[Confirmar Cancelamento]` desabilita a interface e exibe indicador de carregamento ("Comunicando com SEFAZ...").
4. **Desfecho:**
   - Se homologado pela SEFAZ: Exibe alerta verde com o Protocolo de Homologação, emite aviso sonoro e reincorpora as mercadorias ao estoque do banco.
   - Se rejeitado: Exibe alerta vermelho com o código e descrição exata do erro retornado pela SEFAZ.

### 2.3. Assinatura de Código Esperada
```csharp
namespace GetStartedApp.Services.Fiscal;

public class RetornoCancelamentoNfce
{
    public bool Sucesso { get; set; }
    public string Mensagem { get; set; } = string.Empty;
    public string? ProtocoloCancelamento { get; set; }
    public DateTime? DataHoraCancelamento { get; set; }
    public string? XmlCancelamento { get; set; }
}

public interface INfceCancelamentoService
{
    Task<RetornoCancelamentoNfce> CancelarNfceAsync(
        int vendaId,
        string justificativa,
        string senhaSupervisor);
}
```

### 2.4. Tratamento de Erros e Casos Extremos (Edge Cases)
* **Prazo Legal Expirado (> 30 Minutos):** A SEFAZ rejeita cancelamentos após 30 minutos (Rejeição `220`). O sistema deve interceptar esse código e orientar o lojista: *"Prazo legal de cancelamento de 30 minutos expirado. Efetue uma devolução/estorno de mercadoria."*
* **SEFAZ Offline ou Queda de Conexão durante o Cancelamento:** Se a comunicação falhar por Timeout, o sistema **NÃO PODE** estornar o estoque nem marcar a venda como cancelada no banco, para evitar divergência com o fisco.
* **Justificativa Curta (< 15 Caracteres):** Validação na UI antes de disparar requisição, impedindo envio com mensagens como "erro", "desistencia" sem detalhamento.
* **Tentativa de Cancelar Nota já Cancelada:** O sistema deve verificar o status antes do envio e barrar requisições duplicadas.

### 2.5. Casos Básicos de Teste a Cobrir (xUnit)
1. `CancelarNfce_ComJustificativaMenorQue15Caracteres_DeveFalharValidacaoPrevia()`
2. `CancelarNfce_ComSenhaSupervisorInvalida_DeveBloquearOperacao()`
3. `CancelarNfce_ComVendaInexistenteOuJaCancelada_DeveRetornarErro()`
4. `CancelarNfce_HomologadaComSucesso_DeveAtualizarStatusVenda_E_ReincorporarEstoque()`
5. `CancelarNfce_QuandoSefazRetornaRejeicao_NaoDeveAlterarEstoque()`

---

## 3. Especificação do Módulo 2: Fechamento Fiscal Mensal (.ZIP Contábil)

### 3.1. O que deve ser feito
Criar um gerador de **Pacote Contábil Mensal em arquivo `.zip`**, que varre os diretórios de armazenamento de XMLs fiscais do sistema, agrupa notas autorizadas, notas canceladas e eventos fiscais do mês escolhido, gera um relatório sintético em `.csv` e compacta tudo em um arquivo padronizado para entrega à contabilidade.

### 3.2. Comportamento e Fluxo Operacional (UI/UX)
1. **Localização:** Aba no Módulo de Gestão/Dashboard ou tela de Configurações Fiscais.
2. **Seleção:**
   - Seletor de `Mês` (Janeiro a Dezembro, default: mês anterior ou atual).
   - Seletor de `Ano` (default: ano atual).
3. **Ações:**
   - Botão `[📦 Gerar Pacote Contábil (.ZIP)]`.
   - Botão `[📂 Abrir Pasta de Exportações]` (abre o gerenciador de arquivos do Linux/Windows).
4. **Estrutura interna do arquivo compactado (`FechamentoFiscal_[CNPJ]_[ANO]_[MES].zip`):**
   ```
   FechamentoFiscal_12345678000195_2026_08.zip
   ├── Autorizadas/
   │   ├── 312608...650010000000011000000010-nfe.xml
   │   └── 312608...650010000000021000000025-nfe.xml
   ├── Canceladas/
   │   └── 312608...11011101-procEventoNFe.xml
   └── Resumo_Fiscal_2026_08.csv
   ```
   *Conteúdo do `Resumo_Fiscal.csv`:* Número, Série, Data Emissão, Chave de Acesso, Valor Total, Status (Autorizada/Cancelada), Valor Tributos Aprox.

### 3.3. Assinatura de Código Esperada
```csharp
namespace GetStartedApp.Services.Fiscal;

public record ResumoFechamentoFiscalDto(
    int TotalAutorizadas,
    int TotalCanceladas,
    decimal FaturamentoTotal,
    decimal TotalImpostosAproximados,
    string CaminhoArquivoZip);

public interface IFechamentoFiscalService
{
    Task<ResumoFechamentoFiscalDto> GerarPacoteMensalAsync(int ano, int mes, string diretorioDestino);
}
```

### 3.4. Tratamento de Erros e Casos Extremos (Edge Cases)
* **Mês sem Movimentação Fiscal:** Se não houver notas no mês selecionado, não deve gerar zip vazio; deve lançar `InvalidOperationException` ou retornar resultado com mensagem explicativa: *"Nenhuma nota fiscal encontrada para o período 08/2026"*.
* **Arquivos XML Bloqueados por Outro Processo:** Usar `FileShare.Read` ao ler os XMLs para não conflitar com leituras simultâneas do PDV.
* **Espaço Insuficiente ou Caminho Inválido:** Tratar `IOException` e exibir alerta limpo na UI em vez de quebrar a aplicação.

### 3.5. Casos Básicos de Teste a Cobrir (xUnit)
1. `GerarPacoteMensal_ComMesSemNotas_DeveRetornarAlertaSemGerarArquivo()`
2. `GerarPacoteMensal_ComNotasAutorizadasECanceladas_DeveSepararEmPastasNoZip()`
3. `GerarPacoteMensal_DeveGerarCsvResumoComTotalDeFaturamentoExato()`
4. `GerarPacoteMensal_NomeDoArquivo_DeveConterCnpjAnoEMesPadronizados()`

---

## 4. Especificação do Módulo 3: Mecanismo de Backup e Recuperação Automática do SQLite

### 4.1. O que deve ser feito
Implementar uma rotina de **Backup Automático e Resiliente do Banco de Dados SQLite (`pdv.db`)** com rotação e expurgo programado (retenção dos últimos 30 backups diários). Deve operar sem risco de corrupção usando a API online do SQLite ou comando atômico `VACUUM INTO`.

### 4.2. Comportamento e Fluxo Operacional (UI/UX)
1. **Gatilhos Automáticos (Background):**
   - Disparado automaticamente toda vez que o caixa faz **Fechamento de Turno** no PDV (`PdvService.FecharCaixaAsync`);
   - Disparado na inicialização do sistema se o último backup tiver mais de 24 horas.
2. **Gatilho Manual (UI):**
   - Na tela de Configurações, card "Segurança do Banco de Dados":
     - Exibe: Data/Hora do último backup, tamanho do arquivo e total de cópias salvas.
     - Botão `[💾 Fazer Backup Manual Agora]`.
     - Botão `[📂 Abrir Pasta de Cópias de Segurança]`.
3. **Política de Rotação (Housekeeping):**
   - Nome do arquivo: `pdv_backup_yyyyMMdd_HHmmss.zip`.
   - O serviço varre a pasta de backups e apaga arquivos com data superior à política de retenção (default: 30 dias), impedindo o esgotamento do disco do caixa.

### 4.3. Assinatura de Código Esperada
```csharp
namespace GetStartedApp.Services;

public record InformacaoBackupDto(
    string NomeArquivo,
    string CaminhoCompleto,
    DateTime DataHoraCriacao,
    long TamanhoBytes);

public interface IBackupDatabaseService
{
    Task<InformacaoBackupDto> ExecutarBackupAsync(string? motivo = "Manual");
    Task<List<InformacaoBackupDto>> ListarBackupsExistentesAsync();
    Task<int> LimparBackupsAntigosAsync(int diasRetencao = 30);
    Task<bool> RestaurarBackupAsync(string caminhoArquivoZip);
}
```

### 4.4. Tratamento de Erros e Casos Extremos (Edge Cases)
* **Banco SQLite em Uso/Transação Aberta:** O método tradicional de cópia de arquivo (`File.Copy`) corrompe arquivos SQLite se houver gravação concorrente. **Obrigatório usar:**
  ```csharp
  // Execução via conexão SQLite do EF Core / Microsoft.Data.Sqlite
  await dbContext.Database.ExecuteSqlRawAsync($"VACUUM INTO '{caminhoTempDb}'");
  ```
  O `VACUUM INTO` cria um clone perfeito e atômico do banco em uma fração de segundo, mesmo com o PDV operando. Em seguida, o arquivo resultante é compactado para `.zip`.
* **Disco Cheio ou Protegido contra Escrita:** Interceptar exceções de I/O e gravar log crítico via Serilog sem travar a interface do PDV.

### 4.5. Casos Básicos de Teste a Cobrir (xUnit)
1. `ExecutarBackup_EmBancoComDados_DeveGerarArquivoZipValidoENaoVazio()`
2. `ExecutarBackup_DeveCompactarCloneDb_E_PermitirLeituraAposDescompactar()`
3. `LimparBackupsAntigos_ComArquivosMaisVelhosQue30Dias_DeveRemoverApenasOsExpirados()`
4. `ExecutarBackup_DuranteVendaAtiva_DeveExecutarSemBloquearOuLancarErroDeLock()`

---

## 5. Requisitos Técnicos para Homologação e Aprovação

O agente executor só terá sua entrega aceita se cumprir integralmente os 4 critérios técnicos abaixo:

1. **Compilação Impecável:**
   ```bash
   dotnet build /home/tomate/Lixeira/dotnet/C#/GetStartedApp
   ```
   *Critério:* **0 Erros e 0 Warnings**. Nenhuma supressão de aviso com `#pragma warning disable` injustificada.

2. **Cobertura de Testes Automatizados (Zero Regressões):**
   ```bash
   dotnet test /home/tomate/Lixeira/dotnet/C#/GetStartedApp
   ```
   *Critério:* Todos os 191 testes atuais devem permanecer passando, acrescidos dos novos testes dos 3 módulos (mínimo de 12 a 15 novos testes unitários e de integração adicionados).

3. **Injeção de Dependências no Container Central:**
   - Registrar as novas interfaces e serviços em [`App.axaml.cs`](file:///home/tomate/Lixeira/dotnet/C#/GetStartedApp/App.axaml.cs) através de `services.AddSingleton<...>()` ou `services.AddTransient<...>()`.
   - Injetar instâncias reais em produção e mocks/NullLoggers nos testes.

4. **Preservação do Modo Zero Mouse no PDV:**
   - Qualquer novo modal ou tela adicionada deve respeitar o fechamento por `[ESC]`, confirmação por `[ENTER]` e atalhos de função (`F1..F12`), sem forçar o uso de ponteiro de mouse pelo operador.

---
*Documento homologado pelo Revisor Técnico. Pronto para execução imediata.*
