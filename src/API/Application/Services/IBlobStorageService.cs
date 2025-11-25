namespace LabResultsGateway.API.Application.Services;

/// <summary>
/// Service interface for Azure Blob Storage operations.
/// Handles moving failed blobs to the Failed folder.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Moves a blob from its current location to the Failed/ subfolder within the same container.
    /// This preserves the original blob for investigation while removing it from the processing pipeline.
    /// </summary>
    /// <param name="blobName">The name of the blob to move (without container prefix).</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when blobName is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when blob operations fail.</exception>
    Task MoveToFailedFolderAsync(string blobName, CancellationToken cancellationToken = default);
}
