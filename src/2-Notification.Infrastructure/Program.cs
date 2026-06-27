using Context;
using Interfaces;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using Repository;
using Workers;


var builder = Host.CreateApplicationBuilder(args);

// 1. LER AS CONFIGURAÇÕES DO APPSETTINGS.JSON
var rabbitHost = builder.Configuration["RabbitMq:HostName"];
var rabbitPort = int.Parse(builder.Configuration["RabbitMq:Port"] ?? "5672");
var rabbitUser = builder.Configuration["RabbitMq:UserName"];
var rabbitPass = builder.Configuration["RabbitMq:Password"];

// 2. CONFIGURAR A CONEXÃO USANDO AS VARIÁVEIS LIDAS
builder.Services.AddSingleton<IConnectionFactory>(sp => new ConnectionFactory
{
    HostName = rabbitHost,
    Port = rabbitPort,
    UserName = rabbitUser,
    Password = rabbitPass,
    AutomaticRecoveryEnabled = true,
    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
});

// Registra o Entity Framework (Lembre-se de adicionar a string de conexão no appsettings.json)
builder.Services.AddDbContext<NotificacaoDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Registra o Repositório
builder.Services.AddScoped<IHistoricoNotificacaoRepository, HistoricoNotificacaoRepository>();

// Registra o Worker
builder.Services.AddHostedService<NotificacaoGeralWorker>();

var host = builder.Build();
await host.RunAsync();