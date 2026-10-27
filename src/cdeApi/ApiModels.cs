using System.Collections.Generic;
using cdeAppCore.Dtos;

namespace cdeApi;

/// <summary>Catalog list + status-bar figures (catalogs loaded, total entries, process memory).</summary>
public sealed class CatalogsResponse
{
    public IReadOnlyList<CatalogInfoDto> Catalogs { get; set; }
    /// <summary>The working folder (where catalogs were last loaded from, and new ones are written); null before any load.</summary>
    public string CatalogDir { get; set; }
    public int CatalogsLoaded { get; set; }
    public long TotalEntries { get; set; }
    public long MemoryBytes { get; set; }
    /// <summary>Legacy .cde catalogs the last load found and did not load; the UI offers to migrate them.</summary>
    public IReadOnlyList<LegacyCatalogDto> LegacyCatalogs { get; set; } = [];
}

/// <summary>
/// A shell-action request. <see cref="Ref"/> is a session entry reference (catalog id + entry
/// index) — never a client-supplied path; the server resolves and existence-checks it (D11).
/// <see cref="Action"/> is one of <c>open</c>/<c>explore</c>/<c>properties</c>/<c>custom</c>;
/// for <c>custom</c>, <see cref="CommandId"/> indexes the configured custom-command list.
/// </summary>
public sealed class ShellRequest
{
    public string Ref { get; set; }
    public string Action { get; set; }
    public int? CommandId { get; set; }
}

/// <summary>A configured custom command as a menu item; <see cref="Id"/> is the <see cref="ShellRequest.CommandId"/> that runs it.</summary>
public sealed class ShellCommandInfo
{
    public int Id { get; set; }
    public string Label { get; set; }
}
