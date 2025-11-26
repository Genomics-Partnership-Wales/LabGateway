using Azure.Storage.Queues;
using LabResultsGateway.API.Application.Services;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API.Infrastructure.Messaging;

/// <summary>
/// Implementation of IMessageQueueService using Azure Queue Storage.
/// </summary>
public class AzureQueueService : IMessageQueueService
{
    private readonly QueueServiceClient _queueServiceClient;
    private readonly string _processingQueueName;
    private readonly string _poisonQueueName;
    private readonly ILogger<AzureQueueService> _logger;

    /// <summary>
    /// Initializes a new instance of the AzureQueueService class.
    /// </summary>
    /// <param name="queueServiceClient">Azure Queue Service client.</param>
    /// <param name="processingQueueName">Name of the processing queue.</param>
    /// <param name="poisonQueueName">Name of the poison queue.</param>
    /// <param name="logger">Logger for tracking operations.</param>
    public AzureQueueService(
        QueueServiceClient queueServiceClient,
        string processingQueueName,
        string poisonQueueName,
        ILogger<AzureQueueService> logger)
    {
        _queueServiceClient = queueServiceClient ?? throw new ArgumentNullException(nameof(queueServiceClient));
        _processingQueueName = processingQueueName ?? throw new ArgumentNullException(nameof(processingQueueName));
        _poisonQueueName = poisonQueueName ?? throw new ArgumentNullException(nameof(poisonQueueName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Sends a message to the processing queue.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    public async Task SendToProcessingQueueAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var queueClient = _queueServiceClient.GetQueueClient(_processingQueueName);
        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        // Encode message as Base64 for safe storage
        var encodedMessage = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(message));

        await queueClient.SendMessageAsync(encodedMessage, cancellationToken: cancellationToken);

        _logger.LogInformation("Message sent to processing queue '{QueueName}'. MessageLength: {Length} characters",
            _processingQueueName, message.Length);
    }

    /// <summary>
    /// Sends a message to the poison queue with retry count metadata.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="retryCount">The current retry count.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    public async Task SendToPoisonQueueAsync(string message, int retryCount, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentOutOfRangeException.ThrowIfNegative(retryCount);

        var queueClient = _queueServiceClient.GetQueueClient(_poisonQueueName);
        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        // Create message with retry metadata
        var messageWithMetadata = $"{{\"message\":\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(message))}\",\"retryCount\":{retryCount}}}";

        await queueClient.SendMessageAsync(messageWithMetadata, cancellationToken: cancellationToken);

        _logger.LogWarning("Message sent to poison queue '{QueueName}' with retry count {RetryCount}. MessageLength: {Length} characters",
            _poisonQueueName, retryCount, message.Length);
    }
}
