namespace EasyRedmineTool.Core.Models;

public class LoginResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? UserDisplayName { get; set; }
}
