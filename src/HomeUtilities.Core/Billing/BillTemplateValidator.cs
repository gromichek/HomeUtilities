using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Billing;

/// <summary>Validation rules of <see cref="BillTemplate"/> and its <see cref="Schedule"/>.</summary>
public static class BillTemplateValidator
{
    private const int MaxDays = 365;

    /// <summary>Validates a template; deleted custom field definitions are skipped.</summary>
    public static ValidationResult Validate(BillTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(template.Name))
        {
            errors.Add(new(ErrorCodes.BillTemplateNameRequired, nameof(BillTemplate.Name)));
        }
        else if (template.Name.Length > InputLimits.Name)
        {
            errors.Add(new(ErrorCodes.BillTemplateNameTooLong, nameof(BillTemplate.Name), [InputLimits.Name]));
        }

        if (template.PropertyId == Guid.Empty)
        {
            errors.Add(new(ErrorCodes.BillTemplatePropertyRequired, nameof(BillTemplate.PropertyId)));
        }

        if (template.CategoryId == Guid.Empty)
        {
            errors.Add(new(ErrorCodes.BillTemplateCategoryRequired, nameof(BillTemplate.CategoryId)));
        }

        if (template.Title is { Length: > InputLimits.Name })
        {
            errors.Add(new(ErrorCodes.BillTemplateTitleTooLong, nameof(BillTemplate.Title), [InputLimits.Name]));
        }

        if (template.Net is not null || template.VatPercent is not null || template.Gross is not null)
        {
            var (_, error) = Amounts.Complete(template.Net, template.VatPercent, template.Gross);
            if (error is not null)
            {
                errors.Add(error);
            }
        }

        if (template.DueDayOffset is < 0 or > MaxDays || template.RemindDaysBefore is < 0 or > MaxDays)
        {
            var field = template.DueDayOffset is < 0 or > MaxDays ? nameof(BillTemplate.DueDayOffset) : nameof(BillTemplate.RemindDaysBefore);
            errors.Add(new(ErrorCodes.BillTemplateDaysOutOfRange, field, [MaxDays]));
        }

        errors.AddRange(ValidateSchedule(template.Schedule).Errors.Select(e => e with { Field = nameof(BillTemplate.Schedule) + "." + e.Field }));
        ValidationHelpers.CheckNotes(errors, template.Notes, nameof(BillTemplate.Notes));

        for (var i = 0; i < template.Lines.Count; i++)
        {
            errors.AddRange(ValidationHelpers.Prefix(nameof(BillTemplate.Lines), i, ValidateLine(template.Lines[i])));
        }

        errors.AddRange(CustomFieldValidator.ValidateAll(template.CustomFields).Errors);
        return ValidationHelpers.ToResult(errors);
    }

    /// <summary>Validates a schedule: a start month for the scheduled kinds, N within 2–24, an issue day within 1–31.</summary>
    public static ValidationResult ValidateSchedule(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var errors = new List<ValidationError>();
        if (schedule.Kind == ScheduleKind.Once)
        {
            if (schedule.StartMonth is not null || schedule.EndMonth is not null || schedule.DayOfMonth is not null || schedule.EveryMonths is not null)
            {
                errors.Add(new(ErrorCodes.ScheduleOnceHasPeriod, nameof(Schedule.Kind)));
            }

            return ValidationHelpers.ToResult(errors);
        }

        if (schedule.StartMonth is null)
        {
            errors.Add(new(ErrorCodes.ScheduleStartRequired, nameof(Schedule.StartMonth)));
        }

        if (schedule.Kind == ScheduleKind.EveryNMonths)
        {
            if (schedule.EveryMonths is not (>= Schedule.MinEveryMonths and <= Schedule.MaxEveryMonths))
            {
                errors.Add(new(ErrorCodes.ScheduleEveryMonthsOutOfRange, nameof(Schedule.EveryMonths), [Schedule.MinEveryMonths, Schedule.MaxEveryMonths]));
            }
        }
        else if (schedule.EveryMonths is not null)
        {
            errors.Add(new(ErrorCodes.ScheduleEveryMonthsNotAllowed, nameof(Schedule.EveryMonths)));
        }

        if (schedule.DayOfMonth is < 1 or > 31)
        {
            errors.Add(new(ErrorCodes.ScheduleDayOutOfRange, nameof(Schedule.DayOfMonth)));
        }

        if (schedule.StartMonth is { } start && schedule.EndMonth is { } end && end < start)
        {
            errors.Add(new(ErrorCodes.ScheduleEndBeforeStart, nameof(Schedule.EndMonth)));
        }

        return ValidationHelpers.ToResult(errors);
    }

    private static ValidationResult ValidateLine(BillTemplateLine line)
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(line.Description))
        {
            errors.Add(new(ErrorCodes.BillLineDescriptionRequired, nameof(BillTemplateLine.Description)));
        }

        if (line.Net is not null || line.VatPercent is not null || line.Vat is not null)
        {
            var (_, error) = Amounts.Complete(line.Net, line.VatPercent, line.Gross);
            if (error is not null)
            {
                errors.Add(error);
            }
        }

        ValidationHelpers.CheckNotes(errors, line.Note, nameof(BillTemplateLine.Note));
        return ValidationHelpers.ToResult(errors);
    }
}
