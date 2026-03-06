using Microsoft.AspNetCore.Mvc;
using Omni.Web.Data;
using Omni.Web.Models;

namespace Omni.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FlightsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FlightsController(AppDbContext context, ILogger<FlightsController> logger)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Flight>>> GetAll()
        {
            return await _context.Flights.ToListAsync();
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Flight>> Get(int id)
        {
            var flight = await _context.Flights.FindAsync(id);
            if (flight == null) return NotFound();
            return flight;
        }

        [HttpPost]
        public async Task<ActionResult<Flight>> Create(Flight flight)
        {
            _context.Flights.Add(flight);
            await _context.SaveChangesAsync();
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

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var flight = await _context.Flights.FindAsync(id);
            if (flight == null) return NotFound();

            _context.Flights.Remove(flight);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
