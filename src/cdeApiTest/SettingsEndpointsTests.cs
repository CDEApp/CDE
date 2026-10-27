using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using cdeApi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NUnit.Framework;

namespace cdeApiTest;

/// <summary>Drives the <see cref="SettingsEndpoints"/> handlers against stores in a temp folder.</summary>
[TestFixture]
public class SettingsEndpointsTests
{
    private string _dir;
    private string UiStatePath => Path.Combine(_dir, "ui-state.json");
    private string ColourRulesPath => Path.Combine(_dir, "colour-rules.txt");

    [SetUp]
    public void SetUp() => _dir = Directory.CreateTempSubdirectory("cde-api-settings-test").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    private static HttpRequest RequestWithBody(string body)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return ctx.Request;
    }

    private static int? Status(IResult result) => ((IStatusCodeHttpResult)result).StatusCode;

    // ---- ui-state ----

    [Test]
    public void GetUiState_returns_the_merged_document_as_json()
    {
        var result = (ContentHttpResult)SettingsEndpoints.GetUiState(new UiStateStore(UiStatePath));

        Assert.That(result.ContentType, Is.EqualTo("application/json"));
        Assert.That((int)JsonNode.Parse(result.ResponseContent!)!["catalogColumnCount"]!, Is.EqualTo(13));
    }

    [Test]
    public async Task PutUiState_saves_valid_json()
    {
        var store = new UiStateStore(UiStatePath);

        var result = await SettingsEndpoints.PutUiStateAsync(RequestWithBody("{\"catalogColumnCount\":7}"), store);

        Assert.That(Status(result), Is.EqualTo(200));
        Assert.That((int)JsonNode.Parse(store.GetMergedJson())!["catalogColumnCount"]!, Is.EqualTo(7));
    }

    [Test]
    public async Task PutUiState_refuses_a_body_that_is_not_json()
    {
        var result = await SettingsEndpoints.PutUiStateAsync(RequestWithBody("not json"), new UiStateStore(UiStatePath));

        Assert.That(Status(result), Is.EqualTo(400));
        Assert.That(((IValueHttpResult)result).Value, Is.EqualTo("body is not valid JSON"));
        Assert.That(File.Exists(UiStatePath), Is.False);
    }

    // ---- colour rules ----

    [Test]
    public void GetColourRules_creates_the_file_from_the_defaults()
    {
        var result = (ContentHttpResult)SettingsEndpoints.GetColourRules(new ColourRulesStore(ColourRulesPath));

        Assert.That(result.ContentType, Does.StartWith("text/plain"));
        Assert.That(result.ResponseContent, Is.EqualTo(ColourRulesStore.DefaultText));
        Assert.That(File.Exists(ColourRulesPath), Is.True);
    }

    [Test]
    public void GetColourRuleDefaults_returns_the_default_text()
    {
        var result = (ContentHttpResult)SettingsEndpoints.GetColourRuleDefaults();

        Assert.That(result.ContentType, Does.StartWith("text/plain"));
        Assert.That(result.ResponseContent, Is.EqualTo(ColourRulesStore.DefaultText));
    }

    [Test]
    public async Task PutColourRules_stores_the_text_as_typed()
    {
        var store = new ColourRulesStore(ColourRulesPath);
        const string rules = "\\.txt$ red\nnot a valid rule\n";

        var result = await SettingsEndpoints.PutColourRulesAsync(RequestWithBody(rules), store);

        Assert.That(Status(result), Is.EqualTo(200));
        Assert.That(store.Read(), Is.EqualTo(rules));
    }

    [Test]
    public async Task PutColourRules_refuses_text_over_the_limit()
    {
        var result = await SettingsEndpoints.PutColourRulesAsync(
            RequestWithBody(new string('x', ColourRulesStore.MaxLength + 1)), new ColourRulesStore(ColourRulesPath));

        Assert.That(Status(result), Is.EqualTo(StatusCodes.Status413PayloadTooLarge));
        Assert.That(File.Exists(ColourRulesPath), Is.False);
    }

    [Test]
    public async Task PutColourRules_accepts_text_at_the_limit()
    {
        var result = await SettingsEndpoints.PutColourRulesAsync(
            RequestWithBody(new string('x', ColourRulesStore.MaxLength)), new ColourRulesStore(ColourRulesPath));

        Assert.That(Status(result), Is.EqualTo(200));
    }
}
