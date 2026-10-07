namespace CalendarReader.Services;

using System.Collections.Concurrent;
using Google.Apis.Auth.OAuth2.Responses;

public class InMemoryTokenStore : ITokenStore
{
    private readonly ConcurrentDictionary<string, TokenResponse> _tokens = new();

    public void StoreToken(string sessionId, TokenResponse token)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Session ID cannot be null or empty.", nameof(sessionId));
        }

        _tokens[sessionId] = token ?? throw new ArgumentNullException(nameof(token));
    }

    public TokenResponse? GetToken(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        _tokens.TryGetValue(sessionId, out var token);
        return token;
    }

    public void RemoveToken(string sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            _tokens.TryRemove(sessionId, out _);
        }
    }

    public bool HasToken(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        return _tokens.ContainsKey(sessionId);
    }
}
