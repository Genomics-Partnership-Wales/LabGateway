namespace LabResultsGateway.API.Domain.Exceptions;

/// <summary>
/// Base exception for all lab processing domain exceptions.
/// Provides common properties for error handling and retry logic.
/// </summary>
public abstract class LabProcessingException : Exception
{
    /// <summary>
    /// Gets the name of the blob that caused the exception.
    /// </summary>
    public string? BlobName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation that caused this exception can be retried.
    /// Default is <c>false</c>. Override in derived classes to indicate retryable exceptions.
    /// </summary>
    public virtual bool IsRetryable => false;

    /// <summary>
    /// Initializes a new instance of the <see cref="LabProcessingException"/> class.
    /// </summary>
    protected LabProcessingException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LabProcessingException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    protected LabProcessingException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LabProcessingException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    protected LabProcessingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
