namespace IntegrationEvents;

public record NotificacaoIntegrationEvent(
    Guid CorrelacaoId,     // O ID do Login ou do Pagamento (para rastreabilidade)
    string Destinatario,   // O e-mail ou telefone do cliente
    string Assunto,        // Ex: "Bem-vindo!" ou "Pagamento Aprovado"
    string CorpoMensagem,  // O texto final ou template HTML
    string DominioOrigem   // Ex: "Autenticacao" ou "Pagamento"
);