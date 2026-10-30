using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>Result of checking the dates of a contract–meter link.</summary>
/// <param name="Errors">Problems that stop the link from being stored.</param>
/// <param name="StartsBeforeContract">True if the link starts before the contract does (allowed, but the owner is warned).</param>
public sealed record MeterLinkCheck(IReadOnlyList<ValidationError> Errors, bool StartsBeforeContract)
{
    /// <summary>True when there are no errors.</summary>
    public bool IsValid => Errors.Count == 0;
}

/// <summary>Rules for the dates of the link between a meter and a contract.</summary>
public static class MeterLinkRules
{
    /// <summary>
    /// The day a new link starts: the later of the meter's installation date and the contract's start. Readings from the whole
    /// life of the meter then belong to the contract, however long ago the owner links it.
    /// </summary>
    public static DateOnly DefaultStart(DateOnly meterInstalledOn, DateOnly contractValidFrom) =>
        meterInstalledOn > contractValidFrom ? meterInstalledOn : contractValidFrom;

    /// <summary>
    /// The start of a link added for a meter and contract: <see cref="DefaultStart"/>, but not before the day after the end of an earlier,
    /// ended link of the same pair (the owner switched the meter off and on again), so the links never overlap.
    /// </summary>
    /// <param name="meterInstalledOn">Installation date of the meter.</param>
    /// <param name="contractValidFrom">Start of the contract.</param>
    /// <param name="existingLinks">Links of the same meter and contract; deleted and open-ended links are ignored.</param>
    public static DateOnly NewLinkStart(DateOnly meterInstalledOn, DateOnly contractValidFrom, IEnumerable<ContractMeter> existingLinks)
    {
        ArgumentNullException.ThrowIfNull(existingLinks);

        var start = DefaultStart(meterInstalledOn, contractValidFrom);
        var lastEnd = existingLinks.Where(l => !l.IsDeleted && l.ValidTo is not null).Select(l => l.ValidTo!.Value).DefaultIfEmpty().Max();
        return lastEnd != default && lastEnd.AddDays(1) > start ? lastEnd.AddDays(1) : start;
    }

    /// <summary>
    /// Checks a link that is added or edited: it must not start before the meter was installed, must not end before it starts and
    /// must not overlap another link of the same meter and contract. Starting before the contract is only reported.
    /// </summary>
    /// <param name="candidate">The link with its new dates (its <see cref="SyncEntity.Id"/> identifies it among the others).</param>
    /// <param name="meterInstalledOn">Installation date of the meter.</param>
    /// <param name="contractValidFrom">Start of the contract.</param>
    /// <param name="otherLinks">The links of the same meter and contract; the candidate itself and deleted links are ignored.</param>
    public static MeterLinkCheck Check(
        ContractMeter candidate, DateOnly meterInstalledOn, DateOnly contractValidFrom, IEnumerable<ContractMeter> otherLinks)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(otherLinks);

        var errors = new List<ValidationError>();
        if (candidate.ValidFrom < meterInstalledOn)
        {
            errors.Add(new(ErrorCodes.ContractMeterValidFromBeforeInstallation, nameof(ContractMeter.ValidFrom), [meterInstalledOn]));
        }

        if (candidate.ValidTo is { } validTo && validTo < candidate.ValidFrom)
        {
            errors.Add(new(ErrorCodes.ContractMeterValidToBeforeValidFrom, nameof(ContractMeter.ValidTo)));
        }

        var overlapping = otherLinks
            .Where(o => !o.IsDeleted && o.Id != candidate.Id && Overlap(candidate, o))
            .OrderBy(o => o.ValidFrom)
            .FirstOrDefault();
        if (overlapping is not null)
        {
            errors.Add(new(ErrorCodes.ContractMeterOverlap, nameof(ContractMeter.ValidFrom), [overlapping.ValidFrom]));
        }

        return new MeterLinkCheck(errors, candidate.ValidFrom < contractValidFrom);
    }

    private static bool Overlap(ContractMeter a, ContractMeter b) =>
        a.ValidFrom <= (b.ValidTo ?? DateOnly.MaxValue) && b.ValidFrom <= (a.ValidTo ?? DateOnly.MaxValue);
}
