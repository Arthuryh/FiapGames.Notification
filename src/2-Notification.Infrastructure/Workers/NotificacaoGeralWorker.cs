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

    // 1. Constantes conforme o FLUXOGRAMA
    private const string ExchangeName = "notificacao.exchange";
    private const string QueueName = "notificacao.queue";

    // 2. Routing Keys que esta fila vai escutar
    private readonly string[] _routingKeys = { "autenticacao.notificacao", "pagamento.notificacao" };

    // 3. Topologia de Erro (DLQ)
    private const string DlxExchangeName = "notificacao.dlx.exchange";
    private const string DlqQueueName = "notificacao.dlq";
    private const string DlxRoutingKey = "notificacao.falha";

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
        // A. Declara as Exchanges (A principal e a de erro)
        await _channel!.ExchangeDeclareAsync(ExchangeName, ExchangeType.Direct, true, false);
        await _channel.ExchangeDeclareAsync(DlxExchangeName, ExchangeType.Direct, true, false);

        // B. Configura a DLQ
        await _channel.QueueDeclareAsync(DlqQueueName, true, false, false, null);
        await _channel.QueueBindAsync(DlqQueueName, DlxExchangeName, DlxRoutingKey);

        // C. Configura a Fila Principal apontando falhas para a DLX
        var mainQueueArguments = new Dictionary<string, object?>
        {
            { "x-dead-letter-exchange", DlxExchangeName },
            { "x-dead-letter-routing-key", DlxRoutingKey }
        };
        await _channel.QueueDeclareAsync(QueueName, true, false, false, mainQueueArguments);

        // D. O SEGREDO DO FLUXOGRAMA: Múltiplos Binds na mesma fila!
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

        try
        {
            var body = ea.Body.ToArray();
            var json = System.Text.Encoding.UTF8.GetString(body);
            var notificacao = JsonSerializer.Deserialize<NotificacaoIntegrationEvent>(json);

            if (notificacao is null) throw new JsonException("Evento nulo.");

            _logger.LogInformation("Enviando e-mail para {Email}", notificacao.Destinatario);
            await Task.Delay(1000); // Simulando o envio

            
            using (var scope = _scopeFactory.CreateScope())
            {
                
                var repositorio = scope.ServiceProvider.GetRequiredService<IHistoricoNotificacaoRepository>();

                var historico = new HistoricoNotificacao(
                    rastreioId,
                    notificacao.Destinatario,
                    notificacao.Assunto,
                    DateTime.UtcNow,
                    "Sucesso");

                await repositorio.SalvarAsync(historico);
            }

            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
            _logger.LogInformation("Notificação salva no histórico com sucesso.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no envio.");
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }

    public override async void Dispose()
    {
        await DisposeChannelAndConnectionAsync();
        base.Dispose();
    }
}