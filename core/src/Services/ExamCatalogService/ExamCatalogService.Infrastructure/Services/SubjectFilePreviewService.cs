using ExamCatalogService.Application.Interfaces;

namespace ExamCatalogService.Infrastructure.Services;

public class SubjectFilePreviewService
{
    private readonly IS3Service _s3Service;
    private readonly IGotenbergClient _gotenbergClient;

    public SubjectFilePreviewService(IS3Service s3Service, IGotenbergClient gotenbergClient)
    {
        _s3Service = s3Service;
        _gotenbergClient = gotenbergClient;
    }

    public static bool IsDocx(string contentType) =>
        contentType.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase);

    public async Task<(string PreviewS3Key, string PreviewContentType)> CreatePreviewAsync(
        Guid subjectId,
        string originalS3Key,
        string originalContentType,
        string fileName,
        Stream fileStream,
        bool isRubric,
        int rubricVersion,
        CancellationToken ct = default)
    {
        if (IsDocx(originalContentType))
        {
            var previewKey = isRubric
                ? $"rubrics/{subjectId}/v{rubricVersion}/preview.pdf"
                : $"exam-papers/{subjectId}/preview.pdf";

            var pdfBytes = await _gotenbergClient.ConvertDocxToPdfAsync(fileStream, fileName, ct);
            await _s3Service.UploadAsync(
                previewKey, new MemoryStream(pdfBytes), "application/pdf", ct);
            return (previewKey, "application/pdf");
        }

        return (originalS3Key, originalContentType);
    }

    public static string GetPreviewDisplayFileName(string originalFileName, string previewContentType)
    {
        if (previewContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !originalFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Path.ChangeExtension(originalFileName, ".pdf");
        }

        return originalFileName;
    }
}
