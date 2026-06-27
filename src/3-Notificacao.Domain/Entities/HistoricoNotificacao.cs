namespace Entities;

public class HistoricoNotificacao
{
    public Guid Id { get; private set; }
    public string RastreioId { get; private set; }
    public string Destinatario { get; private set; }
    public string Assunto { get; private set; }
    public DateTime DataEnvio { get; private set; }
    public string Status { get; private set; }

    // Construtor para garantir que a entidade nasça válida
    public HistoricoNotificacao(string rastreioId, string destinatario, string assunto, DateTime dataEnvio, string status)
    {
        Id = Guid.NewGuid();
        RastreioId = rastreioId;
        Destinatario = destinatario;
        Assunto = assunto;
        DataEnvio = dataEnvio;
        Status = status;
    }

    // Método de domínio para caso precise atualizar o status depois
    public void MarcarComoFalha()
    {
        Status = "Falha";
    }
}