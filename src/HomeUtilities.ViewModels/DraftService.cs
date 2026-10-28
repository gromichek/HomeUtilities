using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;

namespace HomeUtilities.ViewModels;

/// <summary>What happened when a draft bill was confirmed.</summary>
public enum DraftConfirmResult
{
    /// <summary>The bill is a normal bill now.</summary>
    Confirmed,

    /// <summary>The bill does not exist or is not a draft.</summary>
    NotADraft,

    /// <summary>The total is still 0: the amount has to be entered first.</summary>
    AmountRequired,

    /// <summary>The bill is not valid as it is (e.g. a missing title); it has to be opened and fixed.</summary>
    Invalid,
}

/// <summary>Confirms draft bills (created from templates) without opening the editor: the Confirm button of a row.</summary>
public sealed class DraftService(IBillStore bills, IContractStore contracts)
{
    /// <summary>Confirms the draft when it has an amount and is valid; otherwise says what is missing and changes nothing.</summary>
    public async Task<DraftConfirmResult> ConfirmAsync(Guid billId, CancellationToken ct = default)
    {
        if (await bills.GetAsync(billId, ct) is not { IsDraft: true } bill)
        {
            return DraftConfirmResult.NotADraft;
        }

        if (bill.Gross == 0)
        {
            return DraftConfirmResult.AmountRequired;
        }

        var contract = bill.ContractId is { } contractId ? await contracts.GetAsync(contractId, ct) : null;
        if (!BillValidator.Validate(bill, contract).IsValid)
        {
            return DraftConfirmResult.Invalid;
        }

        bill.IsDraft = false;
        await bills.SaveAsync(bill, ct);
        return DraftConfirmResult.Confirmed;
    }
}
