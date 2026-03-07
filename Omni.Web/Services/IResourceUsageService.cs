using Omni.Web.Models;

namespace Omni.Web.Services
{
    public interface IResourceUsageService
    {
        Task UpsertForFlightAsync(Flight flight, CancellationToken cancellationToken = default);
        Task DeleteForFlightAsync(int flightId, CancellationToken cancellationToken = default);
    }
}
