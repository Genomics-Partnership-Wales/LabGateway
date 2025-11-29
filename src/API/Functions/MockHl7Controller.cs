using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace LabResultsGateway.API.Functions
{
    /// <summary>
    /// Local mock endpoint to receive HL7 messages during development when UseLocalHl7Fallback is enabled.
    /// </summary>
    public static class MockHl7Controller
    {
        [Function("MockHl7Controller")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "SubmitHL7Message")] HttpRequestData req,
            FunctionContext context)
        {
            var logger = context.GetLogger("MockHl7Controller");

            string body = await new StreamReader(req.Body).ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                logger.LogWarning("MockHl7Controller received empty payload");
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Empty HL7 payload");
                return bad;
            }

            // Log a short preview and length for developer diagnostics
            var preview = body.Length > 200 ? body[..200] + "..." : body;
            logger.LogInformation("MockHl7Controller received HL7 message. Length: {Length}. Preview: {Preview}", body.Length, preview);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("OK");
            return response;
        }
    }
}
