using System.Threading.Tasks;
using cdeApi;
using cdeAppCore.Dtos;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace cdeApiTest;

/// <summary>Drives <see cref="DescriptionEndpoint"/> against a real session over a temp catalog.</summary>
[TestFixture]
public class DescriptionEndpointTests
{
    private TestCatalog _catalog;

    [SetUp]
    public void SetUp() => _catalog = new TestCatalog();

    [TearDown]
    public void TearDown() => _catalog.Dispose();

    private Task<IResult> Put(int id, string description) =>
        DescriptionEndpoint.HandleAsync(id, new SetDescriptionRequest { Description = description }, _catalog.Session,
            new DefaultHttpContext());

    private static int? Status(IResult result) => ((IStatusCodeHttpResult)result).StatusCode;

    private static object Value(IResult result) => ((IValueHttpResult)result).Value;

    [Test]
    public async Task Answers_the_updated_catalog()
    {
        var result = await Put(0, "Holiday photos");

        var info = (CatalogInfoDto)Value(result);
        Assert.That(info.CatalogId, Is.EqualTo(0));
        Assert.That(info.Description, Is.EqualTo("Holiday photos"));
        Assert.That(_catalog.Session.GetCatalogs()[0].Description, Is.EqualTo("Holiday photos"));
    }

    [Test]
    public async Task Unknown_catalog_is_404()
    {
        Assert.That(Status(await Put(9, "x")), Is.EqualTo(404));
    }

    [Test]
    public async Task Too_long_is_400()
    {
        Assert.That(Status(await Put(0, new string('x', 1001))), Is.EqualTo(400));
    }

    [Test]
    public async Task Missing_body_is_400()
    {
        var result = await DescriptionEndpoint.HandleAsync(0, null, _catalog.Session, new DefaultHttpContext());
        Assert.That(Status(result), Is.EqualTo(400));
    }
}
