using System;
using FluentAssertions;
using Inseerrtion.Api;
using Xunit;

namespace Inseerrtion.Tests
{
    /// <summary>
    /// Tests for API models and services.
    /// </summary>
    public class ApiTests
    {
        [Fact]
        public void HealthResponse_DefaultValues_AreSet()
        {
            // Arrange & Act
            var response = new HealthResponse();

            // Assert
            response.IsHealthy.Should().BeFalse();
            response.Message.Should().BeNull();
            response.Version.Should().BeNull();
            response.Timestamp.Should().Be(default(DateTime));
        }

        [Fact]
        public void HealthResponse_SetValues_AreStored()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var response = new HealthResponse
            {
                IsHealthy = true,
                Message = "All systems operational",
                Version = "1.0.0",
                Timestamp = now
            };

            // Assert
            response.IsHealthy.Should().BeTrue();
            response.Message.Should().Be("All systems operational");
            response.Version.Should().Be("1.0.0");
            response.Timestamp.Should().Be(now);
        }

        [Fact]
        public void GetHealth_Request_CanBeCreated()
        {
            // Arrange & Act
            var request = new GetHealth();

            // Assert
            request.Should().NotBeNull();
        }
    }
}
