using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace cdeApi;

/// <summary>The per-user settings the frontend persists: the UI-state document and the colour rules.</summary>
public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/ui-state", GetUiState);
        app.MapPut("/ui-state", PutUiStateAsync);
        app.MapGet("/colour-rules", GetColourRules);
        app.MapGet("/colour-rules/defaults", GetColourRuleDefaults);
        app.MapPut("/colour-rules", PutColourRulesAsync);
        return app;
    }

    /// <summary><c>GET /ui-state</c>: the user's overrides merged over the defaults.</summary>
    public static IResult GetUiState(UiStateStore store) =>
        Results.Content(store.GetMergedJson(), "application/json");

    /// <summary><c>PUT /ui-state</c>: stores the raw document; 400 when the body is not JSON.</summary>
    public static async Task<IResult> PutUiStateAsync(HttpRequest request, UiStateStore store)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync();
        try
        {
            store.Save(body);
            return Results.Ok();
        }
        catch (JsonException)
        {
            return Results.BadRequest("body is not valid JSON");
        }
    }

    /// <summary>
    /// <c>GET /colour-rules</c>: plain text, stored as typed. Creates the file from the defaults when missing.
    /// </summary>
    public static IResult GetColourRules(ColourRulesStore store) =>
        Results.Text(store.Read(), "text/plain", Encoding.UTF8);

    /// <summary><c>GET /colour-rules/defaults</c>: the single source of the default text (the UI's "Reset to defaults").</summary>
    public static IResult GetColourRuleDefaults() =>
        Results.Text(ColourRulesStore.DefaultText, "text/plain", Encoding.UTF8);

    /// <summary><c>PUT /colour-rules</c>: stores the text as typed; 413 above <see cref="ColourRulesStore.MaxLength"/>.</summary>
    public static async Task<IResult> PutColourRulesAsync(HttpRequest request, ColourRulesStore store)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        if (body.Length > ColourRulesStore.MaxLength)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        store.Save(body);
        return Results.Ok();
    }
}
