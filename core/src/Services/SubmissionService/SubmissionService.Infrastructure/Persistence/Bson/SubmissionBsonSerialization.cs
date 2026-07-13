using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using SubmissionService.Domain.Entities;
using SubmissionService.Domain.Enums;

namespace SubmissionService.Infrastructure.Persistence.Bson;

internal static class SubmissionBsonSerialization
{
    private static bool _registered;
    private static readonly GuidSerializer StringGuid = new(BsonType.String);

    public static void RegisterSerializers()
    {
        if (_registered)
        {
            return;
        }

        BsonSerializer.RegisterSerializer(new EnumMemberBsonSerializer<BatchStatus>());
        BsonSerializer.RegisterSerializer(new EnumMemberBsonSerializer<PaperStatus>());

        RegisterClassMaps();

        _registered = true;
    }

    // SubmissionBatch, PaperFile and SubmissionAuditLog are plain Domain entities (no Mongo
    // attributes — Domain must stay persistence-agnostic), so their wire format (element names,
    // Guid-as-string) is reproduced here explicitly instead of via [BsonElement]/[BsonId]. Every
    // member is mapped one-to-one with what the old attribute-based documents produced, so
    // existing collection data keeps deserializing unchanged.
    private static void RegisterClassMaps()
    {
        BsonClassMap.RegisterClassMap<SubmissionBatch>(cm =>
        {
            cm.MapIdMember(b => b.Id).SetSerializer(StringGuid);
            cm.MapMember(b => b.SubjectId).SetElementName("subject_id").SetSerializer(StringGuid);
            cm.MapMember(b => b.ZipS3Key).SetElementName("zip_s3_key");
            cm.MapMember(b => b.ZipFileName).SetElementName("zip_file_name");
            cm.MapMember(b => b.TotalPapers).SetElementName("total_papers");
            cm.MapMember(b => b.Status).SetElementName("status");
            cm.MapMember(b => b.UploadedBy).SetElementName("uploaded_by").SetSerializer(StringGuid);
            cm.MapMember(b => b.ErrorMessage).SetElementName("error_message");
            cm.MapMember(b => b.DuplicateWarnings).SetElementName("duplicate_warnings");
            cm.MapMember(b => b.CreatedAt).SetElementName("created_at");
            cm.MapMember(b => b.UpdatedAt).SetElementName("updated_at");
        });

        BsonClassMap.RegisterClassMap<DuplicateFileWarning>(cm =>
        {
            cm.MapMember(w => w.ContentHash).SetElementName("content_hash");
            cm.MapMember(w => w.Files).SetElementName("files");
        });

        BsonClassMap.RegisterClassMap<DuplicateFileEntry>(cm =>
        {
            cm.MapMember(e => e.StudentPaperId).SetElementName("student_paper_id").SetSerializer(StringGuid);
            cm.MapMember(e => e.StudentAlias).SetElementName("student_alias");
            cm.MapMember(e => e.FileName).SetElementName("file_name");
        });

        BsonClassMap.RegisterClassMap<PaperFile>(cm =>
        {
            cm.MapIdMember(f => f.Id).SetSerializer(StringGuid);
            cm.MapMember(f => f.StudentPaperId).SetElementName("student_paper_id").SetSerializer(StringGuid);
            cm.MapMember(f => f.S3Key).SetElementName("s3_key");
            cm.MapMember(f => f.FileName).SetElementName("file_name");
            cm.MapMember(f => f.ContentType).SetElementName("content_type");
            cm.MapMember(f => f.SizeBytes).SetElementName("size_bytes");
            cm.MapMember(f => f.ContentHash).SetElementName("content_hash");
            cm.MapMember(f => f.OrderIndex).SetElementName("order_index");
            cm.MapMember(f => f.CreatedAt).SetElementName("created_at");
        });

        BsonClassMap.RegisterClassMap<SubmissionAuditLog>(cm =>
        {
            cm.MapIdMember(l => l.Id).SetSerializer(StringGuid);
            cm.MapMember(l => l.Action).SetElementName("action");
            cm.MapMember(l => l.EntityType).SetElementName("entity_type");
            cm.MapMember(l => l.EntityId).SetElementName("entity_id").SetSerializer(StringGuid);
            cm.MapMember(l => l.SubjectId).SetElementName("subject_id").SetSerializer(StringGuid);
            cm.MapMember(l => l.PerformedBy).SetElementName("performed_by").SetSerializer(StringGuid);
            cm.MapMember(l => l.Details).SetElementName("details");
            cm.MapMember(l => l.PerformedAt).SetElementName("performed_at");
        });
    }
}
