using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using LabResultsGateway.API.Application.Events;
using LabResultsGateway.API.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LabResultsGateway.API.Tests.Application.Events;

public class DomainEventDispatcherTests
{
    private readonly TestServiceProvider _serviceProvider;
    private readonly Mock<ILogger<DomainEventDispatcher>> _loggerMock;
    private readonly DomainEventDispatcher _dispatcher;

    public DomainEventDispatcherTests()
    {
        _serviceProvider = new TestServiceProvider();
        _loggerMock = new Mock<ILogger<DomainEventDispatcher>>();
        _dispatcher = new DomainEventDispatcher(_serviceProvider, _loggerMock.Object);
    }

    [Fact]
    public async Task DispatchAsync_ShouldThrowArgumentNullException_WhenDomainEventIsNull()
    {
        // Act & Assert
        var action = () => _dispatcher.DispatchAsync(null!);
        await action.Should().ThrowAsync<ArgumentNullException>().WithParameterName("domainEvent");
    }

    [Fact]
    public async Task DispatchAsync_ShouldLogInformation_WhenNoHandlersRegistered()
    {
        // Arrange
        var @event = new TestDomainEvent("test-correlation");

        // Act
        await _dispatcher.DispatchAsync(@event);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("No handlers registered")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ShouldDispatchToSingleHandler()
    {
        // Arrange
        var @event = new TestDomainEvent("test-correlation");
        var handlerMock = new Mock<ITestDomainEventHandler>();
        _serviceProvider.RegisterHandlers(typeof(IEnumerable<IDomainEventHandler<TestDomainEvent>>), new[] { handlerMock.Object });

        handlerMock.Setup(h => h.HandleAsync(@event)).Returns(Task.CompletedTask);

        // Act
        await _dispatcher.DispatchAsync(@event);

        // Assert
        handlerMock.Verify(h => h.HandleAsync(@event), Times.Once);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Successfully dispatched")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ShouldDispatchToMultipleHandlers()
    {
        // Arrange
        var @event = new TestDomainEvent("test-correlation");
        var handler1Mock = new Mock<ITestDomainEventHandler>();
        var handler2Mock = new Mock<ITestDomainEventHandler>();
        _serviceProvider.RegisterHandlers(typeof(IEnumerable<IDomainEventHandler<TestDomainEvent>>), new[] { handler1Mock.Object, handler2Mock.Object });

        handler1Mock.Setup(h => h.HandleAsync(@event)).Returns(Task.CompletedTask);
        handler2Mock.Setup(h => h.HandleAsync(@event)).Returns(Task.CompletedTask);

        // Act
        await _dispatcher.DispatchAsync(@event);

        // Assert
        handler1Mock.Verify(h => h.HandleAsync(@event), Times.Once);
        handler2Mock.Verify(h => h.HandleAsync(@event), Times.Once);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Successfully dispatched")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task DispatchAsync_ShouldHandleHandlerExceptions_WithoutFailingOtherHandlers()
    {
        // Arrange
        var @event = new TestDomainEvent("test-correlation");
        var failingHandlerMock = new Mock<ITestDomainEventHandler>();
        var successfulHandlerMock = new Mock<ITestDomainEventHandler>();
        _serviceProvider.RegisterHandlers(typeof(IEnumerable<IDomainEventHandler<TestDomainEvent>>), new[] { failingHandlerMock.Object, successfulHandlerMock.Object });

        failingHandlerMock.Setup(h => h.HandleAsync(@event)).ThrowsAsync(new Exception("Handler failed"));
        successfulHandlerMock.Setup(h => h.HandleAsync(@event)).Returns(Task.CompletedTask);

        // Act
        await _dispatcher.DispatchAsync(@event);

        // Assert
        failingHandlerMock.Verify(h => h.HandleAsync(@event), Times.Once);
        successfulHandlerMock.Verify(h => h.HandleAsync(@event), Times.Once);

        // Verify error was logged for failing handler
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error dispatching")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Verify success was logged for successful handler
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Successfully dispatched")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // Test domain event and handler for testing
    public class TestDomainEvent : DomainEventBase
    {
        public TestDomainEvent(string correlationId) : base(correlationId) { }
    }

    public interface ITestDomainEventHandler : IDomainEventHandler<TestDomainEvent> { }

    // Test implementation of IServiceProvider for testing
    private class TestServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, IEnumerable<object>> _handlers = new();

        public void RegisterHandlers(Type serviceType, IEnumerable<object> handlers)
        {
            _handlers[serviceType] = handlers;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IServiceScopeFactory))
            {
                return new TestServiceScopeFactory(this);
            }

            if (_handlers.TryGetValue(serviceType, out var handlers))
            {
                return handlers;
            }

            // Return empty enumerable for unregistered handler types
            if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                var elementType = serviceType.GetGenericArguments()[0];
                if (elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
                {
                    return Array.Empty<object>();
                }
            }

            return null;
        }
    }

    private class TestServiceScopeFactory : IServiceScopeFactory
    {
        private readonly TestServiceProvider _provider;

        public TestServiceScopeFactory(TestServiceProvider provider)
        {
            _provider = provider;
        }

        public IServiceScope CreateScope()
        {
            return new TestServiceScope(_provider);
        }
    }

    private class TestServiceScope : IServiceScope
    {
        public TestServiceScope(TestServiceProvider provider)
        {
            ServiceProvider = provider;
        }

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
            // No-op for testing
        }
    }
}
