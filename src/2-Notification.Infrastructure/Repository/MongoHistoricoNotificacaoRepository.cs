using System.Text.Json;
using Entities;
using Interfaces;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Repository;

public class MongoHistoricoNotificacaoRepository : IHistoricoNotificacaoRepository
{
    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoHistoricoNotificacaoRepository(IConfiguration configuration)
    {
        var connectionString = configuration["MongoDb:ConnectionString"];
        var databaseName = configuration["MongoDb:DatabaseName"] ?? "fiapgames_notifications";
        var collectionName = configuration["MongoDb:CollectionName"] ?? "HistoricoNotificacoes";

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("MongoDb:ConnectionString is required when using Mongo persistence.");

        var mongoClient = new MongoClient(connectionString);
        var database = mongoClient.GetDatabase(databaseName);
        _collection = database.GetCollection<BsonDocument>(collectionName);
    }

    public async Task SalvarAsync(HistoricoNotificacao historico)
    {
        var document = new BsonDocument
        {
            ["_id"] = BsonValue.Create(historico.Id.ToString()),
            ["RastreioId"] = historico.RastreioId,
            ["Destinatario"] = historico.Destinatario,
            ["Assunto"] = historico.Assunto,
            ["DataEnvio"] = historico.DataEnvio,
            ["Status"] = historico.Status,
            ["Payload"] = JsonSerializer.Serialize(new
            {
                historico.Id,
                historico.RastreioId,
                historico.Destinatario,
                historico.Assunto,
                historico.DataEnvio,
                historico.Status
            })
        };

        await _collection.InsertOneAsync(document);
    }
}
