using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Omni.Web.Data;
using Omni.Web.Hubs;
using Omni.Web.Models;

namespace Omni.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DisruptionsController : ControllerBase
    {
        private const string GateResourceType = "Gate";
        private const string DisruptionStatusOpen = "Open";

        private readonly AppDbContext _context;
        private readonly IHubContext<FlightsHub> _hubContext;

        public DisruptionsController(AppDbContext context, IHubContext<FlightsHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public sealed record CreateDisruptionRequest(string ResourceType, string ResourceId);

        [HttpPost]
        public async Task<ActionResult<Disruption>> Create(CreateDisruptionRequest request)
        {
            var todayUtc = DateTimeOffset.UtcNow.Date;

            var disruption = new Disruption
            {
                ResourceType = request.ResourceType,
                ResourceId = request.ResourceId,
                StartsAt = todayUtc,
                EndsAt = todayUtc.AddDays(1),
                Status = DisruptionStatusOpen
            };

            _context.Disruptions.Add(disruption);
            await _context.SaveChangesAsync();

            await BroadcastFlightsUpdated();

            return CreatedAtAction(nameof(GetAll), new { id = disruption.DisruptionId }, disruption);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Disruption>>> GetAll()
        {
            var result = await _context.Disruptions
                .AsNoTracking()
                .ToListAsync();

            return result.OrderBy(r => r.StartsAt).ToList();
        }

        [HttpPost("{id:int}/solve")]
        public async Task<IActionResult> Solve(int id)
        {
            var disruption = await _context.Disruptions.FindAsync(id);
            if (disruption == null) return NotFound();

            disruption.Status = "Solved";
            await _context.SaveChangesAsync();

            await BroadcastFlightsUpdated();


            return NoContent();
        }

        private async Task BroadcastFlightsUpdated()
        {
            var now = DateTimeOffset.UtcNow;

            var flights = await _context.Flights
                .AsNoTracking()
                .OrderBy(f => f.FlightId)
                .ToListAsync();

            var gateIdsUpper = flights
                .Select(f => (f.Gate ?? string.Empty).ToUpper())
                .Distinct()
                .ToList();

            var disruptedGatesUpper = await _context.Disruptions
                .AsNoTracking()
                .Where(d =>
                    d.ResourceType == GateResourceType &&
                    gateIdsUpper.Contains(d.ResourceId.ToUpper()) &&
                    d.Status == DisruptionStatusOpen)
                .Select(d => d.ResourceId.ToUpper())
                .Distinct()
                .ToListAsync();

            var disruptedSetUpper = disruptedGatesUpper.ToHashSet();

            var payload = flights
                .Select(f => new FlightResponse(
                    f.FlightId,
                    f.FlightNumber,
                    f.Aircraft,
                    f.Origin,
                    f.Destination,
                    f.ScheduledDeparture,
                    f.ActualDeparture,
                    f.ScheduledArrival,
                    f.ActualArrival,
                    new GateResponse(f.Gate, disruptedSetUpper.Contains((f.Gate ?? string.Empty).ToUpper()) ? "Disrupted" : "Ok"),
                    f.Runway,
                    f.PassengerNumber,
                    f.DelayMinutes,
                    f.CrewPilots,
                    f.CrewFlightAttendants,
                    f.BaggageConveyorBelt,
                    f.BaggageTotalChecked))
                .ToList();

            await _hubContext.Clients.All.SendAsync(FlightsHub.FlightsUpdatedEvent, payload);
        }
    }
}
