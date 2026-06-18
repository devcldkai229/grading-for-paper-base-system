using MongoDB.Bson.Serialization;
using SubmissionService.Domain.Enums;

namespace SubmissionService.Infrastructure.Persistence.Bson;

internal static class SubmissionBsonSerialization
{
    private static bool _registered;

    public static void RegisterSerializers()
    {
        if (_registered)
        {
            return;
        }

        BsonSerializer.RegisterSerializer(new EnumMemberBsonSerializer<BatchStatus>());
        BsonSerializer.RegisterSerializer(new EnumMemberBsonSerializer<PaperStatus>());

        _registered = true;
    }
}
