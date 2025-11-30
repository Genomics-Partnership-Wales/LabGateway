using FluentAssertions;
using LabResultsGateway.API.Application.Options;
using LabResultsGateway.API.Application.Retry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LabResultsGateway.API.Tests.Application.Retry;

public class ExponentialBackoffRetryStrategyTests
{
    private readonly Mock<ILogger<ExponentialBackoffRetryStrategy>> _loggerMock;
    private readonly PoisonQueueRetryOptions _options;

    public ExponentialBackoffRetryStrategyTests()
    {
        _loggerMock = new Mock<ILogger<ExponentialBackoffRetryStrategy>>();
        _options = new PoisonQueueRetryOptions
        {
            MaxRetryAttempts = 3,
            BaseRetryDelayMinutes = 2.0,
            UseJitter = true,
            MaxJitterPercentage = 0.3
        };
    }

    private IOptions<PoisonQueueRetryOptions> CreateOptions(PoisonQueueRetryOptions options)
    {
        var mock = new Mock<IOptions<PoisonQueueRetryOptions>>();
        mock.Setup(o => o.Value).Returns(options);
        return mock.Object;
    }

    [Fact]
    public void ShouldRetry_ReturnsTrue_WhenCurrentRetryCountLessThanMaxAttempts()
    {
        // Arrange
        var strategy = new ExponentialBackoffRetryStrategy(CreateOptions(_options), _loggerMock.Object);
        var context = new RetryContext("test-correlation", 1, 3);

        // Act
        var result = strategy.ShouldRetry(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldRetry_ReturnsFalse_WhenCurrentRetryCountEqualsMaxAttempts()
    {
        // Arrange
        var strategy = new ExponentialBackoffRetryStrategy(CreateOptions(_options), _loggerMock.Object);
        var context = new RetryContext("test-correlation", 3, 3);

        // Act
        var result = strategy.ShouldRetry(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRetry_ReturnsFalse_WhenCurrentRetryCountGreaterThanMaxAttempts()
    {
        // Arrange
        var strategy = new ExponentialBackoffRetryStrategy(CreateOptions(_options), _loggerMock.Object);
        var context = new RetryContext("test-correlation", 4, 3);

        // Act
        var result = strategy.ShouldRetry(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CalculateNextRetryDelay_CalculatesCorrectExponentialBackoff_WithoutJitter()
    {
        // Arrange
        var optionsWithoutJitter = new PoisonQueueRetryOptions
        {
            MaxRetryAttempts = 3,
            BaseRetryDelayMinutes = 2.0,
            UseJitter = false,
            MaxJitterPercentage = 0.3
        };
        var strategy = new ExponentialBackoffRetryStrategy(CreateOptions(optionsWithoutJitter), _loggerMock.Object);
        var context = new RetryContext("test-correlation", 1, 3);

        // Act
        var delay = strategy.CalculateNextRetryDelay(context);

        // Assert
        // Formula: Math.Pow(2.0, 1 + 1) = Math.Pow(2.0, 2) = 4.0 minutes
        delay.Should().Be(TimeSpan.FromMinutes(4.0));
    }

    [Fact]
    public void CalculateNextRetryDelay_CalculatesCorrectExponentialBackoff_WithJitter()
    {
        // Arrange
        var strategy = new ExponentialBackoffRetryStrategy(CreateOptions(_options), _loggerMock.Object);
        var context = new RetryContext("test-correlation", 1, 3);

        // Act
        var delay = strategy.CalculateNextRetryDelay(context);

        // Assert
        // Base delay: Math.Pow(2.0, 1 + 1) = 4.0 minutes
        // With jitter (max 30%): delay should be between 4.0 * (1 - 0.3) and 4.0 * (1 + 0.3)
        // Range: 2.8 to 5.2 minutes
        delay.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMinutes(2.8));
        delay.Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(5.2));
    }

    [Fact]
    public void CalculateNextRetryDelay_CalculatesCorrectExponentialBackoff_ForDifferentRetryCounts()
    {
        // Arrange
        var optionsWithoutJitter = new PoisonQueueRetryOptions
        {
            MaxRetryAttempts = 5,
            BaseRetryDelayMinutes = 1.5,
            UseJitter = false,
            MaxJitterPercentage = 0.0
        };
        var strategy = new ExponentialBackoffRetryStrategy(CreateOptions(optionsWithoutJitter), _loggerMock.Object);

        // Act & Assert
        // Retry 0: Math.Pow(1.5, 0 + 1) = 1.5^1 = 1.5 minutes
        var delay0 = strategy.CalculateNextRetryDelay(new RetryContext("test", 0, 5));
        delay0.Should().Be(TimeSpan.FromMinutes(1.5));

        // Retry 1: Math.Pow(1.5, 1 + 1) = 1.5^2 = 2.25 minutes
        var delay1 = strategy.CalculateNextRetryDelay(new RetryContext("test", 1, 5));
        delay1.Should().Be(TimeSpan.FromMinutes(2.25));

        // Retry 2: Math.Pow(1.5, 2 + 1) = 1.5^3 = 3.375 minutes
        var delay2 = strategy.CalculateNextRetryDelay(new RetryContext("test", 2, 5));
        delay2.Should().Be(TimeSpan.FromMinutes(3.375));
    }

    [Fact]
    public void CalculateNextRetryDelay_LogsCalculatedDelay()
    {
        // Arrange
        var strategy = new ExponentialBackoffRetryStrategy(CreateOptions(_options), _loggerMock.Object);
        var context = new RetryContext("test-correlation-123", 2, 3);

        // Act
        strategy.CalculateNextRetryDelay(context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("test-correlation-123") &&
                                               o.ToString()!.Contains("3") &&
                                               o.ToString()!.Contains("3")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
