using MongoDB.Driver;
using SubmissionService.Application.DTOs;
using SubmissionService.Application.Interfaces;
using SubmissionService.Domain.Entities;
using SubmissionService.Domain.Enums;
using SubmissionService.Infrastructure.Persistence.Bson;
using SubmissionService.Infrastructure.Persistence.Documents;

namespace SubmissionService.Infrastructure.Repositories;

public class StudentPaperRepository : IStudentPaperRepository
{
    private readonly IMongoDatabase _database;

    public StudentPaperRepository(IMongoDatabase database)
    {
        _database = database;
    }

    private IMongoCollection<StudentPaper> PapersCollection =>
        _database.GetCollection<StudentPaper>(MongoCollectionNames.For<StudentPaper>());

    private IMongoCollection<PaperFile> FilesCollection =>
        _database.GetCollection<PaperFile>(MongoCollectionNames.For<PaperFile>());

    public async Task<(IReadOnlyList<StudentPaperDto> Items, int TotalCount)> GetPapersAsync(
        Guid subjectId, string? status, int page, int pageSize,
        Guid? uploadedByFilter = null,
        CancellationToken ct = default)
    {
        var filterBuilder = Builders<StudentPaper>.Filter;
        var filter = filterBuilder.Eq(p => p.SubjectId, subjectId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusEnum = Enum.Parse<PaperStatus>(status, ignoreCase: true);
            filter &= filterBuilder.Eq(p => p.Status, statusEnum);
        }

        if (uploadedByFilter.HasValue)
        {
            filter &= filterBuilder.Eq(p => p.UploadedBy, uploadedByFilter.Value);
        }

        var totalCount = await PapersCollection.CountDocumentsAsync(filter, cancellationToken: ct);

        var papers = await PapersCollection
            .Find(filter)
            .SortBy(p => p.AliasNumber)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        var paperIds = papers.Select(p => p.Id).ToList();
        var fileCounts = new Dictionary<Guid, int>();
        if (paperIds.Count > 0)
        {
            var fileFilter = Builders<PaperFile>.Filter.In(f => f.StudentPaperId, paperIds);
            var files = await FilesCollection.Find(fileFilter).ToListAsync(ct);
            fileCounts = files.GroupBy(f => f.StudentPaperId)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        var items = papers.Select(p => new StudentPaperDto(
            p.Id, p.BatchId, p.SubjectId, p.StudentAlias, p.AliasNumber,
            p.Status.ToString(),
            fileCounts.GetValueOrDefault(p.Id, 0),
            p.CreatedAt
        )).ToList();

        return (items, (int)totalCount);
    }

    public async Task<(IReadOnlyList<StudentPaperDto> Items, int TotalCount)> SearchPapersAsync(
        string keyword, int page, int pageSize,
        Guid? uploadedByFilter = null,
        CancellationToken ct = default)
    {
        var filterBuilder = Builders<StudentPaper>.Filter;

        var escapedKeyword = System.Text.RegularExpressions.Regex.Escape(keyword);
        var keywordFilter = filterBuilder.Regex(
            p => p.StudentAlias, new MongoDB.Bson.BsonRegularExpression(escapedKeyword, "i"));

        if (int.TryParse(keyword, out var aliasNumber))
        {
            keywordFilter |= filterBuilder.Eq(p => p.AliasNumber, aliasNumber);
        }

        var filter = keywordFilter;
        if (uploadedByFilter.HasValue)
        {
            filter &= filterBuilder.Eq(p => p.UploadedBy, uploadedByFilter.Value);
        }

        var totalCount = await PapersCollection.CountDocumentsAsync(filter, cancellationToken: ct);

        var papers = await PapersCollection
            .Find(filter)
            .SortByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        var paperIds = papers.Select(p => p.Id).ToList();
        var fileCounts = new Dictionary<Guid, int>();
        if (paperIds.Count > 0)
        {
            var fileFilter = Builders<PaperFile>.Filter.In(f => f.StudentPaperId, paperIds);
            var files = await FilesCollection.Find(fileFilter).ToListAsync(ct);
            fileCounts = files.GroupBy(f => f.StudentPaperId)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        var items = papers.Select(p => new StudentPaperDto(
            p.Id, p.BatchId, p.SubjectId, p.StudentAlias, p.AliasNumber,
            p.Status.ToString(),
            fileCounts.GetValueOrDefault(p.Id, 0),
            p.CreatedAt
        )).ToList();

        return (items, (int)totalCount);
    }

    public async Task<BatchPapersDto?> GetPapersByBatchAsync(Guid batchId, CancellationToken ct = default)
    {
        var batchCollection = _database.GetCollection<SubmissionBatch>(
            MongoCollectionNames.For<SubmissionBatch>());
        var batch = await batchCollection.Find(b => b.Id == batchId).FirstOrDefaultAsync(ct);
        if (batch is null) return null;

        var papers = await PapersCollection
            .Find(p => p.BatchId == batchId)
            .SortBy(p => p.AliasNumber)
            .ToListAsync(ct);

        return new BatchPapersDto(
            batch.Id,
            batch.SubjectId,
            batch.UploadedBy,
            papers.Select(p => new BatchPaperDto(p.Id, p.SubjectId, p.AliasNumber)).ToList());
    }

    public async Task<InternalPaperSummaryDto?> GetPaperSummaryAsync(Guid paperId, CancellationToken ct = default)
    {
        var paper = await PapersCollection.Find(p => p.Id == paperId).FirstOrDefaultAsync(ct);
        if (paper is null) return null;

        return new InternalPaperSummaryDto(
            paper.Id, paper.BatchId, paper.SubjectId,
            paper.StudentAlias, paper.AliasNumber, paper.UploadedBy);
    }

    public async Task<(Guid PaperId, string StudentAlias)> CreateSinglePaperWithFilesAsync(
        Guid batchId, Guid subjectId, Guid uploadedBy, int aliasNumber,
        IReadOnlyList<(string FileName, string ContentType, long SizeBytes, string S3Key)> files,
        CancellationToken ct = default)
    {
        var studentAlias = $"Student_{aliasNumber:D4}";
        var paperId = Guid.NewGuid();

        await PapersCollection.InsertOneAsync(new StudentPaper
        {
            Id = paperId,
            BatchId = batchId,
            SubjectId = subjectId,
            StudentAlias = studentAlias,
            AliasNumber = aliasNumber,
            UploadedBy = uploadedBy,
            Status = PaperStatus.ReadyToAssign,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken: ct);

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];

            await FilesCollection.InsertOneAsync(new PaperFile
            {
                Id = Guid.NewGuid(),
                StudentPaperId = paperId,
                S3Key = file.S3Key,
                FileName = file.FileName,
                ContentType = file.ContentType,
                SizeBytes = file.SizeBytes,
                OrderIndex = i,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken: ct);
        }

        return (paperId, studentAlias);
    }

    public async Task<StudentPaperDetailDto?> GetPaperDetailAsync(Guid paperId,
        CancellationToken ct = default)
    {
        var paper = await PapersCollection
            .Find(p => p.Id == paperId)
            .FirstOrDefaultAsync(ct);

        if (paper is null) return null;

        var files = await FilesCollection
            .Find(f => f.StudentPaperId == paperId)
            .SortBy(f => f.OrderIndex)
            .ToListAsync(ct);

        var fileDtos = files.Select(f => new PaperFileDto(
            f.Id, f.FileName, f.ContentType, f.SizeBytes, f.OrderIndex
        )).ToList();

        return new StudentPaperDetailDto(
            paper.Id, paper.BatchId, paper.SubjectId,
            paper.StudentAlias, paper.AliasNumber,
            paper.Status.ToString(), fileDtos, paper.CreatedAt);
    }

    public async Task<(string S3Key, string FileName, string ContentType)?> GetPaperFileInfoAsync(
        Guid paperId, Guid fileId, CancellationToken ct = default)
    {
        var filter = Builders<PaperFile>.Filter.Eq(f => f.Id, fileId)
                   & Builders<PaperFile>.Filter.Eq(f => f.StudentPaperId, paperId);

        var file = await FilesCollection.Find(filter).FirstOrDefaultAsync(ct);
        if (file is null) return null;

        return (file.S3Key, file.FileName ?? "unknown", file.ContentType);
    }

    public async Task<PaperDeletionInfoDto?> GetPaperForDeletionAsync(Guid paperId, CancellationToken ct = default)
    {
        var paper = await PapersCollection.Find(p => p.Id == paperId).FirstOrDefaultAsync(ct);
        if (paper is null) return null;

        var files = await FilesCollection.Find(f => f.StudentPaperId == paperId).ToListAsync(ct);

        return new PaperDeletionInfoDto(
            paper.Id, paper.BatchId, paper.SubjectId, paper.UploadedBy, paper.Status.ToString(),
            files.Select(f => new PaperFileRefDto(f.Id, f.S3Key)).ToList());
    }

    public async Task<IReadOnlyList<PaperDeletionInfoDto>> GetPapersForDeletionAsync(Guid batchId, CancellationToken ct = default)
    {
        var papers = await PapersCollection.Find(p => p.BatchId == batchId).ToListAsync(ct);
        if (papers.Count == 0) return [];

        var paperIds = papers.Select(p => p.Id).ToList();
        var files = await FilesCollection
            .Find(Builders<PaperFile>.Filter.In(f => f.StudentPaperId, paperIds))
            .ToListAsync(ct);
        var filesByPaper = files.GroupBy(f => f.StudentPaperId)
            .ToDictionary(g => g.Key, g => g.Select(f => new PaperFileRefDto(f.Id, f.S3Key)).ToList());

        return papers.Select(p => new PaperDeletionInfoDto(
            p.Id, p.BatchId, p.SubjectId, p.UploadedBy, p.Status.ToString(),
            (IReadOnlyList<PaperFileRefDto>)filesByPaper.GetValueOrDefault(p.Id, [])
        )).ToList();
    }

    public async Task DeletePaperAsync(Guid paperId, CancellationToken ct = default)
    {
        await FilesCollection.DeleteManyAsync(f => f.StudentPaperId == paperId, ct);
        await PapersCollection.DeleteOneAsync(p => p.Id == paperId, ct);
    }

    public async Task DeletePapersByBatchAsync(Guid batchId, CancellationToken ct = default)
    {
        var paperIds = await PapersCollection.Find(p => p.BatchId == batchId)
            .Project(p => p.Id)
            .ToListAsync(ct);

        if (paperIds.Count > 0)
        {
            await FilesCollection.DeleteManyAsync(
                Builders<PaperFile>.Filter.In(f => f.StudentPaperId, paperIds), ct);
        }

        await PapersCollection.DeleteManyAsync(p => p.BatchId == batchId, ct);
    }

    public async Task<SubjectPaperStatsDto> GetSubjectPaperStatsAsync(Guid subjectId, CancellationToken ct = default)
    {
        var filter = Builders<StudentPaper>.Filter.Eq(p => p.SubjectId, subjectId);

        var totalPapers = (int)await PapersCollection.CountDocumentsAsync(filter, cancellationToken: ct);
        if (totalPapers == 0)
        {
            return new SubjectPaperStatsDto(0, null);
        }

        var highestAliasPaper = await PapersCollection
            .Find(filter)
            .SortByDescending(p => p.AliasNumber)
            .Limit(1)
            .FirstOrDefaultAsync(ct);

        return new SubjectPaperStatsDto(totalPapers, highestAliasPaper?.AliasNumber);
    }
}
