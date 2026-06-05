namespace EasyRedmineTool.Core.Models;

public class ConnectionTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? UserDisplayName { get; set; }
}
