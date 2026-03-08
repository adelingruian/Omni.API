using Microsoft.AspNetCore.Mvc;
using Omni.Web.Models;
using Omni.Web.Services;

namespace Omni.Web.Controllers
{
    [ApiController]
    [Route("ai-suggested-actions")]
    public sealed class AiSuggestedActionsController : ControllerBase
    {
        private readonly IOpenAiSuggestedActionService _service;

        public AiSuggestedActionsController(IOpenAiSuggestedActionService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<AiPlannedUpdateSlimResponse>>> Get()
        {
            var response = await _service.GetSuggestedActionsAsync(HttpContext.RequestAborted);
            return Ok(response);
        }
    }
}
