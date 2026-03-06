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

            var disruptedGates = await _context.Disruptions
                .AsNoTracking()
                .Where(d =>
                    d.ResourceType == GateResourceType &&
                    d.Status == DisruptionStatusOpen)
                .Select(d => d.ResourceId)
                .Distinct()
                .ToListAsync();

            var disruptedSet = disruptedGates.ToHashSet(StringComparer.OrdinalIgnoreCase);

            return flights.Select(f => ToResponse(f, disruptedSet.Contains(f.Gate))).ToList();
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<FlightResponse>> Get(int id)
        {
            var now = DateTimeOffset.UtcNow;

            var flight = await _context.Flights
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FlightId == id);

            if (flight == null) return NotFound();

            var isGateDisrupted = await _context.Disruptions
                .AsNoTracking()
                .AnyAsync(d =>
                    d.ResourceType == GateResourceType &&
                    d.ResourceId == flight.Gate &&
                    d.Status == DisruptionStatusOpen &&
                    d.StartsAt <= now &&
                    d.EndsAt >= now);

            return ToResponse(flight, isGateDisrupted);
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

        private static FlightResponse ToResponse(Flight f, bool isGateDisrupted)
        {
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
                new GateResponse(f.Gate, isGateDisrupted ? "Disrupted" : "Ok"),
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
            var now = DateTimeOffset.UtcNow;

            var flights = await _context.Flights
                .AsNoTracking()
                .OrderBy(f => f.FlightId)
                .ToListAsync();

            var gateIds = flights
                .Select(f => f.Gate)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var disruptedGates = await _context.Disruptions
                .AsNoTracking()
                .Where(d =>
                    d.ResourceType == GateResourceType &&
                    gateIds.Contains(d.ResourceId) &&
                    d.Status == DisruptionStatusOpen &&
                    d.StartsAt <= now &&
                    d.EndsAt >= now)
                .Select(d => d.ResourceId)
                .Distinct()
                .ToListAsync();

            var disruptedSet = disruptedGates.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var payload = flights.Select(f => ToResponse(f, disruptedSet.Contains(f.Gate))).ToList();

            await _hubContext.Clients.All.SendAsync(FlightsHub.FlightsUpdatedEvent, payload);
        }
    }
}
