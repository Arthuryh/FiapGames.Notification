using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Context;
using Interfaces;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Repository;
using Services;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

if (!builder.Environment.IsDevelopment())
{
    var keyVaultUriValue = Environment.GetEnvironmentVariable("KeyVaultUri");
    if (!Uri.TryCreate(keyVaultUriValue, UriKind.Absolute, out var keyVaultUri) ||
        keyVaultUri.Scheme != Uri.UriSchemeHttps)
    {
        throw new InvalidOperationException("Environment variable KeyVaultUri must contain a valid HTTPS URI.");
    }

    builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
}

var connectionString = builder.Configuration.GetConnectionString("NotificationConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string NotificationConnection is required.");
}

builder.Services.AddDbContext<NotificacaoDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(20, TimeSpan.FromSeconds(10), null)));
builder.Services.AddScoped<IHistoricoNotificacaoRepository, HistoricoNotificacaoRepository>();
builder.Services.AddScoped<NotificationProcessor>();

var host = builder.Build();

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = host.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<NotificacaoDbContext>();
    await context.Database.MigrateAsync();
    Console.WriteLine("Notification database migrations applied successfully.");
    return;
}

await host.RunAsync();
