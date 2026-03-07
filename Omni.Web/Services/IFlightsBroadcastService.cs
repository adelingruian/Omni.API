using Omni.Web.Models;

namespace Omni.Web.Services
{
    public interface IFlightsBroadcastService
    {
        Task BroadcastFlightsUpdatedAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<FlightResponse>> BuildFlightsPayloadAsync(CancellationToken cancellationToken = default);
    }
}
