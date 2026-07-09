using Entities;
using IntegrationEvents;
using Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace Workers;

public class NotificacaoGeralWorker : BackgroundService
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<NotificacaoGeralWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    private IConnection? _connection;
    private IChannel? _channel;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string ExchangeName = "pagamento.exchange";
    private const string QueueName = "notificacao.pagamento.processado";
    private readonly string[] _routingKeys = { "pagamento.aprovado", "pagamento.recusado" };

    private const string DlxExchangeName = "pagamento.notificacao.dlx.exchange";
    private const string DlqQueueName = "notificacao.pagamento.processado.dlq";
    private const string DlxRoutingKey = "pagamento.notificacao.falha";

    internal sealed record NotificacaoDraft(
        string Destinatario,
        string Assunto,
        string StatusHistorico,
        string MensagemLog,
        object[] MensagemLogArgs);

    public NotificacaoGeralWorker(IConnectionFactory connectionFactory, ILogger<NotificacaoGeralWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Conectando ao RabbitMQ...");
                _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken: stoppingToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await ConfigurarTopologiaAsync();

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += OnMessageReceivedAsync;

                await _channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

                _logger.LogInformation("Worker conectado ao RabbitMQ e consumindo a fila {Queue}.", QueueName);

                while (!stoppingToken.IsCancellationRequested && _connection is not null && _connection.IsOpen && _channel is not null && _channel.IsOpen)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }

                _logger.LogWarning("Conexão com RabbitMQ fechada ou indisponível. Tentando reconectar...");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao conectar ao RabbitMQ. Tentando novamente em 5 segundos...");
            }
            finally
            {
                await DisposeChannelAndConnectionAsync();
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async ValueTask DisposeChannelAndConnectionAsync()
    {
        if (_channel is not null)
        {
            try
            {
                await _channel.CloseAsync();
            }
            catch
            {
                // Ignora falhas ao fechar o canal durante a reconexão.
            }

            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null)
        {
            try
            {
                await _connection.CloseAsync();
            }
            catch
            {
                // Ignora falhas ao fechar a conexão durante a reconexão.
            }

            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    private async Task ConfigurarTopologiaAsync()
    {
        await _channel!.ExchangeDeclareAsync(ExchangeName, ExchangeType.Direct, true, false);
        await _channel.ExchangeDeclareAsync(DlxExchangeName, ExchangeType.Direct, true, false);

        await _channel.QueueDeclareAsync(DlqQueueName, true, false, false, null);
        await _channel.QueueBindAsync(DlqQueueName, DlxExchangeName, DlxRoutingKey);

        var mainQueueArguments = new Dictionary<string, object?>
        {
            { "x-dead-letter-exchange", DlxExchangeName },
            { "x-dead-letter-routing-key", DlxRoutingKey }
        };
        await _channel.QueueDeclareAsync(QueueName, true, false, false, mainQueueArguments);

        foreach (var routingKey in _routingKeys)
        {
            await _channel.QueueBindAsync(QueueName, ExchangeName, routingKey);
            _logger.LogInformation("Fila {Queue} conectada à Exchange {Exchange} com a chave {Key}", QueueName, ExchangeName, routingKey);
        }

        await _channel.BasicQosAsync(0, 1, false);
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var rastreioId = ea.BasicProperties.CorrelationId ?? "SEM-RASTREIO";

        using var logScope = _logger.BeginScope(new Dictionary<string, object> { ["RastreioId"] = rastreioId });

        string? json = null;

        try
        {
            var body = ea.Body.ToArray();
            json = System.Text.Encoding.UTF8.GetString(body);
            var evento = JsonSerializer.Deserialize<PagamentoProcessadoIntegrationEvent>(json, JsonOptions);

            if (evento is null) throw new JsonException("Evento nulo.");

            var draft = CriarNotificacao(evento);
            _logger.LogInformation(draft.MensagemLog, draft.MensagemLogArgs);

            await Task.Delay(1000);

            using (var scope = _scopeFactory.CreateScope())
            {
                var repositorio = scope.ServiceProvider.GetRequiredService<IHistoricoNotificacaoRepository>();

                var historico = new HistoricoNotificacao(
                    rastreioId,
                    draft.Destinatario,
                    draft.Assunto,
                    DateTime.UtcNow,
                    draft.StatusHistorico);

                await repositorio.SalvarAsync(historico);
            }

            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
            _logger.LogInformation("Notificação salva no histórico com sucesso.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mensagem da fila {Queue}. Payload: {Payload}", QueueName, json);
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }

    internal static NotificacaoDraft CriarNotificacao(PagamentoProcessadoIntegrationEvent evento)
    {
        var destinatario = string.IsNullOrWhiteSpace(evento.EmailUsuario)
            ? $"usuario-{evento.UsuarioId}@fiapgames.local"
            : evento.EmailUsuario;

        if (evento.Aprovado)
        {
            return new NotificacaoDraft(
                Destinatario: destinatario,
                Assunto: $"Compra aprovada no FiapGames (#{evento.CompraId})",
                StatusHistorico: "Sucesso",
                MensagemLog: "Enviando e-mail de confirmação para {Email} da compra {CompraId}",
                MensagemLogArgs: [destinatario, evento.CompraId]);
        }

        return new NotificacaoDraft(
            Destinatario: destinatario,
            Assunto: $"Compra recusada no FiapGames (#{evento.CompraId})",
            StatusHistorico: "Recusado",
            MensagemLog: "Enviando e-mail de recusa para {Email} da compra {CompraId}. Motivo: {Motivo}",
            MensagemLogArgs: [destinatario, evento.CompraId, evento.MotivoRecusa ?? "Nao informado"]);
    }

    public override async void Dispose()
    {
        await DisposeChannelAndConnectionAsync();
        base.Dispose();
    }
}