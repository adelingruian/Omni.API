using Microsoft.AspNetCore.Mvc;
using Omni.Web.Data;
using Omni.Web.Models;
using Omni.Web.Services;

namespace Omni.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DisruptionsController : ControllerBase
    {
        private const string DisruptionStatusOpen = "Open";

        private readonly AppDbContext _context;
        private readonly IFlightsBroadcastService _broadcast;

        public DisruptionsController(AppDbContext context, IFlightsBroadcastService broadcast)
        {
            _context = context;
            _broadcast = broadcast;
        }

        public sealed record CreateDisruptionRequest(string ResourceType, int ResourceId);

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

            await _broadcast.BroadcastFlightsUpdatedAsync(HttpContext.RequestAborted);

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

            await _broadcast.BroadcastFlightsUpdatedAsync(HttpContext.RequestAborted);

            return NoContent();
        }
    }
}
