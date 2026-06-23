namespace SubmissionService.Application;

public static class PaperContentTypes
{
  public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
  {
    "application/pdf",
    "image/png", "image/jpeg", "image/gif", "image/bmp", "image/tiff", "image/webp",
    "text/plain",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
  };

  public static string FromFileName(string fileName)
  {
    var ext = Path.GetExtension(fileName).ToLowerInvariant();
    return ext switch
    {
      ".pdf" => "application/pdf",
      ".png" => "image/png",
      ".jpg" or ".jpeg" => "image/jpeg",
      ".gif" => "image/gif",
      ".bmp" => "image/bmp",
      ".tiff" or ".tif" => "image/tiff",
      ".webp" => "image/webp",
      ".txt" => "text/plain",
      ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      _ => "application/octet-stream"
    };
  }

  public static bool IsAllowed(string fileName, out string contentType)
  {
    contentType = FromFileName(fileName);
    return Allowed.Contains(contentType);
  }
}
