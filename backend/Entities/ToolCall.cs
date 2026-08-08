namespace FreightLink.Api.Entities;

public class ToolCall
{
    public Guid ToolCallId { get; set; }
    public Guid AgentStepId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public int AttemptNo { get; set; }
    public string? RequestJson { get; set; }
    public string? ResponseJson { get; set; }
    public bool Success { get; set; }
    public int? HttpStatusCode { get; set; }
    public int? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CalledAt { get; set; }

    public AgentStep AgentStep { get; set; } = null!;
}
