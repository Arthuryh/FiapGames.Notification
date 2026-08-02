using System.Text;
using IntegrationEvents;
using Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Workers;

public sealed class NotificacaoAutenticacaoWorker : BackgroundService
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<NotificacaoAutenticacaoWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    private IConnection? _connection;
    private IChannel? _channel;

    private const string ExchangeName = "notificacao.exchange";
    private const string QueueName = "autenticacao.notificacao";
    private const string RoutingKey = "autenticacao.notificacao";
    private const string DlxExchangeName = "autenticacao.notificacao.dlx.exchange";
    private const string DlqQueueName = "autenticacao.notificacao.dlq";
    private const string DlxRoutingKey = "autenticacao.notificacao.falha";

    public NotificacaoAutenticacaoWorker(
        IConnectionFactory connectionFactory,
        ILogger<NotificacaoAutenticacaoWorker> logger,
        IServiceScopeFactory scopeFactory)
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
                _logger.LogInformation("Conectando ao RabbitMQ para consumir notificacoes de autenticacao...");
                _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken: stoppingToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await ConfigurarTopologiaAsync(stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += OnMessageReceivedAsync;

                await _channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

                _logger.LogInformation("Worker de autenticacao aguardando mensagens na fila {Queue}.", QueueName);

                while (!stoppingToken.IsCancellationRequested && _connection.IsOpen && _channel.IsOpen)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
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

    private async Task ConfigurarTopologiaAsync(CancellationToken cancellationToken)
    {
        await _channel!.ExchangeDeclareAsync(ExchangeName, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await _channel.ExchangeDeclareAsync(DlxExchangeName, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        await _channel.QueueDeclareAsync(DlqQueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await _channel.QueueBindAsync(DlqQueueName, DlxExchangeName, DlxRoutingKey, cancellationToken: cancellationToken);

        var mainQueueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = DlxExchangeName,
            ["x-dead-letter-routing-key"] = DlxRoutingKey
        };

        await _channel.QueueDeclareAsync(
            QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueArguments,
            cancellationToken: cancellationToken);
        await _channel.QueueBindAsync(QueueName, ExchangeName, RoutingKey, cancellationToken: cancellationToken);
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        string? json = null;

        try
        {
            var body = ea.Body.ToArray();
            json = Encoding.UTF8.GetString(body);
            var evento = NotificacaoIntegrationEvent.Deserialize(json);

            if (evento is null)
            {
                throw new InvalidOperationException("Evento de notificacao de autenticacao nulo.");
            }

            _logger.LogInformation(
                "Enviando e-mail de boas-vindas para {Email}. Assunto: {Assunto}",
                evento.Destinatario,
                evento.Assunto);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var repositorio = scope.ServiceProvider.GetRequiredService<IHistoricoNotificacaoRepository>();

            var historico = new Entities.HistoricoNotificacao(
                evento.CorrelacaoId.ToString(),
                evento.Destinatario,
                evento.Assunto,
                DateTime.UtcNow,
                "Sucesso");

            await repositorio.SalvarAsync(historico);

            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
            _logger.LogInformation("Notificacao de autenticacao registrada com sucesso para {Email}.", evento.Destinatario);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mensagem da fila {Queue}. Payload: {Payload}", QueueName, json);
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
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
            }

            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
