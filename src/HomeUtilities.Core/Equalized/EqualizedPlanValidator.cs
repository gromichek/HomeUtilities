using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Equalized;

/// <summary>Validation rules of <see cref="EqualizedPlan"/>.</summary>
public static class EqualizedPlanValidator
{
    /// <summary>Validates one plan: amount above zero, <c>ValidTo</c> not before <c>ValidFrom</c>, comment length.</summary>
    public static ValidationResult Validate(EqualizedPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var errors = new List<ValidationError>();
        if (plan.MonthlyAmount <= 0)
        {
            errors.Add(new(ErrorCodes.EqualizedPlanAmountNotPositive, nameof(EqualizedPlan.MonthlyAmount)));
        }

        if (plan.ValidTo is { } to && to < plan.ValidFrom)
        {
            errors.Add(new(ErrorCodes.EqualizedPlanValidToBeforeValidFrom, nameof(EqualizedPlan.ValidTo)));
        }

        ValidationHelpers.CheckNotes(errors, plan.Notes, nameof(EqualizedPlan.Notes));
        return ValidationHelpers.ToResult(errors);
    }

    /// <summary>
    /// Validates the plans of one contract together: every plan on its own, and no two non-deleted plans may overlap
    /// (an open-ended plan runs forever). Overlaps are reported on <c>ValidFrom</c> of the later plan.
    /// </summary>
    public static ValidationResult ValidateSet(IEnumerable<EqualizedPlan> plans)
    {
        ArgumentNullException.ThrowIfNull(plans);

        var active = plans.Where(p => !p.IsDeleted).OrderBy(p => p.ValidFrom).ToList();
        var errors = new List<ValidationError>();
        foreach (var plan in active)
        {
            errors.AddRange(Validate(plan).Errors);
        }

        for (var i = 1; i < active.Count; i++)
        {
            if (active[i - 1].ValidTo is not { } previousEnd || previousEnd >= active[i].ValidFrom)
            {
                errors.Add(new(ErrorCodes.EqualizedPlanOverlap, nameof(EqualizedPlan.ValidFrom)));
            }
        }

        return ValidationHelpers.ToResult(errors);
    }
}
