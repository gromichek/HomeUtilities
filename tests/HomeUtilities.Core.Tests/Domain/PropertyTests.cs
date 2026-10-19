using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tests.Domain;

public class PropertyTests
{
    private static PropertyParameter Param(PropertyParameterKind kind, string from, decimal value, string? name = null) =>
        new() { Kind = kind, ValidFrom = DateOnly.Parse(from), Value = value, Name = name };

    private static Property Sample() => new()
    {
        Name = "Test apartment",
        Parameters =
        [
            Param(PropertyParameterKind.DeclaredResidents, "2020-01-01", 3),
            Param(PropertyParameterKind.DeclaredResidents, "2024-06-01", 2),
            Param(PropertyParameterKind.TotalArea, "2020-01-01", 54.5m),
        ],
    };

    [Fact]
    public void Residents_change_on_valid_from()
    {
        var p = Sample();
        Assert.Equal(3m, p.GetParameter(PropertyParameterKind.DeclaredResidents, new DateOnly(2024, 5, 31)));
        Assert.Equal(2m, p.GetParameter(PropertyParameterKind.DeclaredResidents, new DateOnly(2024, 6, 1)));
        Assert.Null(p.GetParameter(PropertyParameterKind.DeclaredResidents, new DateOnly(2019, 12, 31)));
    }

    [Fact]
    public void Area_is_unaffected_by_residents()
    {
        Assert.Equal(54.5m, Sample().GetParameter(PropertyParameterKind.TotalArea, new DateOnly(2025, 1, 1)));
    }

    [Fact]
    public void Unknown_kind_returns_null()
    {
        Assert.Null(Sample().GetParameter(PropertyParameterKind.LandArea, new DateOnly(2025, 1, 1)));
    }

    [Fact]
    public void Custom_parameter_is_matched_by_name()
    {
        var p = Sample();
        p.Parameters.Add(Param(PropertyParameterKind.Custom, "2020-01-01", 10, "Balcony"));
        p.Parameters.Add(Param(PropertyParameterKind.Custom, "2020-01-01", 20, "Cellar"));

        Assert.Equal(10m, p.GetParameter("Balcony", new DateOnly(2021, 1, 1)));
        Assert.Equal(20m, p.GetParameter("Cellar", new DateOnly(2021, 1, 1)));
        Assert.Null(p.GetParameter("Garage", new DateOnly(2021, 1, 1)));
    }

    [Fact]
    public void Deleted_parameter_is_ignored()
    {
        var p = Sample();
        p.Parameters[1].IsDeleted = true;
        Assert.Equal(3m, p.GetParameter(PropertyParameterKind.DeclaredResidents, new DateOnly(2025, 1, 1)));
    }
}
