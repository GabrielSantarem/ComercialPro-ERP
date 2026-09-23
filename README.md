* Linux (Ubuntu, Debian, Fedora, Arch) ou Windows 10/11.

### Passos de Instalação e Execução
```bash
# 1. Clonar o repositório
git clone https://github.com/GabrielSantarem/ComercialPro-ERP.git
cd ComercialPro-ERP

# 2. Restaurar dependências
dotnet restore ComercialPro.sln

# 3. Executar os testes automatizados
dotnet test ComercialPro.sln

# 4. Rodar o ERP
dotnet run --project ComercialPro.csproj
