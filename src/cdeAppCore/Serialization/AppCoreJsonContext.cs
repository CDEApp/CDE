using System.Collections.Generic;
using System.Text.Json.Serialization;
using cdeAppCore.Dtos;
using cdeLib;

namespace cdeAppCore.Serialization;

/// <summary>
/// System.Text.Json source-generated serialization context for the cross-boundary DTOs. AOT-friendly
/// and reflection-free; used by <c>cdeApi</c> for HTTP/JSON + SSE. Lives in core so the contract and
/// its wire format are defined in one place.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SearchQuery))]
[JsonSerializable(typeof(SearchResultRow))]
[JsonSerializable(typeof(List<SearchResultRow>))]
[JsonSerializable(typeof(SearchProgress))]
[JsonSerializable(typeof(DirectoryNodeDto))]
[JsonSerializable(typeof(List<DirectoryNodeDto>))]
[JsonSerializable(typeof(CatalogInfoDto))]
[JsonSerializable(typeof(List<CatalogInfoDto>))]
[JsonSerializable(typeof(CatalogLoadProgress))]
[JsonSerializable(typeof(ColumnDef))]
[JsonSerializable(typeof(List<ColumnDef>))]
[JsonSerializable(typeof(EntryRefDto))]
public partial class AppCoreJsonContext : JsonSerializerContext;
