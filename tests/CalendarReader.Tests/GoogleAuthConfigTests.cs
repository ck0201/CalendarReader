namespace CalendarReader.Tests;

using CalendarReader.Models;
using Microsoft.Extensions.Configuration;
using Xunit;

public class GoogleAuthConfigTests
{
    [Fact]
    public void IsConfigured_WhenAllValuesProvided_ReturnsTrue()
    {
        var config = new GoogleAuthConfig
        {
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            RedirectUri = "http://localhost:5000/api/auth/callback"
        };

        Assert.True(config.IsConfigured);
    }

    [Theory]
    [InlineData("", "secret", "http://localhost:5000")]
    [InlineData("id", "", "http://localhost:5000")]
    [InlineData("id", "secret", "")]
    [InlineData(" ", "secret", "http://localhost:5000")]
    public void IsConfigured_WhenAnyValueMissing_ReturnsFalse(string clientId, string clientSecret, string redirectUri)
    {
        var config = new GoogleAuthConfig
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            RedirectUri = redirectUri
        };

        Assert.False(config.IsConfigured);
    }

    [Fact]
    public void ConfigurationBinding_SupportsFlatUserSecretsKeys()
    {
        var memoryConfig = new Dictionary<string, string?>
        {
            ["GOOGLE_CLIENT_ID"] = "user-secret-id",
            ["GOOGLE_CLIENT_SECRET"] = "user-secret-secret",
            ["GOOGLE_REDIRECT_URI"] = "http://localhost:5000/callback"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(memoryConfig)
            .Build();

        var configSection = configuration.GetSection("Google");

        string GetConfigValue(string flatKey, string sectionKey, string defaultValue = "")
        {
            var val = configuration[flatKey];
            if (string.IsNullOrWhiteSpace(val))
            {
                val = configSection[sectionKey];
            }
            return string.IsNullOrWhiteSpace(val) ? defaultValue : val;
        }

        var options = new GoogleAuthConfig
        {
            ClientId = GetConfigValue("GOOGLE_CLIENT_ID", "ClientId"),
            ClientSecret = GetConfigValue("GOOGLE_CLIENT_SECRET", "ClientSecret"),
            RedirectUri = GetConfigValue("GOOGLE_REDIRECT_URI", "RedirectUri", "http://localhost:5000/api/auth/callback")
        };

        Assert.True(options.IsConfigured);
        Assert.Equal("user-secret-id", options.ClientId);
        Assert.Equal("user-secret-secret", options.ClientSecret);
        Assert.Equal("http://localhost:5000/callback", options.RedirectUri);
    }

    [Fact]
    public void ConfigurationBinding_SupportsHierarchicalSectionKeys()
    {
        var memoryConfig = new Dictionary<string, string?>
        {
            ["Google:ClientId"] = "section-id",
            ["Google:ClientSecret"] = "section-secret"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(memoryConfig)
            .Build();

        var configSection = configuration.GetSection("Google");

        string GetConfigValue(string flatKey, string sectionKey, string defaultValue = "")
        {
            var val = configuration[flatKey];
            if (string.IsNullOrWhiteSpace(val))
            {
                val = configSection[sectionKey];
            }
            return string.IsNullOrWhiteSpace(val) ? defaultValue : val;
        }

        var options = new GoogleAuthConfig
        {
            ClientId = GetConfigValue("GOOGLE_CLIENT_ID", "ClientId"),
            ClientSecret = GetConfigValue("GOOGLE_CLIENT_SECRET", "ClientSecret"),
            RedirectUri = GetConfigValue("GOOGLE_REDIRECT_URI", "RedirectUri", "http://localhost:5000/api/auth/callback")
        };

        Assert.True(options.IsConfigured);
        Assert.Equal("section-id", options.ClientId);
        Assert.Equal("section-secret", options.ClientSecret);
        Assert.Equal("http://localhost:5000/api/auth/callback", options.RedirectUri);
    }
}
