using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Services;

namespace Functions;

public sealed class AuthenticationNotificationFunction(NotificationProcessor processor, ILogger<AuthenticationNotificationFunction> logger)
{
    [Function(nameof(AuthenticationNotificationFunction))]
    public async Task Run(
        [ServiceBusTrigger("%AuthenticationNotificationQueueName%", Connection = "NotificationServiceBus")] string payload,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["FunctionName"] = nameof(AuthenticationNotificationFunction),
            ["InvocationId"] = context.InvocationId
        });
        logger.LogInformation("Processing authentication notification");
        await processor.ProcessAuthenticationAsync(payload, cancellationToken);
        logger.LogInformation("Authentication notification processed successfully");
    }
}
