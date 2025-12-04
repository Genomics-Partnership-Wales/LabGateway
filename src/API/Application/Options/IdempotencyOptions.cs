using System.ComponentModel.DataAnnotations;

namespace LabResultsGateway.API.Application.Options;

public class IdempotencyOptions
{
    [Required]
    public string TableName { get; set; } = "IdempotencyRecords";

    [Range(0.001, 8760)] // 3.6 seconds to 1 year
    public double TTLHours { get; set; } = 24;

    [Required]
    public string StorageConnection { get; set; } = "AzureWebJobsStorage";
}
