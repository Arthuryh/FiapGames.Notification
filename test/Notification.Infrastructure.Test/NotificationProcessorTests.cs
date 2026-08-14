using IntegrationEvents;
using Services;
using Xunit;

namespace Notification.Infrastructure.Test;

public class NotificationProcessorTests
{
    [Fact]
    public void CreatePaymentNotification_ReturnsApprovedDraft()
    {
        var draft = NotificationProcessor.CreatePaymentNotification(CreateEvent(true, "cliente@fiapgames.com"));

        Assert.Equal("cliente@fiapgames.com", draft.Destinatario);
        Assert.Equal("Compra aprovada no FiapGames (#123)", draft.Assunto);
        Assert.Equal("Sucesso", draft.StatusHistorico);
    }

    [Fact]
    public void CreatePaymentNotification_ReturnsRejectedDraft()
    {
        var draft = NotificationProcessor.CreatePaymentNotification(CreateEvent(false, "jogador@fiapgames.com"));

        Assert.Equal("jogador@fiapgames.com", draft.Destinatario);
        Assert.Equal("Compra recusada no FiapGames (#123)", draft.Assunto);
        Assert.Equal("Recusado", draft.StatusHistorico);
    }

    [Fact]
    public void CreatePaymentNotification_UsesFallbackRecipient()
    {
        var draft = NotificationProcessor.CreatePaymentNotification(CreateEvent(false, null));
        Assert.Equal("usuario-10@fiapgames.local", draft.Destinatario);
    }

    private static PagamentoProcessadoIntegrationEvent CreateEvent(bool approved, string? email) => new(
        CompraId: 123,
        UsuarioId: 10,
        Aprovado: approved,
        ValorTotal: 199.90m,
        Status: approved ? "Aprovado" : "Reprovado",
        ProcessadoEm: DateTime.UtcNow,
        RastreioId: Guid.NewGuid().ToString(),
        MotivoRecusa: approved ? null : "Saldo insuficiente",
        EmailUsuario: email);
}
