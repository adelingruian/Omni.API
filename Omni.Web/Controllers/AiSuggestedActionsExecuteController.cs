using Microsoft.AspNetCore.Mvc;
using Omni.Web.Data;
using Omni.Web.Models;
using Omni.Web.Services;
using System.Text.Json;

namespace Omni.Web.Controllers
{
    [ApiController]
    [Route("ai-suggested-actions")]
    public sealed class AiSuggestedActionsExecuteController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IResourceUsageService _usage;
        private readonly IFlightsBroadcastService _broadcast;

        public AiSuggestedActionsExecuteController(AppDbContext context, IResourceUsageService usage, IFlightsBroadcastService broadcast)
        {
            _context = context;
            _usage = usage;
            _broadcast = broadcast;
        }

        [HttpPost("execute")]
        public async Task<IActionResult> Execute([FromBody] AiExecuteToolRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ToolName))
                return BadRequest("ToolName is required.");

            if (request.Parameters.ValueKind is not (JsonValueKind.Object))
                return BadRequest("Parameters must be a JSON object.");

            switch (request.ToolName)
            {
                case "reassign_gate":
                    {
                        if (!request.Parameters.TryGetProperty("flightId", out var flightIdEl) ||
                            !request.Parameters.TryGetProperty("newGateId", out var newGateIdEl) ||
                            flightIdEl.ValueKind != JsonValueKind.Number ||
                            newGateIdEl.ValueKind != JsonValueKind.Number)
                            return BadRequest("Invalid parameters for reassign_gate.");

                        var flightId = flightIdEl.GetInt32();
                        var newGateId = newGateIdEl.GetInt32();

                        var flight = await _context.Flights.FindAsync(flightId);
                        if (flight is null) return NotFound($"Flight {flightId} not found.");

                        var gateExists = await _context.Gates.AsNoTracking().AnyAsync(g => g.GateId == newGateId);
                        if (!gateExists) return BadRequest($"Unknown gateId '{newGateId}'.");

                        flight.GateId = newGateId;
                        await _context.SaveChangesAsync(HttpContext.RequestAborted);
                        await _usage.UpsertForFlightAsync(flight, HttpContext.RequestAborted);
                        break;
                    }

                case "reassign_runway":
                    {
                        if (!request.Parameters.TryGetProperty("flightId", out var flightIdEl) ||
                            !request.Parameters.TryGetProperty("newRunwayId", out var newRunwayIdEl) ||
                            flightIdEl.ValueKind != JsonValueKind.Number ||
                            newRunwayIdEl.ValueKind != JsonValueKind.Number)
                            return BadRequest("Invalid parameters for reassign_runway.");

                        var flightId = flightIdEl.GetInt32();
                        var newRunwayId = newRunwayIdEl.GetInt32();

                        var flight = await _context.Flights.FindAsync(flightId);
                        if (flight is null) return NotFound($"Flight {flightId} not found.");

                        var runwayExists = await _context.Runways.AsNoTracking().AnyAsync(r => r.RunwayId == newRunwayId);
                        if (!runwayExists) return BadRequest($"Unknown runwayId '{newRunwayId}'.");

                        flight.RunwayId = newRunwayId;
                        await _context.SaveChangesAsync(HttpContext.RequestAborted);
                        await _usage.UpsertForFlightAsync(flight, HttpContext.RequestAborted);
                        break;
                    }

                case "reassign_belt":
                    {
                        if (!request.Parameters.TryGetProperty("flightId", out var flightIdEl) ||
                            !request.Parameters.TryGetProperty("newBaggageConveyorBeltId", out var newBeltIdEl) ||
                            flightIdEl.ValueKind != JsonValueKind.Number ||
                            newBeltIdEl.ValueKind != JsonValueKind.Number)
                            return BadRequest("Invalid parameters for reassign_belt.");

                        var flightId = flightIdEl.GetInt32();
                        var newBeltId = newBeltIdEl.GetInt32();

                        var flight = await _context.Flights.FindAsync(flightId);
                        if (flight is null) return NotFound($"Flight {flightId} not found.");

                        var beltExists = await _context.BaggageConveyorBelts.AsNoTracking().AnyAsync(b => b.BaggageConveyorBeltId == newBeltId);
                        if (!beltExists) return BadRequest($"Unknown baggageConveyorBeltId '{newBeltId}'.");

                        flight.BaggageConveyorBeltId = newBeltId;
                        await _context.SaveChangesAsync(HttpContext.RequestAborted);
                        await _usage.UpsertForFlightAsync(flight, HttpContext.RequestAborted);
                        break;
                    }

                case "delay_pushback":
                    {
                        if (!request.Parameters.TryGetProperty("flightId", out var flightIdEl) ||
                            !request.Parameters.TryGetProperty("delayMinutes", out var delayMinutesEl) ||
                            flightIdEl.ValueKind != JsonValueKind.Number ||
                            delayMinutesEl.ValueKind != JsonValueKind.Number)
                            return BadRequest("Invalid parameters for delay_pushback.");

                        var flightId = flightIdEl.GetInt32();
                        var delayMinutes = delayMinutesEl.GetInt32();

                        var flight = await _context.Flights.FindAsync(flightId);
                        if (flight is null) return NotFound($"Flight {flightId} not found.");

                        if (delayMinutes < 0 || delayMinutes > 360)
                            return BadRequest("delayMinutes must be between 0 and 360.");

                        // Move actual times forward when present; otherwise do nothing (prompt tells AI to use only when actual times exist).
                        if (flight.ActualDeparture.HasValue)
                            flight.ActualDeparture = flight.ActualDeparture.Value.AddMinutes(delayMinutes);
                        if (flight.ActualArrival.HasValue)
                            flight.ActualArrival = flight.ActualArrival.Value.AddMinutes(delayMinutes);

                        await _context.SaveChangesAsync(HttpContext.RequestAborted);
                        await _usage.UpsertForFlightAsync(flight, HttpContext.RequestAborted);
                        break;
                    }

                default:
                    return BadRequest($"Unknown toolName '{request.ToolName}'.");
            }

            await _broadcast.BroadcastFlightsUpdatedAsync(HttpContext.RequestAborted);
            return Ok();
        }
    }
}
