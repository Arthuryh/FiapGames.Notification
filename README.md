# FiapGames.Notification

Microserviço responsável por processar notificações da plataforma FiapGames, consumindo eventos de mensageria e registrando o histórico de comunicações enviadas.

Este serviço atua como o componente de integração assíncrona do ecossistema, recebendo mensagens via RabbitMQ e persistindo os registros de notificação em banco SQL Server.

> Objetivo: oferecer uma base organizada e resiliente para processamento de notificações, seguindo princípios de Clean Architecture e integração com mensageria.

---

## Arquitetura do Projeto

A solução é estruturada em camadas inspiradas em Clean Architecture, com foco em processamento assíncrono, tolerância a falhas e evolução incremental.

### Executar via Docker

Na raiz do projeto, execute:

```bash
docker compose up --build
```

### Estrutura da solução

```txt
FiapGame.Notification.sln

src/
├── 1-Notification.Application
├── 2-Notification.Infrastructure
└── 3-Notificacao.Domain
```

### Responsabilidades das camadas

#### 1-Notification.Application

Camada de aplicação.

Responsável por:
- Regras de negócio relacionadas a notificações
- Serviços de aplicação
- Casos de uso
- DTOs
- Interfaces de contratos

#### 3-Notificacao.Domain

Camada de domínio.

Responsável por:
- Entidades de notificação
- Regras de domínio
- Contratos principais
- Lógica independente de framework

#### 2-Notification.Infrastructure

Camada de infraestrutura.

Responsável por:
- Worker service para processamento assíncrono
- Integração com RabbitMQ
- Persistência com Entity Framework Core
- Contexto do banco
- Implementações técnicas

---

## Principais Funcionalidades

Este microserviço é responsável por:
- Consumo de mensagens de eventos de notificação
- Processamento assíncrono de notificações
- Registro de histórico de notificações
- Integração com RabbitMQ
- Persistência em SQL Server
- Reprocessamento e recuperação de falhas

---

## Stack Tecnológica

- .NET 9
- BackgroundService / Worker Service
- Entity Framework Core
- SQL Server
- RabbitMQ
- Docker Compose

---

## Padrões Utilizados

- Clean Architecture
- SOLID
- Dependency Injection
- Repository Pattern
- Separation of Concerns
- Event-driven processing

---

## Configuração do Ambiente

### Pré-requisitos

Antes de executar este projeto, certifique-se de ter instalado:
- .NET SDK 9+
- SQL Server
- RabbitMQ
- Docker Desktop
- Visual Studio 2022+ ou Rider

### Exemplo de configuração

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1435;Database=fiapgames_notification;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;Encrypt=False"
  },
  "RabbitMq": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest"
  }
}
```

---

## Executando o Projeto

Restaurar dependências:

```bash
dotnet restore
```

Executar o worker:

```bash
dotnet run --project src/2-Notification.Infrastructure
```

---

## Testes

Este projeto pode ser validado via execução local do worker e verificação do fluxo de mensagens no RabbitMQ.

---

## Licença

Projeto desenvolvido para fins acadêmicos e evolução arquitetural da plataforma FiapGames.
