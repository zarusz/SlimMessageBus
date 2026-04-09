namespace SlimMessageBus.Host.Outbox.MongoDb;

/// <summary>
/// Internal MongoDB document representing a stored outbox message.
/// </summary>
[BsonIgnoreExtraElements]
internal class MongoDbOutboxDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("timestamp")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Timestamp { get; set; }

    [BsonElement("bus_name")]
    public string BusName { get; set; } = null!;

    [BsonElement("message_type")]
    public string MessageType { get; set; } = null!;

    [BsonElement("message_payload")]
    public byte[] MessagePayload { get; set; } = null!;

    /// <summary>
    /// Headers serialized as JSON string (nullable when no headers present).
    /// </summary>
    [BsonElement("headers")]
    public string? Headers { get; set; }

    [BsonElement("path")]
    public string? Path { get; set; }

    [BsonElement("lock_instance_id")]
    public string? LockInstanceId { get; set; }

    [BsonElement("lock_expires_on")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LockExpiresOn { get; set; }

    [BsonElement("delivery_attempt")]
    public int DeliveryAttempt { get; set; }

    [BsonElement("delivery_complete")]
    public bool DeliveryComplete { get; set; }

    [BsonElement("delivery_aborted")]
    public bool DeliveryAborted { get; set; }
}

/// <summary>
/// Document tracking which schema migrations have been successfully applied.
/// </summary>
[BsonIgnoreExtraElements]
internal class MongoDbMigrationDocument
{
    /// <summary>Migration identifier, e.g. "20240101000000_SMB_Init".</summary>
    [BsonId]
    public string Id { get; set; } = null!;

    [BsonElement("applied_at")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime AppliedAt { get; set; }

    [BsonElement("product_version")]
    public string ProductVersion { get; set; } = null!;
}

/// <summary>
/// Document used as a global table lock to ensure sequential (single-instance) message processing.
/// </summary>
[BsonIgnoreExtraElements]
internal class MongoDbOutboxLockDocument
{
    [BsonId]
    public string Id { get; set; } = "global";

    [BsonElement("lock_instance_id")]
    public string? LockInstanceId { get; set; }

    [BsonElement("lock_expires_on")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LockExpiresOn { get; set; }
}
