using System.Reflection;

namespace SubmissionService.Infrastructure.Persistence.Bson;

internal static class MongoCollectionNames
{
    public static string For<TDocument>()
    {
        var attribute = typeof(TDocument).GetCustomAttribute<BsonCollectionAttribute>()
            ?? throw new InvalidOperationException(
                $"Document type {typeof(TDocument).Name} is missing {nameof(BsonCollectionAttribute)}.");

        return attribute.CollectionName;
    }
}
