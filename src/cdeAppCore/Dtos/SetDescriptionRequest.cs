namespace cdeAppCore.Dtos;

/// <summary>Body of <c>PUT /catalogs/{id}/description</c>: the new description (empty clears it).</summary>
public sealed class SetDescriptionRequest
{
    public string Description { get; set; }
}
