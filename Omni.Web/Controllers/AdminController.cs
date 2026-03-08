using Microsoft.AspNetCore.Mvc;
using Omni.Web.Data;
using Omni.Web.Services;

namespace Omni.Web.Controllers
{
    [ApiController]
    [Route("admin")]
    public sealed class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IFlightsBroadcastService _broadcast;

        public AdminController(AppDbContext context, IFlightsBroadcastService broadcast)
        {
            _context = context;
            _broadcast = broadcast;
        }

        /// <summary>
        /// Dangerous maintenance endpoint: deletes all rows from Flights, ResourceUsages and Disruptions.
        /// Intended for local development/demo resets.
        /// </summary>
        [HttpDelete("reset")]
        public async Task<IActionResult> Reset(CancellationToken cancellationToken)
        {
            await _context.ResourceUsages.ExecuteDeleteAsync(cancellationToken);
            await _context.Disruptions.ExecuteDeleteAsync(cancellationToken);
            await _context.Flights.ExecuteDeleteAsync(cancellationToken);

            await _broadcast.BroadcastFlightsUpdatedAsync(cancellationToken);
            return NoContent();
        }
    }
}
