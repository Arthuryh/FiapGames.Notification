# FiapGames.Notification

Microsservico serverless de notificacoes do FIAP Cloud Games, implementado como Azure Functions .NET 10 no modelo isolated worker.

## Arquitetura

- `AuthenticationNotificationFunction`: acionada pela fila `notification-authentication` do Azure Service Bus.
- `PaymentNotificationFunction`: acionada pela fila `notification-payment` do Azure Service Bus.
- Azure SQL: armazena o historico de notificacoes.
- Managed Identity: autentica a Function e os produtores no Service Bus e no Key Vault.
- DLQ: cada fila encaminha mensagens automaticamente para `$DeadLetterQueue` depois de cinco entregas malsucedidas.
- Scale to zero: o plano Consumption inicia execucoes somente quando chegam mensagens.

O antigo `ca-notification-worker` nao faz mais parte da arquitetura. O pipeline o remove somente depois de confirmar que as duas Functions foram publicadas.

## Estrutura

```text
src/2-Notification.Infrastructure/
  Functions/                 Service Bus triggers
  Services/                  processamento dos eventos
  Context/                   EF Core DbContext e migrations
  Program.cs                 DI, Key Vault e modo --migrate
  host.json                  configuracao do Functions runtime
infra/
  main.tf                    Function App, Service Bus, Storage e RBAC
.github/workflows/
  deploy-production.yml      infraestrutura, migration e ZIP deploy
```

## Desenvolvimento local

1. Copie `src/2-Notification.Infrastructure/local.settings.example.json` para `local.settings.json`.
2. Configure Azure SQL e um Service Bus de desenvolvimento.
3. Execute:

```powershell
dotnet test FiapGame.Notification.slnx
func start --dotnet-isolated
```

Para aplicar migrations sem iniciar o host:

```powershell
dotnet run --project src/2-Notification.Infrastructure/2-Notification.Infrastructure.csproj -- --migrate
```

## Infraestrutura

O Terraform serverless pertence a este repositorio. Consulte [infra/README.md](infra/README.md). O estado remoto usado pelo pipeline fica no Storage Account `stfcgtfstatecec7f71a`, container `tfstate`.

## Deploy

Pushes em `master` ou `azureContainer` executam:

1. Login no Azure com `AZURE_CREDENTIALS`.
2. `terraform apply` da infraestrutura serverless.
3. Build e testes .NET 10.
4. Migration por Container Apps Job efemero.
5. ZIP deploy na Azure Function.
6. Validacao das duas Functions e remocao do container legado.

Auth e Payment publicam no Service Bus usando a identidade `id-fiapgames-workloads-prod`. Nenhuma chave SAS ou connection string de mensageria e armazenada no GitHub.
