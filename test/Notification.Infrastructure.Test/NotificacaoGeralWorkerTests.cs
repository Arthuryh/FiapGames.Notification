using IntegrationEvents;
using Workers;
using Xunit;

namespace Notification.Infrastructure.Test;

public class NotificacaoGeralWorkerTests
{
    [Fact]
    public void CriarNotificacao_DeveGerarAssuntoEStatusDeSucesso_QuandoPagamentoAprovado()
    {
        var evento = new PagamentoProcessadoIntegrationEvent(
            CompraId: 123,
            UsuarioId: 10,
            Aprovado: true,
            ValorTotal: 199.90m,
            Status: "Aprovado",
            ProcessadoEm: DateTime.UtcNow,
            RastreioId: Guid.NewGuid().ToString(),
            MotivoRecusa: null,
            EmailUsuario: "cliente@fiapgames.com");

        var draft = NotificacaoGeralWorker.CriarNotificacao(evento);

        Assert.Equal("cliente@fiapgames.com", draft.Destinatario);
        Assert.Equal("Compra aprovada no FiapGames (#123)", draft.Assunto);
        Assert.Equal("Sucesso", draft.StatusHistorico);
        Assert.Equal("Enviando e-mail de confirmação para {Email} da compra {CompraId}", draft.MensagemLog);
        Assert.Equal(2, draft.MensagemLogArgs.Length);
    }

    [Fact]
    public void CriarNotificacao_DeveGerarAssuntoEStatusDeRecusa_QuandoPagamentoRecusado()
    {
        var evento = new PagamentoProcessadoIntegrationEvent(
            CompraId: 456,
            UsuarioId: 33,
            Aprovado: false,
            ValorTotal: 59.90m,
            Status: "Reprovado",
            ProcessadoEm: DateTime.UtcNow,
            RastreioId: Guid.NewGuid().ToString(),
            MotivoRecusa: "Saldo insuficiente",
            EmailUsuario: "jogador@fiapgames.com");

        var draft = NotificacaoGeralWorker.CriarNotificacao(evento);

        Assert.Equal("jogador@fiapgames.com", draft.Destinatario);
        Assert.Equal("Compra recusada no FiapGames (#456)", draft.Assunto);
        Assert.Equal("Recusado", draft.StatusHistorico);
        Assert.Equal("Enviando e-mail de recusa para {Email} da compra {CompraId}. Motivo: {Motivo}", draft.MensagemLog);
        Assert.Equal(3, draft.MensagemLogArgs.Length);
        Assert.Equal("Saldo insuficiente", draft.MensagemLogArgs[2]);
    }

    [Fact]
    public void CriarNotificacao_DeveUsarFallbackDeEmail_QuandoEmailNaoVierNoEvento()
    {
        var evento = new PagamentoProcessadoIntegrationEvent(
            CompraId: 789,
            UsuarioId: 77,
            Aprovado: false,
            ValorTotal: 39.90m,
            Status: "Reprovado",
            ProcessadoEm: DateTime.UtcNow,
            RastreioId: Guid.NewGuid().ToString(),
            MotivoRecusa: null,
            EmailUsuario: null);

        var draft = NotificacaoGeralWorker.CriarNotificacao(evento);

        Assert.Equal("usuario-77@fiapgames.local", draft.Destinatario);
        Assert.Equal("Nao informado", draft.MensagemLogArgs[2]);
    }
}
