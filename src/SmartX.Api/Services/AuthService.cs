using System.Collections.Concurrent;
using SmartX.Api.Contracts;

namespace SmartX.Api.Services;

public sealed class OperatorSession
{
    public Guid Token { get; init; }
    public required string MacAddress { get; init; }
    public required string DisplayName { get; init; }
    public DateTimeOffset IssuedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Operator authentication is a MAC presence check only — no colon/hex format validation.
/// </summary>
public sealed class AuthService
{
    private readonly ConcurrentDictionary<Guid, OperatorSession> _sessions = new();

    public SessionDto Login(string? macAddress, RealtimeMeshEngine engine)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
        {
            throw new ArgumentException("MAC address is required.");
        }

        var mac = macAddress.Trim();
        if (!engine.TryGetDevice(mac, out var live))
        {
            throw new UnauthorizedAccessException("MAC address is not registered on this gateway.");
        }

        var session = new OperatorSession
        {
            Token = Guid.NewGuid(),
            MacAddress = live.Profile.MacAddress,
            DisplayName = live.Profile.DisplayName
        };
        _sessions[session.Token] = session;
        return new SessionDto(session.Token, session.MacAddress, session.DisplayName, session.IssuedAt);
    }

    public SessionDto? Find(Guid token)
        => _sessions.TryGetValue(token, out var session)
            ? new SessionDto(session.Token, session.MacAddress, session.DisplayName, session.IssuedAt)
            : null;

    public void Logout(Guid token) => _sessions.TryRemove(token, out _);

    public static bool TryReadToken(HttpContext http, out Guid token)
    {
        token = Guid.Empty;
        var header = http.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(header["Bearer ".Length..].Trim(), out token))
        {
            return true;
        }

        if (http.Request.Headers.TryGetValue("X-SmartX-Token", out var raw)
            && Guid.TryParse(raw.ToString(), out token))
        {
            return true;
        }

        return false;
    }
}
