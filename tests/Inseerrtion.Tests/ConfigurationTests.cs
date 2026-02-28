using System;
using FluentAssertions;
using Inseerrtion.Configuration;
using Xunit;

namespace Inseerrtion.Tests
{
    /// <summary>
    /// Tests for plugin configuration.
    /// </summary>
    public class ConfigurationTests
    {
        [Fact]
        public void PluginConfiguration_DefaultValues_AreSet()
        {
            // Arrange & Act
            var config = new PluginConfiguration();

            // Assert
            config.SeerrBaseUrl.Should().BeEmpty();
            config.SeerrApiKey.Should().BeEmpty();
            config.EnableDebugLogging.Should().BeFalse();
            config.DefaultRequestQuota.Should().Be(0);
            config.AutoApproveAdminRequests.Should().BeTrue();
        }

        [Fact]
        public void PluginConfiguration_SetValues_AreStored()
        {
            // Arrange
            var config = new PluginConfiguration
            {
                SeerrBaseUrl = "http://localhost:5050",
                SeerrApiKey = "test-api-key-12345",
                EnableDebugLogging = true,
                DefaultRequestQuota = 10,
                AutoApproveAdminRequests = false
            };

            // Assert
            config.SeerrBaseUrl.Should().Be("http://localhost:5050");
            config.SeerrApiKey.Should().Be("test-api-key-12345");
            config.EnableDebugLogging.Should().BeTrue();
            config.DefaultRequestQuota.Should().Be(10);
            config.AutoApproveAdminRequests.Should().BeFalse();
        }

        [Theory]
        [InlineData(0, true)]   // Unlimited
        [InlineData(1, true)]   // Minimum valid
        [InlineData(50, true)]  // Mid range
        [InlineData(100, true)] // Maximum valid
        public void PluginConfiguration_RequestQuota_ValidValues(int quota, bool shouldBeValid)
        {
            // Arrange
            var config = new PluginConfiguration();

            // Act
            config.DefaultRequestQuota = quota;

            // Assert
            config.DefaultRequestQuota.Should().Be(quota);
        }
    }
}
