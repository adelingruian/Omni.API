namespace Omni.Web.Models
{
    /// <summary>
    /// Read-only contract returned by the AI suggestion endpoint.
    /// ActionType should be a canonical code (e.g. ReassignGate, DelayPushback).
    /// </summary>
    public sealed record AiSuggestedActionResponse(
        int FlightId,
        string FlightNumber,
        string ActionType,
        string Reason,
        int EstimatedDelayReductionMinutes);
}
