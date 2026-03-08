using Omni.Web.Data;
using Omni.Web.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Omni.Web.Services
{
    public sealed class OpenAiSuggestedActionService : IOpenAiSuggestedActionService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private static IReadOnlyList<AiToolSpec> GetToolSpecs() =>
        [
            new AiToolSpec(
                Name: "reassign_gate",
                Description: "Reassign a flight to a different gate, this can be done for both resource ussage and disruptions. This updates Flight.GateId and regenerates resource usages for that flight. This can NEVER be used to reasign to same gate. This can NEVER be used to reasign if status is OK.",
                ParametersJsonSchema: new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "flightId", "newGateId" },
                    properties = new
                    {
                        flightId = new { type = "integer", minimum = 1 },
                        newGateId = new { type = "integer", minimum = 1 },
                    }
           }),

            new AiToolSpec(
                Name: "reassign_runway",
                Description: "Reassign a flight to a different runway, this can be done for both resource ussage and disruptions. This updates Flight.RunwayId and regenerates resource usages for that flight. This can NEVER be used to reasign to same runway. This can NEVER be used to reasign if status is OK.",
                ParametersJsonSchema: new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "flightId", "newRunwayId" },
                    properties = new
                    {
                        flightId = new { type = "integer", minimum = 1 },
                        newRunwayId = new { type = "integer", minimum = 1 },
                    }
           }),

            new AiToolSpec(
                Name: "reassign_belt",
                Description: "Reassign a flight to a different baggage belt, this can be done for both resource ussage and disruptions. This updates Flight.BeltId and regenerates resource usages for that flight. This can NEVER be used to reasign to same belt. This can NEVER be used to reasign if status is OK.",
                ParametersJsonSchema: new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "flightId", "newBaggageConveyorBeltId" },
                    properties = new
                    {
                        flightId = new { type = "integer", minimum = 1 },
                        newBaggageConveyorBeltId = new { type = "integer", minimum = 1 },
                    }
           }),

            new AiToolSpec(
                Name: "delay_pushback",
                Description: "Delay a flight by moving its ActualDeparture/ActualArrival forward by a number of minutes. Use only when actual times exist or when explicitly instructed to adjust scheduled times.",
                ParametersJsonSchema: new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "flightId", "delayMinutes" },
                    properties = new
                    {
                        flightId = new { type = "integer", minimum = 1 },
                        delayMinutes = new { type = "integer", minimum = 0, maximum = 360 },
                    }
              }),

            new AiToolSpec(
                Name: "escalate_ops_review",
                Description: "No database change. Use when there is no safe automated change; flags the flight for human review. Try to use this only as last resort.",
                ParametersJsonSchema: new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "flightId", "note" },
                    properties = new
                    {
                        flightId = new { type = "integer", minimum = 1 },
                        note = new { type = "string", minLength = 1, maxLength = 500 },
                    }
                })
        ];

        private readonly HttpClient _http;
        private readonly AppDbContext _context;
        private readonly IFlightsBroadcastService _broadcast;
        private readonly IConfiguration _config;

        public OpenAiSuggestedActionService(HttpClient http, AppDbContext context, IFlightsBroadcastService broadcast, IConfiguration config)
        {
            _http = http;
            _context = context;
            _broadcast = broadcast;
            _config = config;
        }

        public async Task<IReadOnlyList<AiPlannedUpdateSlimResponse>> GetSuggestedActionsAsync(CancellationToken cancellationToken = default)
        {
            var apiKey = _config["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Missing OpenAI:ApiKey configuration.");

            var model = _config["OpenAI:Model"] ?? "gpt-4o-mini";

            var flights = await _broadcast.BuildFlightsPayloadAsync(cancellationToken);

            // Provide a concise flight view.
            var flightFacts = flights.Select(f => new
            {
                f.FlightId,
                f.FlightNumber,
                f.Origin,
                f.Destination,
                f.ScheduledDeparture,
                f.ActualDeparture,
                f.ScheduledArrival,
                f.ActualArrival,
                Gate = new { f.Gate.GateId, f.Gate.Name, Status = f.Gate.Status.ToString(), f.Gate.Description },
                Runway = new { f.Runway.RunwayId, f.Runway.Name, Status = f.Runway.Status.ToString(), f.Runway.Description },
                Belt = new { f.Belt.BaggageConveyorBeltId, f.Belt.Name, Status = f.Belt.Status.ToString(), f.Belt.Description },
                f.PassengerNumber,
                f.PossibleDelayMinutes,
                f.BaggageTotalChecked,
                Score = new { f.DisruptionScore.TotalPoints, f.DisruptionScore.Severity }
            }).ToList();

            // Provide resource catalogs so the model can pick valid alternative IDs.
            var gates = await _context.Gates
                .AsNoTracking()
                .OrderBy(g => g.GateId)
                .Select(g => new { g.GateId, g.Name })
                .ToListAsync(cancellationToken);

            var runways = await _context.Runways
                .AsNoTracking()
                .OrderBy(r => r.RunwayId)
                .Select(r => new { r.RunwayId, r.Name })
                .ToListAsync(cancellationToken);

            var belts = await _context.BaggageConveyorBelts
                .AsNoTracking()
                .OrderBy(b => b.BaggageConveyorBeltId)
                .Select(b => new { b.BaggageConveyorBeltId, b.Name })
                .ToListAsync(cancellationToken);

            var disruptions = await _context.Disruptions
                .AsNoTracking()
                .OrderBy(d => d.StartsAt)
                .Select(d => new
                {
                    d.DisruptionId,
                    d.ResourceType,
                    d.ResourceId,
                    d.StartsAt,
                    d.EndsAt
                })
                .ToListAsync(cancellationToken);

            var usages = await _context.ResourceUsages
                .AsNoTracking()
                .OrderBy(u => u.StartsAt)
                .Select(u => new
                {
                    u.ResourceUsageId,
                    u.ResourceType,
                    u.ResourceId,
                    u.FlightId,
                    u.StartsAt,
                    u.EndsAt
                })
                .ToListAsync(cancellationToken);

            var tools = GetToolSpecs();

            var system =
                "You are an airport operations assistant. " +
                "Your goal is to reduce conflicts and disruptions by proposing only SAFE, minimal changes. " +
                "Output ONLY valid JSON (no markdown, no extra text). " +
                "Never invent IDs; only use IDs present in the input lists.\n" +
                "\nHARD RULES (MUST FOLLOW):\n" +
                "1) Omit flights with Score.TotalPoints = 0 (no disruption).\n" +
                "2) Do not propose NO-OP moves: never reassign to the same resource ID the flight already has.\n" +
                "3) Status enforcement (STRICT):\n" +
                "   - If Gate.Status == 'Ok' then reassign_gate MUST NOT be used for that flight.\n" +
                "   - If Runway.Status == 'Ok' then reassign_runway MUST NOT be used for that flight.\n" +
                "   - If Belt.Status == 'Ok' then reassign_belt MUST NOT be used for that flight.\n" +
                "   - If Gate.Status == 'Ok' AND Runway.Status == 'Ok' AND Belt.Status == 'Ok', then NO ACTION is required for that flight and it MUST be omitted from the output.\n" +
                "   The ONLY exception to the above is if a change is required to prevent a NEW conflict caused by your overall plan.\n" +
                "4) Global plan consistency: all tool calls must remain valid when applied together and must not introduce new overlaps/conflicts as a set.\n" +
                "5) Priority: runway conflicts/unavailability have highest impact; if a runway fix is needed, prefer reassign_runway first, then gate, then belt.\n" +
                "   delay_pushback (moving the flight in time) MUST be treated as LAST RESORT.\n" +
                "   You MUST NOT use delay_pushback if ANY safe resource reassignment (runway/gate/belt) can resolve the issue.\n" +
                "   If all required resources for a flight are available (Gate/Runway/Belt are 'Ok' AND your overall plan does not create a new conflict), then that flight MUST NEVER be delayed additionally.\n" +
                "6) If no flights qualify for action under these rules, return an empty JSON array []. Returning [] is OK.\n" +
                "7) Full-resource-disruption handling (IMPORTANT): if a flight needs a resource but ALL gates are disrupted/unavailable during that flight's relevant usage window (meaning there is no safe alternative resourceId in the Resource catalog), then you MUST propose delay_pushback to postpone the flight until AFTER the disruption ends. Use delayMinutes to delay just enough to clear the disruption window (subject to delay_pushback limits).\n" +
                "8) Luggage-jam reduction (IMPORTANT): if total bags on a belt within the +/-1 hour window would exceed 200, prefer reassign_belt for the flights contributing most bags, moving them to a belt where the +/-1 hour total stays <= 200. Only use reassign_belt when Belt.Status is Conflict/Unavailable OR when needed to reduce this luggage-jam score (i.e., avoid totalBagsOnBelt > 200).";

            var user =
                "Return a JSON ARRAY with AT MOST one item per input flight (0..N items total). " +
                "If there is no improvement to make for a flight, omit it from the array. " +
                "Each item MUST follow this schema: " +
                "{ \"flightId\": number, \"description\": string, \"tool\": { \"name\": string, \"parameters\": object } | null }. " +
                "Rules:\n" +
                "- description must be a short sentence describing the change and MUST use human-readable resource NAMES (not IDs).\n" +
                "- The tool call MUST use resource IDs in parameters (not names).\n" +
                "- tool.name must match one of the provided tools exactly.\n" +
                "- tool.parameters MUST be an object that matches that tool's JSON schema.\n" +
                "\nMapping rule:\n" +
                "- When you choose a new gate/runway/belt, pick it from the catalogs and use the catalog Name in description, but the catalog Id in tool.parameters.\n" +
                "\nHow timing/conflicts are computed (for reasoning):\n" +
                "- A ResourceUsage has a half-open interval [StartsAt, EndsAt). Two usages overlap if aStart < bEnd AND bStart < aEnd.\n" +
                "- Gate/Runway usages are generated around actual/scheduled arrival/departure times with before/after buffers (see usages list).\n" +
                "- PossibleDelayMinutes is computed by delaying the later-starting overlapping usage by the overlap duration (ceiling to minutes).\n" +
                "\nHow Disruption Score is computed (IMPORTANT):\n" +
                "- Score.TotalPoints is the sum of the following components (rounded to an integer):\n" +
                "  1) Delay points (if delayMins > 0): (delayMins/10)^2 * (passengers/10).\n" +
                "  2) Legal fines (if delayMins >= 180): +25 * passengers.\n" +
                "  3) Gate occupied/conflict: +500 points when the gate is in conflict/unavailable.\n" +
                "  4) Runway conflict/unavailable: +4000 points when the runway is in conflict/unavailable (highest impact).\n" +
                "  5) Belt luggage jam (if total bags on belt > 200): +((extraBags/10)^2 * 5), where extraBags = totalBagsOnBelt - 200.\n" +
                "- Severity tiers: 0='On Time', <100='Low Risk', <500='Medium Risk', <2000='High Risk', >=2000='CRITICAL'.\n" +
                "\nInput data:\n" +
                "Tools: " + JsonSerializer.Serialize(tools, JsonOptions) +
                "\nGates: " + JsonSerializer.Serialize(gates, JsonOptions) +
                "\nRunways: " + JsonSerializer.Serialize(runways, JsonOptions) +
                "\nBelts: " + JsonSerializer.Serialize(belts, JsonOptions) +
                "\nFlights: " + JsonSerializer.Serialize(flightFacts, JsonOptions) +
                "\nDisruptions: " + JsonSerializer.Serialize(disruptions, JsonOptions) +
                "\nResourceUsages: " + JsonSerializer.Serialize(usages, JsonOptions);

            var payload = new
            {
                model,
                temperature = 0,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user }
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, cancellationToken);
            var raw = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"OpenAI request failed: {(int)resp.StatusCode} ({resp.StatusCode}). {raw}");

            using var doc = JsonDocument.Parse(raw);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
                return [];

            using var outDoc = JsonDocument.Parse(content);

            JsonElement arrayEl;
            if (outDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                arrayEl = outDoc.RootElement;
            }
            else if (outDoc.RootElement.ValueKind == JsonValueKind.Object)
            {
                // Accept any property that contains the result array (models sometimes wrap the payload).
                JsonElement? found = null;
                foreach (var prop in outDoc.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        found = prop.Value;
                        break;
                    }
                }

                if (found is null)
                    throw new InvalidOperationException("Unexpected OpenAI JSON shape (no array found on root object).");

                arrayEl = found.Value;
            }
            else
            {
                throw new InvalidOperationException("Unexpected OpenAI JSON shape (expected array or object containing an array).");
            }

            return ParsePlannedSuggestionsArray(arrayEl);
        }

        private static IReadOnlyList<AiPlannedUpdateSlimResponse> ParsePlannedSuggestionsArray(JsonElement suggestionsEl)
        {
            var results = new List<AiPlannedUpdateSlimResponse>();

            foreach (var el in suggestionsEl.EnumerateArray())
            {
                var flightId = el.GetProperty("flightId").GetInt32();
                var description = el.TryGetProperty("description", out var dEl) ? (dEl.GetString() ?? string.Empty) : string.Empty;

                AiToolCallSlimResponse? tool = null;
                if (el.TryGetProperty("tool", out var toolEl) && toolEl.ValueKind == JsonValueKind.Object)
                {
                    var name = toolEl.TryGetProperty("name", out var nEl) ? (nEl.GetString() ?? string.Empty) : string.Empty;
                    var parametersEl = toolEl.TryGetProperty("parameters", out var pEl) ? pEl : default;

                    var parameters = parametersEl.ValueKind == JsonValueKind.Undefined
                        ? new { }
                        : (JsonSerializer.Deserialize<object>(parametersEl.GetRawText(), JsonOptions) ?? new { });

                    tool = new AiToolCallSlimResponse(name, parameters);
                }

                results.Add(new AiPlannedUpdateSlimResponse(flightId, description, tool));
            }

            return results
                .OrderBy(r => r.FlightId)
                .ToList();
        }
    }
}
