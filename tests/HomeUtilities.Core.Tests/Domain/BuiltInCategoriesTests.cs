using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tests.Domain;

public class BuiltInCategoriesTests
{
    [Fact]
    public void One_category_per_service_kind_in_enum_order()
    {
        var categories = BuiltInCategories.Create();

        Assert.Equal(Enum.GetValues<ServiceKind>(), categories.Select(c => c.SystemKey!.Value));
        Assert.Equal(Enumerable.Range(0, categories.Count), categories.Select(c => c.SortOrder));
        Assert.All(categories, c =>
        {
            Assert.True(c.IsBuiltIn);
            Assert.Null(c.Name);
        });
    }

    [Fact]
    public void Colours_are_unique_valid_and_deterministic()
    {
        var first = BuiltInCategories.Create();
        var second = BuiltInCategories.Create();

        Assert.Equal(first.Count, first.Select(c => c.Color).Distinct().Count());
        Assert.Equal(first.Select(c => c.Color), second.Select(c => c.Color));
        Assert.All(first, c => Assert.True(CategoryValidator.Validate(c).IsValid));
    }

    [Fact]
    public void Each_call_creates_new_ids()
    {
        Assert.Empty(BuiltInCategories.Create().Select(c => c.Id).Intersect(BuiltInCategories.Create().Select(c => c.Id)));
    }

    [Fact]
    public void FindFor_returns_the_built_in_category_of_the_kind()
    {
        var categories = BuiltInCategories.Create();
        categories = [.. categories, new Category { Name = "Kindergarten" }];

        var id = BuiltInCategories.FindFor(ServiceKind.Gas, categories);

        Assert.Equal(categories.Single(c => c.SystemKey == ServiceKind.Gas).Id, id);
    }

    [Fact]
    public void FindFor_ignores_deleted_and_user_categories()
    {
        var gas = BuiltInCategories.Create().Single(c => c.SystemKey == ServiceKind.Gas);
        gas.IsDeleted = true;

        Assert.Null(BuiltInCategories.FindFor(ServiceKind.Gas, [gas, new Category { Name = "Gas" }]));
    }
}
