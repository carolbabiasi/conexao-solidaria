using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GestorONG.Infrastructure.Persistencia.Mongo;

public sealed class DoacaoProcessada
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("idDoacao")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid IdDoacao { get; set; }

    [BsonElement("idCampanha")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid IdCampanha { get; set; }

    [BsonElement("idDoador")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid IdDoador { get; set; }

    [BsonElement("valor")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Valor { get; set; }

    [BsonElement("processadoEm")]
    public DateTime ProcessadoEm { get; set; }
}
