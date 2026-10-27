using System;
using System.Security.Cryptography;

namespace cdeApi;

/// <summary>
/// The startup-handshake token. <see cref="TokenGate"/> checks every request against it and the sidecar prints it
/// on stdout for the Tauri shell. A DI singleton so both read the same value, and so tests can register a known one.
/// </summary>
public sealed class HandshakeToken(string value)
{
    public string Value { get; } = value;

    /// <summary>A fresh 32-byte random token, hex encoded.</summary>
    public static HandshakeToken CreateRandom() => new(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
}
