using Microsoft.AspNetCore.Mvc;
using Omni.Web.Data;
using Omni.Web.Models;

namespace Omni.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BaggageConveyorBeltsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BaggageConveyorBeltsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BaggageConveyorBelt>>> GetAll()
        {
            var belts = await _context.BaggageConveyorBelts
                .AsNoTracking()
                .OrderBy(b => b.BaggageConveyorBeltId)
                .ToListAsync();

            return belts;
        }
    }
}
