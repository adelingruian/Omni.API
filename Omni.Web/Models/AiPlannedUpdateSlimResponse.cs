namespace Omni.Web.Models
{
    /// <summary>
    /// Slim AI response: one suggestion per flight.
    /// </summary>
    public sealed record AiPlannedUpdateSlimResponse(
        int FlightId,
        string Description,
        AiToolCallSlimResponse? Tool);

    /// <summary>
    /// Slim tool call contract: tool name + parameters object.
    /// </summary>
    public sealed record AiToolCallSlimResponse(
        string Name,
        object Parameters);
}
