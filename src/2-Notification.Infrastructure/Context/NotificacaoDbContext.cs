using Entities;
using Microsoft.EntityFrameworkCore;

namespace Context;

public class NotificacaoDbContext : DbContext
{
    public NotificacaoDbContext(DbContextOptions<NotificacaoDbContext> options) : base(options) { }

    public DbSet<HistoricoNotificacao> Historicos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configuração simples para mapear a tabela
        modelBuilder.Entity<HistoricoNotificacao>(e =>
        {
            e.ToTable("HistoricoNotificacoes");
            e.HasKey(x => x.Id);
            e.Property(x => x.RastreioId).HasMaxLength(100).IsRequired();
            e.Property(x => x.Destinatario).HasMaxLength(150).IsRequired();
            e.Property(x => x.Status).HasMaxLength(50).IsRequired();
        });
    }
}