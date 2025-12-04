using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LabResultsGateway.API.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API.Application.Events;

/// <summary>
/// Implementation of domain event dispatcher that resolves handlers from the DI container.
/// </summary>
public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainEventDispatcher"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving handlers.</param>
    /// <param name="logger">The logger for recording dispatch operations.</param>
    public DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(IDomainEvent domainEvent)
    {
        if (domainEvent == null)
        {
            throw new ArgumentNullException(nameof(domainEvent));
        }

        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices(handlerType).Cast<object>();

        if (!handlers.Any())
        {
            _logger.LogInformation("No handlers registered for event type {EventType}", eventType.Name);
            return;
        }

        var tasks = new List<Task>();
        foreach (var handler in handlers)
        {
            tasks.Add(DispatchToHandlerAsync(handler, domainEvent, eventType));
        }

        await Task.WhenAll(tasks);
    }

    private async Task DispatchToHandlerAsync(object handler, IDomainEvent domainEvent, Type eventType)
    {
        try
        {
            var handleMethod = handler.GetType().GetMethod("HandleAsync");
            if (handleMethod == null)
            {
                _logger.LogWarning("Handler {HandlerType} does not have HandleAsync method", handler.GetType().Name);
                return;
            }

            var task = (Task?)handleMethod.Invoke(handler, new[] { domainEvent });
            if (task != null)
            {
                await task;
            }

            _logger.LogInformation("Successfully dispatched {EventType} to handler {HandlerType}",
                eventType.Name, handler.GetType().Name);
        }
        catch (Exception ex)
        {
            // Log the exception but don't let it affect other handlers or the main processing flow
            _logger.LogError(ex, "Error dispatching {EventType} to handler {HandlerType}",
                eventType.Name, handler.GetType().Name);
        }
    }
}
