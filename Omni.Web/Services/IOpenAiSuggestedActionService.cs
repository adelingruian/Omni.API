using Omni.Web.Models;

namespace Omni.Web.Services
{
    public interface IOpenAiSuggestedActionService
    {
        Task<IReadOnlyList<AiPlannedUpdateSlimResponse>> GetSuggestedActionsAsync(CancellationToken cancellationToken = default);
    }
}
