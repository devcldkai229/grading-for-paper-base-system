using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using SubmissionService.Domain.Entities;
using SubmissionService.Domain.Enums;
using SubmissionService.Infrastructure.Persistence.Bson;
using Xunit;

namespace SubmissionService.UnitTests
{
    /// <summary>
    /// Locks in that SubmissionBatch/PaperFile/SubmissionAuditLog — moved from attribute-decorated
    /// Mongo documents in Infrastructure to plain Domain entities mapped via BsonClassMap — still
    /// produce byte-identical wire format (element names, Guid-as-string) so existing collection
    /// data keeps deserializing unchanged.
    /// </summary>
    public class MongoDocumentParityTests
    {
        public MongoDocumentParityTests()
        {
            SubmissionBsonSerialization.RegisterSerializers();
        }

        private static void AssertGuidAsString(BsonDocument doc, string elementName)
        {
            Assert.Equal(BsonType.String, doc[elementName].BsonType);
            Assert.True(Guid.TryParse(doc[elementName].AsString, out _));
        }

        [Fact]
        public void SubmissionBatch_SerializesWithExpectedElementNamesAndGuidStrings()
        {
            var batch = new SubmissionBatch
            {
                Id = Guid.NewGuid(),
                SubjectId = Guid.NewGuid(),
                ZipS3Key = "submissions/abc/batch.zip",
                ZipFileName = "papers.zip",
                TotalPapers = 3,
                Status = BatchStatus.Ready,
                UploadedBy = Guid.NewGuid(),
                ErrorMessage = null,
                DuplicateWarnings = new List<DuplicateFileWarning>
                {
                    new()
                    {
                        ContentHash = "hash123",
                        Files = new List<DuplicateFileEntry>
                        {
                            new() { StudentPaperId = Guid.NewGuid(), StudentAlias = "Student_0001", FileName = "1.pdf" }
                        }
                    }
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var doc = batch.ToBsonDocument();

            var expectedNames = new[]
            {
                "_id", "subject_id", "zip_s3_key", "zip_file_name", "total_papers", "status",
                "uploaded_by", "error_message", "duplicate_warnings", "created_at", "updated_at"
            };
            Assert.Equal(expectedNames.OrderBy(n => n), doc.Names.OrderBy(n => n));

            AssertGuidAsString(doc, "_id");
            AssertGuidAsString(doc, "subject_id");
            AssertGuidAsString(doc, "uploaded_by");
            Assert.Equal(BsonType.String, doc["status"].BsonType);

            var warningDoc = doc["duplicate_warnings"].AsBsonArray[0].AsBsonDocument;
            Assert.Equal(
                new[] { "content_hash", "files" }.OrderBy(n => n),
                warningDoc.Names.OrderBy(n => n));

            var entryDoc = warningDoc["files"].AsBsonArray[0].AsBsonDocument;
            Assert.Equal(
                new[] { "student_paper_id", "student_alias", "file_name" }.OrderBy(n => n),
                entryDoc.Names.OrderBy(n => n));
            AssertGuidAsString(entryDoc, "student_paper_id");
        }

        [Fact]
        public void PaperFile_SerializesWithExpectedElementNamesAndGuidStrings()
        {
            var file = new PaperFile
            {
                Id = Guid.NewGuid(),
                StudentPaperId = Guid.NewGuid(),
                S3Key = "submissions/abc/Student_0001/1.pdf",
                FileName = "1.pdf",
                ContentType = "application/pdf",
                SizeBytes = 12345,
                ContentHash = "hash456",
                OrderIndex = 0,
                CreatedAt = DateTime.UtcNow
            };

            var doc = file.ToBsonDocument();

            var expectedNames = new[]
            {
                "_id", "student_paper_id", "s3_key", "file_name", "content_type",
                "size_bytes", "content_hash", "order_index", "created_at"
            };
            Assert.Equal(expectedNames.OrderBy(n => n), doc.Names.OrderBy(n => n));

            AssertGuidAsString(doc, "_id");
            AssertGuidAsString(doc, "student_paper_id");
        }

        [Fact]
        public void SubmissionAuditLog_SerializesWithExpectedElementNamesAndGuidStrings()
        {
            var log = new SubmissionAuditLog
            {
                Id = Guid.NewGuid(),
                Action = "DeleteBatch",
                EntityType = "Batch",
                EntityId = Guid.NewGuid(),
                SubjectId = Guid.NewGuid(),
                PerformedBy = Guid.NewGuid(),
                Details = "Deleted batch 'papers.zip' (2 paper(s))",
                PerformedAt = DateTime.UtcNow
            };

            var doc = log.ToBsonDocument();

            var expectedNames = new[]
            {
                "_id", "action", "entity_type", "entity_id", "subject_id",
                "performed_by", "details", "performed_at"
            };
            Assert.Equal(expectedNames.OrderBy(n => n), doc.Names.OrderBy(n => n));

            AssertGuidAsString(doc, "_id");
            AssertGuidAsString(doc, "entity_id");
            AssertGuidAsString(doc, "subject_id");
            AssertGuidAsString(doc, "performed_by");
        }
    }
}
