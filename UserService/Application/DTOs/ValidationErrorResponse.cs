namespace UserService.Application.DTOs;

/// <summary>
/// Response DTO for validation errors
/// </summary>
public class ValidationErrorResponse
{
    public int StatusCode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public Dictionary<string, string[]> Errors { get; set; } = new();
}








