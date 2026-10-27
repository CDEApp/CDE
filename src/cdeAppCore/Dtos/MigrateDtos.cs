using System.Collections.Generic;

namespace cdeAppCore.Dtos;

/// <summary>
/// A legacy <c>.cde</c> catalog found by the last load but not loaded: it has no <c>.cdex</c> of the same base name
/// beside it. <see cref="Writable"/> is whether its folder can be written to, so it can be migrated there.
/// </summary>
public sealed class LegacyCatalogDto
{
    public string Path { get; set; }
    public long SizeBytes { get; set; }
    public bool Writable { get; set; }
}

/// <summary>Migration progress: starting catalog <see cref="Current"/> of <see cref="Total"/> (<see cref="Path"/>).</summary>
public readonly record struct MigrateProgress(int Current, int Total, string Path);

/// <summary>One catalog that failed to migrate, or migrated with a warning.</summary>
public sealed class MigrateIssue
{
    public string Path { get; set; }
    public string Message { get; set; }
}

/// <summary>Final outcome of a migration (the SSE <c>done</c> payload).</summary>
public sealed class MigrateResult
{
    /// <summary>Catalogs converted to <c>.cdex</c>, including those whose source could not be renamed.</summary>
    public int Migrated { get; set; }

    /// <summary>Catalogs attempted (the writable legacy catalogs).</summary>
    public int Total { get; set; }

    /// <summary>Not converted; they stay legacy catalogs and are offered again.</summary>
    public List<MigrateIssue> Failures { get; set; } = [];

    /// <summary>Converted, but with something to report (the source could not be renamed to <c>.cde.migrated</c>).</summary>
    public List<MigrateIssue> Warnings { get; set; } = [];
}

/// <summary>The refusal / SSE <c>error</c> payload for a migration. <see cref="Code"/> is a stable machine-readable reason.</summary>
public sealed class MigrateError
{
    public string Code { get; set; }
    public string Message { get; set; }
}
