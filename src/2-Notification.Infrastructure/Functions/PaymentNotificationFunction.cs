using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Services;

namespace Functions;

public sealed class PaymentNotificationFunction(NotificationProcessor processor, ILogger<PaymentNotificationFunction> logger)
{
    [Function(nameof(PaymentNotificationFunction))]
    public async Task Run(
        [ServiceBusTrigger("%PaymentNotificationQueueName%", Connection = "NotificationServiceBus")] string payload,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing payment notification. InvocationId: {InvocationId}", context.InvocationId);
        await processor.ProcessPaymentAsync(payload, cancellationToken);
    }
}
