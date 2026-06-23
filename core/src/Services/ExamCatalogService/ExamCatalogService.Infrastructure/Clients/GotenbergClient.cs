using System.Net.Http.Headers;
using ExamCatalogService.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ExamCatalogService.Infrastructure.Clients;

public class GotenbergClient : IGotenbergClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GotenbergClient> _logger;

    public GotenbergClient(HttpClient httpClient, ILogger<GotenbergClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<byte[]> ConvertDocxToPdfAsync(
        Stream docxStream, string fileName, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(docxStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "files", fileName);

        var response = await _httpClient.PostAsync("/forms/libreoffice/convert", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Gotenberg conversion failed ({StatusCode}): {Body}",
                response.StatusCode,
                body);
            throw new InvalidOperationException(
                $"DOCX to PDF conversion failed (HTTP {(int)response.StatusCode}).");
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }
}
