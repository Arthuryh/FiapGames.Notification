using Context;
using Entities;
using Interfaces;

namespace Repository;

public class HistoricoNotificacaoRepository : IHistoricoNotificacaoRepository
{
    private readonly NotificacaoDbContext _context;

    public HistoricoNotificacaoRepository(NotificacaoDbContext context)
    {
        _context = context;
    }

    public async Task SalvarAsync(HistoricoNotificacao historico)
    {
        await _context.Historicos.AddAsync(historico);
        await _context.SaveChangesAsync();
    }
}