using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace cdeApi;

/// <summary>
/// Persists the frontend's UI-state document to a single user-scoped file
/// (<c>%APPDATA%\cde\ui-state.json</c>). <see cref="GetMergedJson"/> returns the user's overrides
/// merged over the core defaults; <see cref="Save"/> persists the raw document. Window geometry is
/// deliberately excluded — that is Tauri-native (D14); this store is a mostly-opaque per-frontend blob.
/// </summary>
public sealed class UiStateStore
{
    private readonly string _path;
    private readonly object _gate = new();

    public UiStateStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "cde");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "ui-state.json");
    }

    // Core defaults the API knows about. The frontend layers its own document on top.
    private static JsonObject Defaults() => new()
    {
        ["catalogColumnCount"] = 13,
        ["directoryColumnCount"] = 3,
        ["searchResultColumnCount"] = 5,
        ["searchHistory"] = new JsonArray()
    };

    /// <summary>Defaults deep-merged with the persisted user document (user wins).</summary>
    public string GetMergedJson()
    {
        var merged = Defaults();
        lock (_gate)
        {
            if (File.Exists(_path))
            {
                var userText = File.ReadAllText(_path);
                if (!string.IsNullOrWhiteSpace(userText) &&
                    JsonNode.Parse(userText) is JsonObject user)
                {
                    Merge(merged, user);
                }
            }
        }

        return merged.ToJsonString();
    }

    /// <summary>Persist the raw UI-state document supplied by the frontend.</summary>
    public void Save(string json)
    {
        // Validate it is JSON before writing so we never persist garbage.
        _ = JsonNode.Parse(json);
        lock (_gate)
        {
            File.WriteAllText(_path, json);
        }
    }

    private static void Merge(JsonObject target, JsonObject overlay)
    {
        foreach (var (key, value) in overlay)
        {
            if (value is JsonObject overlayChild && target[key] is JsonObject targetChild)
            {
                Merge(targetChild, overlayChild);
            }
            else
            {
                target[key] = value?.DeepClone();
            }
        }
    }
}
