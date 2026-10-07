namespace CalendarReader.Services;

using Google.Apis.Auth.OAuth2.Responses;

public interface ITokenStore
{
    void StoreToken(string sessionId, TokenResponse token);
    TokenResponse? GetToken(string sessionId);
    void RemoveToken(string sessionId);
    bool HasToken(string sessionId);
}
