using Xunit;

namespace FreightLink.Api.Tests;

public class HealthCheckTests
{
    [Fact]
    public void HealthCheck_ReturnsOk()
    {
        // Arrange
        var isHealthy = true;

        // Act
        // Add your test code here

        // Assert
        Assert.True(isHealthy);
    }
}
