namespace SubmissionService.Infrastructure.Persistence.Bson;

[AttributeUsage(AttributeTargets.Class)]
public sealed class BsonCollectionAttribute(string collectionName) : Attribute
{
    public string CollectionName { get; } = collectionName;
}
