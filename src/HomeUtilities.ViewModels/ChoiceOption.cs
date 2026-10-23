namespace HomeUtilities.ViewModels;

/// <summary>An entry of a selector row (category, supplier): its id, display name and colour dot.</summary>
/// <param name="Id">Id of the entity; <see cref="Guid.Empty"/> for the "new …" entry.</param>
/// <param name="Name">Localized display name.</param>
/// <param name="Color">Colour as <c>#RRGGBB</c>, or empty when the entry has no dot.</param>
public sealed record ChoiceOption(Guid Id, string Name, string Color);
