namespace Omni.Web.Models
{
    /// <summary>
    /// Minimal tool/function specification to send to the AI so it can emit tool calls.
    /// This is intentionally small and JSON-serializable.
    /// </summary>
    public sealed record AiToolSpec(
        string Name,
        string Description,
        object ParametersJsonSchema);
}
