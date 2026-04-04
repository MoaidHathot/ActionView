using System.Text.Json;
using System.Text.Json.Serialization;

namespace ActionView.Core.Models;

[JsonSerializable(typeof(Entry))]
[JsonSerializable(typeof(List<Entry>))]
[JsonSerializable(typeof(ContentBlock))]
[JsonSerializable(typeof(EntryAction))]
[JsonSerializable(typeof(ActionCommand))]
[JsonSerializable(typeof(EntryOutcome))]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(DashboardStats))]
[JsonSerializable(typeof(ActionExecutionResult))]
[JsonSerializable(typeof(EntryTemplate))]
[JsonSerializable(typeof(List<EntryTemplate>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
public partial class ActionViewJsonContext : JsonSerializerContext;

/// <summary>
/// Dashboard statistics for the frontend.
/// </summary>
public sealed class DashboardStats
{
    public int TotalPending { get; set; }
    public int TotalViewed { get; set; }
    public Dictionary<string, int> CountByType { get; set; } = new();
    public Dictionary<string, int> CountBySeverity { get; set; } = new();
}

/// <summary>
/// Result returned after executing an action.
/// </summary>
public sealed class ActionExecutionResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int? StatusCode { get; set; }
    public string? Output { get; set; }
}
