using System.Text.Json;

namespace Omni.Web.Models
{
    /// <summary>
    /// A single tool call emitted by the AI. The FE can display Description + Parameters,
    /// then submit the same ToolName + Parameters back to an API to apply the change.
    /// </summary>
    public sealed record AiToolCallResponse(
        string ToolCallId,
        string Description,
        string ToolName,
        IReadOnlyList<AiToolParameter> Parameters);

    /// <summary>
    /// Simple name/value pair to render in the UI and to send back for execution.
    /// Value is kept as JsonElement to preserve original types.
    /// </summary>
    public sealed record AiToolParameter(
        string Name,
        JsonElement Value);
}
