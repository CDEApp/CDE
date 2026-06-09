using System.Collections.Generic;
using cdeAppCore.Dtos;

namespace cdeApi;

/// <summary>Catalog list + status-bar figures (catalogs loaded, total entries, process memory).</summary>
public sealed class CatalogsResponse
{
    public IReadOnlyList<CatalogInfoDto> Catalogs { get; set; }
    public int CatalogsLoaded { get; set; }
    public long TotalEntries { get; set; }
    public long MemoryBytes { get; set; }
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
