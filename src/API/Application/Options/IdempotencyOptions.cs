using System.ComponentModel.DataAnnotations;

namespace LabResultsGateway.API.Application.Options;

public class IdempotencyOptions
{
    [Required]
    public string TableName { get; set; } = "IdempotencyRecords";

    [Range(1, 8760)] // 1 hour to 1 year
    public int TTLHours { get; set; } = 24;

    [Required]
    public string StorageConnection { get; set; } = "AzureWebJobsStorage";
}
