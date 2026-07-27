namespace SubmissionService.Application;

/// <summary>A file to upload, decoupled from ASP.NET Core's IFormFile so Application stays framework-agnostic.</summary>
public sealed record UploadFileInput(string FileName, long Length, Stream Content);
