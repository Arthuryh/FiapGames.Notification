namespace IntegrationEvents;

public record PagamentoProcessadoIntegrationEvent(
    int CompraId,
    int UsuarioId,
    bool Aprovado,
    decimal ValorTotal,
    string Status,
    DateTime ProcessadoEm,
    string RastreioId,
    string? MotivoRecusa,
    string? EmailUsuario = null);
