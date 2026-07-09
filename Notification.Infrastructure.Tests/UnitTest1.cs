using IntegrationEvents;

namespace Notification.Infrastructure.Tests;

public class NotificacaoIntegrationEventDeserializerTests
{
    [Fact]
    public void Deserialize_DeveAceitarPropriedadesEmCamelCase()
    {
        const string json = """
        {
          "correlacaoId": "8a5f3f4a-2d9d-4f33-b2da-5f2167092d15",
          "destinatario": "cliente@exemplo.com",
          "assunto": "Pagamento aprovado",
          "corpoMensagem": "Seu pagamento foi aprovado.",
          "dominioOrigem": "Pagamento"
        }
        """;

        var evento = NotificacaoIntegrationEvent.Deserialize(json);

        Assert.NotNull(evento);
        Assert.Equal("cliente@exemplo.com", evento!.Destinatario);
        Assert.Equal("Pagamento aprovado", evento.Assunto);
        Assert.Equal("Pagamento", evento.DominioOrigem);
    }
}
