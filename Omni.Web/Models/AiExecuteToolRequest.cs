using System.Text.Json;

namespace Omni.Web.Models
{
    /// <summary>
    /// Request body for executing an AI tool suggestion.
    /// Parameters must match the chosen tool's JSON schema.
    /// </summary>
    public sealed record AiExecuteToolRequest(
        string ToolName,
        JsonElement Parameters);
}
