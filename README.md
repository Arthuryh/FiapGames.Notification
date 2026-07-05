# FiapGames.Notification

Microserviço responsável por processar notificações da plataforma FiapGames, consumindo eventos de mensageria e registrando o histórico de comunicações enviadas. Construído com .NET 10, RabbitMQ e SQL Server LocalDB.

## Objetivo

Este projeto consome mensagens de eventos publicados por outros microsserviços e persiste o histórico de notificações em banco de dados.

O fluxo principal é:
- uma mensagem chega na fila RabbitMQ;
- o worker lê o evento;
- o evento é deserializado e processado;
- o histórico da notificação é salvo no banco.

## Arquitetura

A solução está organizada em três camadas principais:

- Application: modelos de eventos de integração e contratos compartilhados.
- Domain: entidades e interfaces de domínio.
- Infrastructure: configuração do RabbitMQ, worker, contexto do EF Core e repositório.

## Tecnologias

- .NET 10
- RabbitMQ
- Entity Framework Core
- SQL Server / LocalDB
- xUnit para testes

## Pré-requisitos

- .NET 10 SDK
- RabbitMQ instalado e em execução localmente
- SQL Server LocalDB disponível

## Configuração

O arquivo de configuração está em:
- src/2-Notification.Infrastructure/appsettings.json

As principais chaves são:
- RabbitMq:HostName
- RabbitMq:Port
- RabbitMq:UserName
- RabbitMq:Password
- ConnectionStrings:DefaultConnection

## Execução

1. Inicie o RabbitMQ localmente.
2. Garanta que o SQL Server LocalDB esteja disponível.
3. Aplique as migrações:
   ```bash
   dotnet ef database update --project src/2-Notification.Infrastructure/2-Notification.Infrastructure.csproj
   ```
4. Execute a aplicação:
   ```bash
   dotnet run --project src/2-Notification.Infrastructure/2-Notification.Infrastructure.csproj
   ```

## Fluxo de fila

O worker consome mensagens da exchange e fila:
- exchange: notificacao.exchange
- fila: notificacao.queue
- routing keys aceitas:
  - autenticacao.notificacao
  - pagamento.notificacao

O payload esperado segue o contrato de evento de integração com suporte a propriedades em camelCase.

## Persistência

As notificações processadas são salvas na tabela:
- HistoricoNotificacoes

O repositório responsável pela gravação está em:
- src/2-Notification.Infrastructure/Repository/HistoricoNotificacaoRepository.cs

## Testes

Para executar os testes:

```bash
 dotnet test FiapGame.Notification.slnx
```

## Observações

- O worker usa acknowledge manual (`ack/nack`) para controlar o ciclo de processamento.
- Em caso de erro, a mensagem pode ser descartada da fila conforme a implementação atual.
- Para troubleshooting, verifique os logs do worker e o estado da fila no RabbitMQ.
