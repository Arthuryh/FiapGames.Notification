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
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptionsAction: sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 20,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));

// Registra o Repositório
builder.Services.AddScoped<IHistoricoNotificacaoRepository, HistoricoNotificacaoRepository>();

// Registra o Worker
builder.Services.AddHostedService<NotificacaoGeralWorker>();

var host = builder.Build();
await InitializeDatabaseAsync(host);
await host.RunAsync();

static async Task InitializeDatabaseAsync(IHost host)
{
    var logger = host.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseInitialization");

    const int maxAttempts = 20;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var scope = host.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NotificacaoDbContext>();

            await context.Database.MigrateAsync();
            logger.LogInformation("Database initialized successfully.");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(ex,
                "Database initialization failed (attempt {Attempt}/{MaxAttempts}). Retrying in 5 seconds...",
                attempt,
                maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }

    throw new InvalidOperationException("Failed to initialize the notification database.");
}