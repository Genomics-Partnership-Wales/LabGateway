using System.Threading.Tasks;
using LabResultsGateway.API.Domain.Events;

namespace LabResultsGateway.API.Application.Events;

/// <summary>
/// Interface for dispatching domain events to registered handlers.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Dispatches the specified domain event to all registered handlers asynchronously.
    /// </summary>
    /// <param name="domainEvent">The domain event to dispatch.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DispatchAsync(IDomainEvent domainEvent);
}
