using Microsoft.AspNetCore.Mvc;
using Omni.Web.Data;
using Omni.Web.Models;

namespace Omni.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GatesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public GatesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Gate>>> GetAll()
        {
            var gates = await _context.Gates
                .AsNoTracking()
                .OrderBy(g => g.GateId)
                .ToListAsync();

            return gates;
        }
    }
}
