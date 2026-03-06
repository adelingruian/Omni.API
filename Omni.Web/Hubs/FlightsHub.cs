using Microsoft.AspNetCore.SignalR;

namespace Omni.Web.Hubs
{
    public class FlightsHub : Hub
    {
        public const string HubPath = "/hubs/flights";
        public const string FlightsUpdatedEvent = "FlightsUpdated";
    }
}
