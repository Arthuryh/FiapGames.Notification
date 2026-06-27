using Entities;

namespace Interfaces;

public interface IHistoricoNotificacaoRepository
{
    Task SalvarAsync(HistoricoNotificacao historico);
}