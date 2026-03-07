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
        private const string RunwayResourceType = "Runway";
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
            var flights = await _context.Flights
                .AsNoTracking()
                .OrderBy(f => f.FlightId)
                .ToListAsync();

            var gatesById = await _context.Gates
                .AsNoTracking()
                .ToDictionaryAsync(g => g.GateId, g => g.Name);

            var runwaysById = await _context.Runways
                .AsNoTracking()
                .ToDictionaryAsync(r => r.RunwayId, r => r.Name);

            var disruptedGates = await _context.Disruptions
                .AsNoTracking()
                .Where(d => d.ResourceType == GateResourceType && d.Status == DisruptionStatusOpen)
                .Select(d => d.ResourceId)
                .Distinct()
                .ToListAsync();

            var disruptedRunways = await _context.Disruptions
                .AsNoTracking()
                .Where(d => d.ResourceType == RunwayResourceType && d.Status == DisruptionStatusOpen)
                .Select(d => d.ResourceId)
                .Distinct()
                .ToListAsync();

            var disruptedGateSet = disruptedGates.ToHashSet();
            var disruptedRunwaySet = disruptedRunways.ToHashSet();

            // Detect overlaps => Conflict.
            static DateTimeOffset GetDeparture(Flight f) => f.ActualDeparture ?? f.ScheduledDeparture;
            static DateTimeOffset GetArrival(Flight f) => f.ActualArrival ?? f.ScheduledArrival;

            static Dictionary<int, string> BuildConflictMap(IEnumerable<Flight> flights, Func<Flight, int> keySelector)
            {
                var map = new Dictionary<int, string>();

                foreach (var group in flights.GroupBy(keySelector))
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

                            map.TryAdd(a.Flight.FlightId, b.Flight.FlightNumber);
                            map.TryAdd(b.Flight.FlightId, a.Flight.FlightNumber);
                        }
                    }
                }

                return map;
            }

            var gateConflictMap = BuildConflictMap(flights, f => f.GateId);
            var runwayConflictMap = BuildConflictMap(flights, f => f.RunwayId);

            return flights.Select(f => ToResponse(
                    f,
                    gatesById.TryGetValue(f.GateId, out var gateName) ? gateName : string.Empty,
                    runwaysById.TryGetValue(f.RunwayId, out var runwayName) ? runwayName : string.Empty,
                    disruptedGateSet.Contains(f.GateId),
                    gateConflictMap.TryGetValue(f.FlightId, out var otherGateFlight) ? otherGateFlight : null,
                    disruptedRunwaySet.Contains(f.RunwayId),
                    runwayConflictMap.TryGetValue(f.FlightId, out var otherRunwayFlight) ? otherRunwayFlight : null))
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

            var gatesById = await _context.Gates
                .AsNoTracking()
                .ToDictionaryAsync(g => g.GateId, g => g.Name);

            var runwaysById = await _context.Runways
                .AsNoTracking()
                .ToDictionaryAsync(r => r.RunwayId, r => r.Name);

            var isGateUnavailable = await _context.Disruptions
                .AsNoTracking()
                .AnyAsync(d =>
                    d.ResourceType == GateResourceType &&
                    d.ResourceId == flight.GateId &&
                    d.Status == DisruptionStatusOpen &&
                    d.StartsAt <= now &&
                    d.EndsAt >= now);

            var isRunwayUnavailable = await _context.Disruptions
                .AsNoTracking()
                .AnyAsync(d =>
                    d.ResourceType == RunwayResourceType &&
                    d.ResourceId == flight.RunwayId &&
                    d.Status == DisruptionStatusOpen &&
                    d.StartsAt <= now &&
                    d.EndsAt >= now);

            static DateTimeOffset GetDeparture(Flight f) => f.ActualDeparture ?? f.ScheduledDeparture;
            static DateTimeOffset GetArrival(Flight f) => f.ActualArrival ?? f.ScheduledArrival;

            var thisStart = GetDeparture(flight);
            var thisEnd = GetArrival(flight);

            var sameGateFlights = await _context.Flights
                .AsNoTracking()
                .Where(f => f.GateId == flight.GateId && f.FlightId != id)
                .ToListAsync();

            var overlappingGate = sameGateFlights
                .Select(f => new { f.FlightNumber, Start = GetDeparture(f), End = GetArrival(f) })
                .FirstOrDefault(x => thisStart <= x.End && x.Start <= thisEnd);

            var sameRunwayFlights = await _context.Flights
                .AsNoTracking()
                .Where(f => f.RunwayId == flight.RunwayId && f.FlightId != id)
                .ToListAsync();

            var overlappingRunway = sameRunwayFlights
                .Select(f => new { f.FlightNumber, Start = GetDeparture(f), End = GetArrival(f) })
                .FirstOrDefault(x => thisStart <= x.End && x.Start <= thisEnd);

            return ToResponse(
                flight,
                gatesById.TryGetValue(flight.GateId, out var gateName) ? gateName : string.Empty,
                runwaysById.TryGetValue(flight.RunwayId, out var runwayName) ? runwayName : string.Empty,
                isGateUnavailable,
                overlappingGate?.FlightNumber,
                isRunwayUnavailable,
                overlappingRunway?.FlightNumber);
        }

        [HttpPost]
        public async Task<ActionResult<Flight>> Create(Flight flight)
        {
            await ValidateGateAndRunwayExist(flight);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            _context.Flights.Add(flight);
            await _context.SaveChangesAsync();

            await BroadcastFlightsUpdated();

            return CreatedAtAction(nameof(Get), new { id = flight.FlightId }, flight);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, Flight flight)
        {
            if (id != flight.FlightId) return BadRequest();

            await ValidateGateAndRunwayExist(flight);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

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

        private static FlightResponse ToResponse(
            Flight f,
            string gateName,
            string runwayName,
            bool isGateUnavailable,
            string? overlappingGateFlightNumber,
            bool isRunwayUnavailable,
            string? overlappingRunwayFlightNumber)
        {
            var gateStatus = GateStatus.Ok;
            string? gateDescription = null;

            if (isGateUnavailable)
            {
                gateStatus = GateStatus.Unavailable;
                gateDescription = "Gate is unavailable";
            }
            else if (!string.IsNullOrWhiteSpace(overlappingGateFlightNumber))
            {
                gateStatus = GateStatus.Conflict;
                gateDescription = $"Overlaps with {overlappingGateFlightNumber}";
            }

            var runwayStatus = RunwayStatus.Ok;
            string? runwayDescription = null;

            if (isRunwayUnavailable)
            {
                runwayStatus = RunwayStatus.Unavailable;
                runwayDescription = "Runway is unavailable";
            }
            else if (!string.IsNullOrWhiteSpace(overlappingRunwayFlightNumber))
            {
                runwayStatus = RunwayStatus.Conflict;
                runwayDescription = $"Overlaps with {overlappingRunwayFlightNumber}";
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
                new GateResponse(f.GateId, gateName, gateStatus, gateDescription),
                new RunwayResponse(f.RunwayId, runwayName, runwayStatus, runwayDescription),
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

        private async Task ValidateGateAndRunwayExist(Flight flight)
        {
            if (!await _context.Gates.AsNoTracking().AnyAsync(g => g.GateId == flight.GateId))
                ModelState.AddModelError(nameof(flight.GateId), $"Unknown gateId '{flight.GateId}'.");

            if (!await _context.Runways.AsNoTracking().AnyAsync(r => r.RunwayId == flight.RunwayId))
                ModelState.AddModelError(nameof(flight.RunwayId), $"Unknown runwayId '{flight.RunwayId}'.");
        }
    }
}
