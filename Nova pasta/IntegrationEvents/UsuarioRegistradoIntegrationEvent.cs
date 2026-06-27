namespace IntegrationEvents;

public record UsuarioRegistradoIntegrationEvent(
    string Nome,
    string Email,
    int TipoUsuario
);