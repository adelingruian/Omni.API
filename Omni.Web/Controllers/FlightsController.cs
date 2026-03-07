using Microsoft.AspNetCore.Mvc;
using Omni.Web.Data;
using Omni.Web.Models;
using Omni.Web.Services;

namespace Omni.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FlightsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IFlightsBroadcastService _broadcast;
        private readonly IResourceUsageService _usage;

        public FlightsController(
            AppDbContext context,
            IFlightsBroadcastService broadcast,
            IResourceUsageService usage,
            ILogger<FlightsController> logger)
        {
            _context = context;
            _broadcast = broadcast;
            _usage = usage;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<FlightResponse>>> GetAll()
        {
            var payload = await _broadcast.BuildFlightsPayloadAsync(HttpContext.RequestAborted);
            return payload.ToList();
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<FlightResponse>> Get(int id)
        {
            var payload = await _broadcast.BuildFlightsPayloadAsync(HttpContext.RequestAborted);
            var flight = payload.FirstOrDefault(f => f.FlightId == id);
            if (flight is null) return NotFound();
            return flight;
        }

        [HttpPost]
        public async Task<ActionResult<Flight>> Create(Flight flight)
        {
            await ValidateGateRunwayAndBeltExist(flight);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            _context.Flights.Add(flight);
            await _context.SaveChangesAsync();

            await _usage.UpsertForFlightAsync(flight, HttpContext.RequestAborted);
            await _broadcast.BroadcastFlightsUpdatedAsync(HttpContext.RequestAborted);

            return CreatedAtAction(nameof(Get), new { id = flight.FlightId }, flight);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, Flight flight)
        {
            if (id != flight.FlightId) return BadRequest();

            await ValidateGateRunwayAndBeltExist(flight);
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

            await _usage.UpsertForFlightAsync(flight, HttpContext.RequestAborted);
            await _broadcast.BroadcastFlightsUpdatedAsync(HttpContext.RequestAborted);

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var flight = await _context.Flights.FindAsync(id);
            if (flight == null) return NotFound();

            _context.Flights.Remove(flight);
            await _context.SaveChangesAsync();

            await _usage.DeleteForFlightAsync(id, HttpContext.RequestAborted);
            await _broadcast.BroadcastFlightsUpdatedAsync(HttpContext.RequestAborted);

            return NoContent();
        }

        private async Task ValidateGateRunwayAndBeltExist(Flight flight)
        {
            if (!await _context.Gates.AsNoTracking().AnyAsync(g => g.GateId == flight.GateId))
                ModelState.AddModelError(nameof(flight.GateId), $"Unknown gateId '{flight.GateId}'.");

            if (!await _context.Runways.AsNoTracking().AnyAsync(r => r.RunwayId == flight.RunwayId))
                ModelState.AddModelError(nameof(flight.RunwayId), $"Unknown runwayId '{flight.RunwayId}'.");

            if (!await _context.BaggageConveyorBelts.AsNoTracking().AnyAsync(b => b.BaggageConveyorBeltId == flight.BaggageConveyorBeltId))
                ModelState.AddModelError(nameof(flight.BaggageConveyorBeltId), $"Unknown baggageConveyorBeltId '{flight.BaggageConveyorBeltId}'.");
        }
    }
}
