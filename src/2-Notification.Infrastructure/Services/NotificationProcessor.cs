using System.Text.Json;
using Entities;
using IntegrationEvents;
using Interfaces;
using Microsoft.Extensions.Logging;

namespace Services;

public sealed class NotificationProcessor(
    IHistoricoNotificacaoRepository repository,
    ILogger<NotificationProcessor> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task ProcessPaymentAsync(string payload, CancellationToken cancellationToken = default)
    {
        var evento = JsonSerializer.Deserialize<PagamentoProcessadoIntegrationEvent>(payload, JsonOptions)
            ?? throw new JsonException("Payment notification event is null.");
        var draft = CreatePaymentNotification(evento);

        await repository.SalvarAsync(new HistoricoNotificacao(
            evento.RastreioId,
            draft.Destinatario,
            draft.Assunto,
            DateTime.UtcNow,
            draft.StatusHistorico));

        logger.LogInformation("Payment notification persisted for {Recipient}.", draft.Destinatario);
    }

    public async Task ProcessAuthenticationAsync(string payload, CancellationToken cancellationToken = default)
    {
        var evento = NotificacaoIntegrationEvent.Deserialize(payload)
            ?? throw new JsonException("Authentication notification event is null.");

        await repository.SalvarAsync(new HistoricoNotificacao(
            evento.CorrelacaoId.ToString(),
            evento.Destinatario,
            evento.Assunto,
            DateTime.UtcNow,
            "Sucesso"));

        logger.LogInformation("Authentication notification persisted for {Recipient}.", evento.Destinatario);
    }

    public static NotificationDraft CreatePaymentNotification(PagamentoProcessadoIntegrationEvent evento)
    {
        var recipient = string.IsNullOrWhiteSpace(evento.EmailUsuario)
            ? $"usuario-{evento.UsuarioId}@fiapgames.local"
            : evento.EmailUsuario;

        return evento.Aprovado
            ? new(recipient, $"Compra aprovada no FiapGames (#{evento.CompraId})", "Sucesso")
            : new(recipient, $"Compra recusada no FiapGames (#{evento.CompraId})", "Recusado");
    }

    public sealed record NotificationDraft(string Destinatario, string Assunto, string StatusHistorico);
}
