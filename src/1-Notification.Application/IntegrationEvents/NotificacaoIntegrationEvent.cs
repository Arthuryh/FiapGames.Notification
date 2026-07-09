using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntegrationEvents;

public record NotificacaoIntegrationEvent(
    [property: JsonPropertyName("correlacaoId")] Guid CorrelacaoId,
    [property: JsonPropertyName("destinatario")] string Destinatario,
    [property: JsonPropertyName("assunto")] string Assunto,
    [property: JsonPropertyName("corpoMensagem")] string CorpoMensagem,
    [property: JsonPropertyName("dominioOrigem")] string DominioOrigem)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static NotificacaoIntegrationEvent? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<NotificacaoIntegrationEvent>(json, SerializerOptions);
    }
}