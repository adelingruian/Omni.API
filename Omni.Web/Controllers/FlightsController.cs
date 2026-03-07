using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Omni.Web.Data;
using Omni.Web.Hubs;
using Omni.Web.Models;

namespace Omni.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FlightsController : ControllerBase
    {
        private const string GateResourceType = "Gate";
        private const string DisruptionStatusOpen = "Open";

        private readonly AppDbContext _context;
        private readonly IHubContext<FlightsHub> _hubContext;

        public FlightsController(AppDbContext context, IHubContext<FlightsHub> hubContext, ILogger<FlightsController> logger)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<FlightResponse>>> GetAll()
        {
            var now = DateTimeOffset.UtcNow;

            var flights = await _context.Flights
                .AsNoTracking()
                .OrderBy(f => f.FlightId)
                .ToListAsync();

            // Gates blocked by disruptions => Unavailable.
            var disruptedGates = await _context.Disruptions
                .AsNoTracking()
                .Where(d =>
                    d.ResourceType == GateResourceType &&
                    d.Status == DisruptionStatusOpen)
                .Select(d => d.ResourceId)
                .Distinct()
                .ToListAsync();

            var disruptedSet = disruptedGates.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Detect overlapping flights per gate => Conflict.
            // Overlap rule: [departure, arrival] intersects with another flight's interval.
            static DateTimeOffset GetDeparture(Flight f) => f.ActualDeparture ?? f.ScheduledDeparture;
            static DateTimeOffset GetArrival(Flight f) => f.ActualArrival ?? f.ScheduledArrival;

            var conflictMap = new Dictionary<int, string>(); // flightId -> other flight number

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
                        // Since list is ordered by Start, once the next Start is after current End, no further overlaps for i.
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

            return flights.Select(f => ToResponse(
                    f,
                    disruptedSet.Contains(f.Gate),
                    conflictMap.TryGetValue(f.FlightId, out var otherFlightNumber) ? otherFlightNumber : null))
                .ToList();
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<FlightResponse>> Get(int id)
        {
            var now = DateTimeOffset.UtcNow;

            var flight = await _context.Flights
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FlightId == id);

            if (flight == null) return NotFound();

            var isGateUnavailable = await _context.Disruptions
                .AsNoTracking()
                .AnyAsync(d =>
                    d.ResourceType == GateResourceType &&
                    d.ResourceId == flight.Gate &&
                    d.Status == DisruptionStatusOpen &&
                    d.StartsAt <= now &&
                    d.EndsAt >= now);

            // Best-effort conflict check for a single flight by pulling same-gate flights.
            var sameGateFlights = await _context.Flights
                .AsNoTracking()
                .Where(f => f.Gate == flight.Gate && f.FlightId != id)
                .ToListAsync();

            static DateTimeOffset GetDeparture(Flight f) => f.ActualDeparture ?? f.ScheduledDeparture;
            static DateTimeOffset GetArrival(Flight f) => f.ActualArrival ?? f.ScheduledArrival;

            var thisStart = GetDeparture(flight);
            var thisEnd = GetArrival(flight);

            var overlapping = sameGateFlights
                .Select(f => new { f.FlightNumber, Start = GetDeparture(f), End = GetArrival(f) })
                .FirstOrDefault(x => thisStart <= x.End && x.Start <= thisEnd);

            return ToResponse(flight, isGateUnavailable, overlapping?.FlightNumber);
        }

        [HttpPost]
        public async Task<ActionResult<Flight>> Create(Flight flight)
        {
            _context.Flights.Add(flight);
            await _context.SaveChangesAsync();

            await BroadcastFlightsUpdated();

            return CreatedAtAction(nameof(Get), new { id = flight.FlightId }, flight);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, Flight flight)
        {
            if (id != flight.FlightId) return BadRequest();

            _context.Entry(flight).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Flights.AnyAsync(e => e.FlightId == id))
                    return NotFound();
                throw;
            }

            await BroadcastFlightsUpdated();

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var flight = await _context.Flights.FindAsync(id);
            if (flight == null) return NotFound();

            _context.Flights.Remove(flight);
            await _context.SaveChangesAsync();

            await BroadcastFlightsUpdated();

            return NoContent();
        }

        private static FlightResponse ToResponse(Flight f, bool isGateUnavailable, string? overlappingFlightNumber)
        {
            var status = GateStatus.Ok;
            string? description = null;

            if (isGateUnavailable)
            {
                status = GateStatus.Unavailable;
                description = "Gate is unavailable";
            }
            else if (!string.IsNullOrWhiteSpace(overlappingFlightNumber))
            {
                status = GateStatus.Conflict;
                description = $"Overlaps with {overlappingFlightNumber}";
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
        }

        private async Task BroadcastFlightsUpdated()
        {
            // Reuse the GET logic so sockets and HTTP match.
            var result = await GetAll();
            if (result.Result is ObjectResult obj && obj.Value is IEnumerable<FlightResponse> payload)
            {
                await _hubContext.Clients.All.SendAsync(FlightsHub.FlightsUpdatedEvent, payload);
            }
        }
    }
}
