using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests.Common;

public class EnumTextTests
{
    [Fact]
    public void Get_ReturnsLocalizedName()
    {
        var localizer = new FakeLocalizer(new FakeLanguageService());

        Assert.Equal("Enum_PropertyKind_Cottage [en]", EnumText.Get(localizer, PropertyKind.Cottage));
    }

    [Fact]
    public void Get_MissingKey_ReturnsMemberName()
    {
        var localizer = new FakeLocalizer(new FakeLanguageService());
        localizer.MissingKeys.Add("Enum_PropertyKind_Cottage");

        Assert.Equal("Cottage", EnumText.Get(localizer, PropertyKind.Cottage));
    }

    [Fact]
    public void Options_ListAllMembersInOrder()
    {
        var localizer = new FakeLocalizer(new FakeLanguageService());

        var options = EnumText.Options<PropertyKind>(localizer);

        Assert.Equal(Enum.GetValues<PropertyKind>().Length, options.Count);
        Assert.Equal(PropertyKind.House, options[0].As<PropertyKind>());
        Assert.Equal("Enum_PropertyKind_House [en]", options[0].DisplayName);
    }
}
