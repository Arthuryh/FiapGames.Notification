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
        logger.LogInformation("Processing authentication notification. InvocationId: {InvocationId}", context.InvocationId);
        await processor.ProcessAuthenticationAsync(payload, cancellationToken);
    }
}
