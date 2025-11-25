using System;
using LabResultsGateway.API.Domain.ValueObjects;

namespace LabResultsGateway.API.Domain.Entities;

/// <summary>
/// Represents a lab report aggregate root entity.
/// This entity encapsulates all data related to a lab report processing workflow.
/// </summary>
public class LabReport
{
    /// <summary>
    /// The unique lab number identifier.
    /// </summary>
    public LabNumber LabNumber { get; private set; }

    /// <summary>
    /// The PDF content as a byte array.
    /// </summary>
    public byte[]? PdfContent { get; private set; }

    /// <summary>
    /// The lab metadata retrieved from the external API.
    /// </summary>
    public LabMetadata? Metadata { get; private set; }

    /// <summary>
    /// The timestamp when this lab report was created/processed.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// The correlation ID for distributed tracing.
    /// </summary>
    public string? CorrelationId { get; private set; }

    /// <summary>
    /// Initializes a new instance of the LabReport entity.
    /// </summary>
    /// <param name="labNumber">The lab number value object.</param>
    /// <param name="pdfContent">The PDF content as byte array.</param>
    /// <param name="metadata">The lab metadata value object.</param>
    /// <param name="correlationId">The correlation ID for tracing.</param>
    /// <exception cref="ArgumentNullException">Thrown when pdfContent or metadata is null.</exception>
    /// <exception cref="ArgumentException">Thrown when pdfContent is empty or correlationId is null/empty.</exception>
    public LabReport(LabNumber labNumber, byte[] pdfContent, LabMetadata metadata, string correlationId)
    {
        LabNumber = labNumber;
        PdfContent = pdfContent ?? throw new ArgumentNullException(nameof(pdfContent));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

        if (pdfContent.Length == 0)
        {
            throw new ArgumentException("PDF content cannot be empty.", nameof(pdfContent));
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Correlation ID cannot be null or empty.", nameof(correlationId));
        }

        CorrelationId = correlationId.Trim();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Private constructor for EF Core or other ORM frameworks.
    /// </summary>
    private LabReport() { }

    /// <summary>
    /// Returns a string representation of the lab report.
    /// </summary>
    /// <returns>A string containing the lab number and correlation ID.</returns>
    public override string ToString() => $"LabReport[LabNumber={LabNumber}, CorrelationId={CorrelationId}]";
}