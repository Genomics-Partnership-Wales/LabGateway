using System.Threading.Tasks;
using LabResultsGateway.API.Domain.Events;

namespace LabResultsGateway.API.Application.Events;

/// <summary>
/// Generic interface for domain event handlers.
/// Implementations should handle specific domain event types.
/// </summary>
/// <typeparam name="TEvent">The type of domain event this handler processes.</typeparam>
public interface IDomainEventHandler<TEvent> where TEvent : IDomainEvent
{
    /// <summary>
    /// Handles the specified domain event asynchronously.
    /// </summary>
    /// <param name="event">The domain event to handle.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleAsync(TEvent @event);
}
