using Azure.Storage.Blobs;
using LabResultsGateway.API.Application.Services;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API.Infrastructure.Storage;

/// <summary>
/// Implementation of IBlobStorageService using Azure Blob Storage.
/// </summary>
public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly ILogger<BlobStorageService> _logger;

    /// <summary>
    /// Initializes a new instance of the BlobStorageService class.
    /// </summary>
    /// <param name="blobServiceClient">Azure Blob Service client.</param>
    /// <param name="containerName">Name of the blob container.</param>
    /// <param name="logger">Logger for tracking operations.</param>
    public BlobStorageService(
        BlobServiceClient blobServiceClient,
        string containerName,
        ILogger<BlobStorageService> logger)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _containerName = containerName ?? throw new ArgumentNullException(nameof(containerName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Moves a blob from its current location to the Failed folder.
    /// </summary>
    /// <param name="blobName">The name of the blob to move.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    public async Task MoveToFailedFolderAsync(string blobName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        var blobContainerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var sourceBlobClient = blobContainerClient.GetBlobClient(blobName);
        var destinationBlobName = $"Failed/{Path.GetFileName(blobName)}";
        var destinationBlobClient = blobContainerClient.GetBlobClient(destinationBlobName);

        _logger.LogInformation("Moving blob from '{SourceBlob}' to '{DestinationBlob}'",
            blobName, destinationBlobName);

        try
        {
            // Copy the blob to the Failed folder
            var copyOperation = await destinationBlobClient.StartCopyFromUriAsync(
                sourceBlobClient.Uri,
                cancellationToken: cancellationToken);

            // Wait for the copy operation to complete
            await copyOperation.WaitForCompletionAsync(cancellationToken);

            // Delete the original blob
            await sourceBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully moved blob '{BlobName}' to Failed folder", blobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move blob '{BlobName}' to Failed folder", blobName);
            throw;
        }
    }
}
