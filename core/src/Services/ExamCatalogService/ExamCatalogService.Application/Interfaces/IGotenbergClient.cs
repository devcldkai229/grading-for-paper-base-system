namespace ExamCatalogService.Application.Interfaces;

public interface IGotenbergClient
{
    /// <summary>
    /// Convert a DOCX stream to PDF bytes via Gotenberg LibreOffice.
    /// </summary>
    Task<byte[]> ConvertDocxToPdfAsync(Stream docxStream, string fileName, CancellationToken ct = default);
}
