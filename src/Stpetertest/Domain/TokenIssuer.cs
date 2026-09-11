using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Stpetertest.Domain;

// The transport layer asks for this so a test can substitute it, and so the
// auth rules can be tested without standing up an HTTP host.
public interface ITokenIssuer
{
    bool IsConfigured { get; }

    string? Issue(string username, string password);

    bool IsValid(string? token);
}

/// <summary>
/// Exchanges a configured credential for an opaque bearer token.
///
/// There is no default credential and there must not be one: this service is
/// deployed to a public URL, so a fallback would be the same fallback on every
/// environment. When nothing is configured the issuer refuses outright, which
/// is the safe direction to fail.
/// </summary>
public sealed class TokenIssuer : ITokenIssuer
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _issued = new();
    private readonly string? _username;
    private readonly string? _password;
    private readonly TimeSpan _lifetime;

    public TokenIssuer(string? username, string? password, TimeSpan? lifetime = null)
    {
        _username = username;
        _password = password;
        _lifetime = lifetime ?? TimeSpan.FromHours(1);
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_username) && !string.IsNullOrWhiteSpace(_password);

    public string? Issue(string username, string password)
    {
        if (!IsConfigured)
        {
            return null;
        }

        // Fixed-time comparison: an ordinary == returns as soon as two strings
        // differ, which leaks the credential a character at a time to anyone
        // measuring the response.
        if (!FixedTimeEquals(_username!, username) || !FixedTimeEquals(_password!, password))
        {
            return null;
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _issued[token] = DateTimeOffset.UtcNow.Add(_lifetime);
        return token;
    }

    public bool IsValid(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || !_issued.TryGetValue(token, out var expiresAt))
        {
            return false;
        }

        if (expiresAt > DateTimeOffset.UtcNow)
        {
            return true;
        }

        // Dropped on use rather than swept: the set is small, and a token past
        // its window must never validate again.
        _issued.TryRemove(token, out _);
        return false;
    }

    private static bool FixedTimeEquals(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(actual));
}
