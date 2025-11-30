namespace LabResultsGateway.API.Domain.Constants;

/// <summary>
/// Constants for blob storage operations in the Lab Results Gateway.
/// </summary>
public static class BlobConstants
{
    /// <summary>
    /// The prefix used for the failed folder where problematic blobs are moved.
    /// </summary>
    public const string FailedFolderPrefix = "Failed/";

    /// <summary>
    /// The name of the blob container for lab results.
    /// </summary>
    public const string LabResultsContainer = "lab-results-gateway";
}
