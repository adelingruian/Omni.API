using Omni.Web.Models;

namespace Omni.Web.Services
{
    public interface IFlightDelayService
    {
        Task<Dictionary<int, int>> CalculatePossibleDelaysAsync(CancellationToken cancellationToken = default);
    }
}
