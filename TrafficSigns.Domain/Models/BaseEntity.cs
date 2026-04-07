namespace TrafficSigns.Domain.Models;

public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public bool Inactive { get; set; }
    public DateTime CreatedDt { get; set; }
    public DateTime UpdatedDt { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }

    public void AddMetadataLog(string key, string newEntry)
    {
        var newMetadata = Metadata != null
            ? new Dictionary<string, string>(Metadata)
            : new Dictionary<string, string>();

        newMetadata[key] = newEntry;

        Metadata = newMetadata;
    }
}