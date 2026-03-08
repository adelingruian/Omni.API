namespace Omni.Web.Models
{
    /// <summary>
    /// AI response: one suggestion per flight. Each suggestion may include a single tool call.
    /// The controller returns the array directly.
    /// </summary>
    public sealed record AiPlannedUpdateResponse(
        int FlightId,
        string FlightNumber,
        string ActionType,
        string Reason,
        int EstimatedDelayReductionMinutes,
        AiToolCallResponse? ToolCall);
}
