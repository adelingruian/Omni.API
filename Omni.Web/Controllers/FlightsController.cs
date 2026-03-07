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

        public FlightsController(AppDbContext context, IFlightsBroadcastService broadcast, ILogger<FlightsController> logger)
        {
            _context = context;
            _broadcast = broadcast;
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
            // Keep it simple: build the same payload and pick the requested flight.
            var payload = await _broadcast.BuildFlightsPayloadAsync(HttpContext.RequestAborted);
            var flight = payload.FirstOrDefault(f => f.FlightId == id);
            if (flight is null) return NotFound();
            return flight;
        }

        [HttpPost]
        public async Task<ActionResult<Flight>> Create(Flight flight)
        {
            await ValidateGateAndRunwayExist(flight);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            _context.Flights.Add(flight);
            await _context.SaveChangesAsync();

            await _broadcast.BroadcastFlightsUpdatedAsync(HttpContext.RequestAborted);

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

            await _broadcast.BroadcastFlightsUpdatedAsync(HttpContext.RequestAborted);

            return NoContent();
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
