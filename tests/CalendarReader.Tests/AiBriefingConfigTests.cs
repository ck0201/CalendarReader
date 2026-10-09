namespace CalendarReader.Tests;

using CalendarReader.Models;
using Microsoft.Extensions.Configuration;
using Xunit;

public class AiBriefingConfigTests
{
    [Fact]
    public void DefaultConfiguration_HasGemini38FlashAsDefaultModel()
    {
        var config = new AiBriefingConfig();
        Assert.Equal("gemini-3.8-flash", config.Model);
        Assert.Equal(15, config.TimeoutSeconds);
        Assert.Equal(5, config.CacheTtlMinutes);
        Assert.False(config.IsConfigured);
    }

    [Fact]
    public void IsConfigured_WhenApiKeyProvided_ReturnsTrue()
    {
        var config = new AiBriefingConfig
        {
            ApiKey = "test-api-key",
            Model = "gemini-3.8-flash"
        };

        Assert.True(config.IsConfigured);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void IsConfigured_WhenApiKeyEmptyOrNull_ReturnsFalse(string? apiKey)
    {
        var config = new AiBriefingConfig
        {
            ApiKey = apiKey!,
            Model = "gemini-3.8-flash"
        };

        Assert.False(config.IsConfigured);
    }

    [Fact]
    public void ConfigurationBinding_SupportsEnvironmentVariablesAndUserSecrets()
    {
        var memoryConfig = new Dictionary<string, string?>
        {
            ["GEMINI_API_KEY"] = "secret-gemini-key",
            ["GEMINI_MODEL"] = "gemini-3.8-flash",
            ["AI_TIMEOUT_SECONDS"] = "20"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(memoryConfig)
            .Build();

        var configSection = configuration.GetSection("Ai");

        string GetConfigValue(string flatKey, string sectionKey, string defaultValue = "")
        {
            var val = configuration[flatKey];
            if (string.IsNullOrWhiteSpace(val))
            {
                val = configSection[sectionKey];
            }
            if (string.IsNullOrWhiteSpace(val))
            {
                val = Environment.GetEnvironmentVariable(flatKey);
            }
            return string.IsNullOrWhiteSpace(val) ? defaultValue : val;
        }

        var config = new AiBriefingConfig();
        config.ApiKey = GetConfigValue("GEMINI_API_KEY", "ApiKey");
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            config.ApiKey = GetConfigValue("AI_API_KEY", "ApiKey");
        }
        config.Model = GetConfigValue("GEMINI_MODEL", "Model", "gemini-3.8-flash");

        var timeoutStr = GetConfigValue("AI_TIMEOUT_SECONDS", "TimeoutSeconds", "15");
        if (int.TryParse(timeoutStr, out var timeout) && timeout > 0)
        {
            config.TimeoutSeconds = timeout;
        }

        Assert.True(config.IsConfigured);
        Assert.Equal("secret-gemini-key", config.ApiKey);
        Assert.Equal("gemini-3.8-flash", config.Model);
        Assert.Equal(20, config.TimeoutSeconds);
    }

    [Fact]
    public void ConfigurationBinding_FallsBackToSectionKeys()
    {
        var memoryConfig = new Dictionary<string, string?>
        {
            ["Ai:ApiKey"] = "section-api-key",
            ["Ai:Model"] = "gemini-1.5-flash",
            ["Ai:TimeoutSeconds"] = "30"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(memoryConfig)
            .Build();

        var configSection = configuration.GetSection("Ai");

        string GetConfigValue(string flatKey, string sectionKey, string defaultValue = "")
        {
            var val = configuration[flatKey];
            if (string.IsNullOrWhiteSpace(val))
            {
                val = configSection[sectionKey];
            }
            return string.IsNullOrWhiteSpace(val) ? defaultValue : val;
        }

        var config = new AiBriefingConfig();
        config.ApiKey = GetConfigValue("GEMINI_API_KEY", "ApiKey");
        config.Model = GetConfigValue("GEMINI_MODEL", "Model", "gemini-3.8-flash");

        var timeoutStr = GetConfigValue("AI_TIMEOUT_SECONDS", "TimeoutSeconds", "15");
        if (int.TryParse(timeoutStr, out var timeout) && timeout > 0)
        {
            config.TimeoutSeconds = timeout;
        }

        Assert.True(config.IsConfigured);
        Assert.Equal("section-api-key", config.ApiKey);
        Assert.Equal("gemini-1.5-flash", config.Model);
        Assert.Equal(30, config.TimeoutSeconds);
    }
}
