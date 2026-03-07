using Microsoft.AspNetCore.Mvc;
using Omni.Web.Data;
using Omni.Web.Models;

namespace Omni.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RunwaysController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RunwaysController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Runway>>> GetAll()
        {
            var runways = await _context.Runways
                .AsNoTracking()
                .OrderBy(r => r.RunwayId)
                .ToListAsync();

            return runways;
        }
    }
}
