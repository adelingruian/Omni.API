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

            var disruptedGates = await _context.Disruptions
                .AsNoTracking()
                .Where(d =>
                    d.ResourceType == GateResourceType &&
                    d.Status == DisruptionStatusOpen &&
                    d.StartsAt <= now &&
                    d.EndsAt >= now)
                .Select(d => d.ResourceId)
                .Distinct()
                .ToListAsync();

            var disruptedSet = disruptedGates.ToHashSet(StringComparer.OrdinalIgnoreCase);

            static DateTimeOffset GetDeparture(Flight f) => f.ActualDeparture ?? f.ScheduledDeparture;
            static DateTimeOffset GetArrival(Flight f) => f.ActualArrival ?? f.ScheduledArrival;

            var conflictMap = new Dictionary<int, string>();

            foreach (var group in flights.GroupBy(f => f.Gate, StringComparer.OrdinalIgnoreCase))
            {
                var list = group
                    .Select(f => new { Flight = f, Start = GetDeparture(f), End = GetArrival(f) })
                    .OrderBy(x => x.Start)
                    .ToList();

                for (var i = 0; i < list.Count; i++)
                {
                    for (var j = i + 1; j < list.Count; j++)
                    {
                        if (list[j].Start > list[i].End)
                            break;

                        var a = list[i];
                        var b = list[j];

                        var overlaps = a.Start <= b.End && b.Start <= a.End;
                        if (!overlaps)
                            continue;

                        conflictMap.TryAdd(a.Flight.FlightId, b.Flight.FlightNumber);
                        conflictMap.TryAdd(b.Flight.FlightId, a.Flight.FlightNumber);
                    }
                }
            }

            var payload = flights.Select(f =>
            {
                var isGateUnavailable = disruptedSet.Contains(f.Gate);
                var hasConflict = conflictMap.TryGetValue(f.FlightId, out var otherFlight);

                var status = GateStatus.Ok;
                string? description = null;

                if (isGateUnavailable)
                {
                    status = GateStatus.Unavailable;
                    description = "Gate is unavailable";
                }
                else if (hasConflict)
                {
                    status = GateStatus.Conflict;
                    description = $"Overlaps with {otherFlight}";
                }

                return new FlightResponse(
                    f.FlightId,
                    f.FlightNumber,
                    f.Aircraft,
                    f.Origin,
                    f.Destination,
                    f.ScheduledDeparture,
                    f.ActualDeparture,
                    f.ScheduledArrival,
                    f.ActualArrival,
                    new GateResponse(f.Gate, status, description),
                    f.Runway,
                    f.PassengerNumber,
                    f.DelayMinutes,
                    f.CrewPilots,
                    f.CrewFlightAttendants,
                    f.BaggageConveyorBelt,
                    f.BaggageTotalChecked);
            }).ToList();

            await _hubContext.Clients.All.SendAsync(FlightsHub.FlightsUpdatedEvent, payload);
        }
    }
}
