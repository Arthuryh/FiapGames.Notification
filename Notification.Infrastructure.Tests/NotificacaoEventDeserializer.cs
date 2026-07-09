using System.Text.Json;
using IntegrationEvents;

namespace Notification.Infrastructure.Tests;

public static class NotificacaoEventDeserializer
{
    public static NotificacaoIntegrationEvent? Deserialize(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<NotificacaoIntegrationEvent>(json, options);
    }
}
