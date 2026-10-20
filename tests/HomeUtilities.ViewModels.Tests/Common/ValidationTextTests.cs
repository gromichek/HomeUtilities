using HomeUtilities.Core.Common;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests.Common;

public class ValidationTextTests
{
    [Fact]
    public void KeyFor_ReplacesDotsWithUnderscores()
    {
        Assert.Equal("Error_property_name_required", ValidationText.KeyFor(ErrorCodes.PropertyNameRequired));
        Assert.Equal("Error_common_notes_too_long", ValidationText.KeyFor(ErrorCodes.NotesTooLong));
    }

    [Fact]
    public void For_KnownCode_UsesLocalizedTextWithArgs()
    {
        var localizer = new FakeLocalizer(new FakeLanguageService());

        var text = ValidationText.For(localizer, new ValidationError(ErrorCodes.PropertyNameTooLong, "Name", [100]));

        Assert.Equal("Error_property_name_too_long 100 [en]", text);
    }

    [Fact]
    public void For_KnownCodeWithoutArgs_UsesPlainText()
    {
        var localizer = new FakeLocalizer(new FakeLanguageService());

        Assert.Equal("Error_property_name_required [en]", ValidationText.For(localizer, new ValidationError(ErrorCodes.PropertyNameRequired)));
    }

    [Fact]
    public void For_UnknownCode_FallsBackToGenericText()
    {
        var localizer = new FakeLocalizer(new FakeLanguageService());
        localizer.MissingKeys.Add("Error_some_unknown_code");

        var text = ValidationText.For(localizer, new ValidationError("some.unknown_code"));

        Assert.StartsWith("Error_Generic [en]", text);
    }
}
