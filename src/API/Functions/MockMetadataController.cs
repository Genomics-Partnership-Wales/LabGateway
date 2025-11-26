using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using LabResultsGateway.API.Application.DTOs;

namespace LabResultsGateway.API.Functions
{
    public class MockMetadataController
    {
        private readonly ILogger _logger;

        public MockMetadataController(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<MockMetadataController>();
        }

        [Function("GetLabMetadata")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "metadata")] HttpRequestData req)
        {
            _logger.LogInformation("Mock Metadata API received a request.");

            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var labNumber = query["labNumber"];

            if (string.IsNullOrEmpty(labNumber))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Please pass a labNumber on the query string");
                return badRequest;
            }

            // Simulate Not Found for specific lab number
            if (labNumber == "LAB999")
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);

            // Generate dummy data based on labNumber
            var metadata = new LabMetadataDto
            {
                PatientId = $"PAT-{labNumber.GetHashCode() % 10000:D4}",
                FirstName = "John",
                LastName = "Doe",
                DateOfBirth = new DateTime(1980, 1, 1),
                Gender = "Male",
                TestType = "Blood Count",
                CollectionDate = DateTime.UtcNow.AddDays(-1)
            };

            await response.WriteAsJsonAsync(metadata);

            return response;
        }
    }
}
