using System.Reflection;
using SubmissionService.Domain.Entities;

namespace SubmissionService.Infrastructure.Persistence.Bson;

internal static class MongoCollectionNames
{
    // Domain entities carry no Mongo attributes (Domain must stay persistence-agnostic), so their
    // collection names are registered explicitly here instead of via BsonCollectionAttribute.
    private static readonly IReadOnlyDictionary<Type, string> ExplicitNames = new Dictionary<Type, string>
    {
        [typeof(SubmissionBatch)] = "submission_batches",
        [typeof(PaperFile)] = "paper_files",
        [typeof(SubmissionAuditLog)] = "submission_audit_logs",
    };

    public static string For<TDocument>()
    {
        if (ExplicitNames.TryGetValue(typeof(TDocument), out var explicitName))
        {
            return explicitName;
        }

        var attribute = typeof(TDocument).GetCustomAttribute<BsonCollectionAttribute>()
            ?? throw new InvalidOperationException(
                $"Document type {typeof(TDocument).Name} is missing {nameof(BsonCollectionAttribute)}.");

        return attribute.CollectionName;
    }
}
