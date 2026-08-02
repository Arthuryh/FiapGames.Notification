using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Context;
using Interfaces;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using Repository;
using Workers;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    var keyVaultUriValue = Environment.GetEnvironmentVariable("KeyVaultUri");
    if (!Uri.TryCreate(keyVaultUriValue, UriKind.Absolute, out var keyVaultUri) ||
        keyVaultUri.Scheme != Uri.UriSchemeHttps)
    {
        throw new InvalidOperationException(
            "Environment variable KeyVaultUri must contain a valid HTTPS URI.");
    }

    builder.Configuration.AddAzureKeyVault(
        keyVaultUri,
        new DefaultAzureCredential());
}

var rabbitHost = builder.Configuration["RabbitMq:HostName"] ?? "localhost";
var rabbitPort = int.TryParse(builder.Configuration["RabbitMq:Port"], out var parsedPort) ? parsedPort : 5672;
var rabbitUser = builder.Configuration["RabbitMq:UserName"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMq:Password"] ?? "guest";

var rabbitConnectionFactory = new ConnectionFactory
{
    HostName = rabbitHost,
    Port = rabbitPort,
    UserName = rabbitUser,
    Password = rabbitPass,
    AutomaticRecoveryEnabled = true,
    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
};
var rabbitHealthConnection = new Lazy<Task<IConnection>>(
    () => rabbitConnectionFactory.CreateConnectionAsync());

builder.Services.AddSingleton<IConnectionFactory>(rabbitConnectionFactory);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string DefaultConnection is required.");
}

// Registra o Entity Framework (Lembre-se de adicionar a string de conexão no appsettings.json)
builder.Services.AddDbContext<NotificacaoDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlServerOptionsAction: sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 20,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddSqlServer(connectionString, name: "sqlserver", tags: ["ready"])
    .AddRabbitMQ(
        _ => rabbitHealthConnection.Value,
        name: "rabbitmq",
        tags: ["ready"]);

// Registra o Repositório
builder.Services.AddScoped<IHistoricoNotificacaoRepository, HistoricoNotificacaoRepository>();

// Registra o Worker
builder.Services.AddHostedService<NotificacaoGeralWorker>();
builder.Services.AddHostedService<NotificacaoAutenticacaoWorker>();

var app = builder.Build();

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<NotificacaoDbContext>();

    await context.Database.MigrateAsync();

    Console.WriteLine("Notification database migrations applied successfully.");
    return;
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

await app.RunAsync();
