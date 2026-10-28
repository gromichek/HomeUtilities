namespace HomeUtilities.Core.Billing;

/// <summary>A default detail line of a bill template; becomes a <see cref="BillLine"/> of every bill created from it.</summary>
public sealed class BillTemplateLine
{
    /// <summary>What the line is for.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Quantity.</summary>
    public decimal? Quantity { get; set; }

    /// <summary>Unit of the quantity.</summary>
    public string? Unit { get; set; }

    /// <summary>Price per unit.</summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>Amount excluding VAT.</summary>
    public decimal? Net { get; set; }

    /// <summary>VAT rate in percent.</summary>
    public decimal? VatPercent { get; set; }

    /// <summary>VAT amount.</summary>
    public decimal? Vat { get; set; }

    /// <summary>Total including VAT; 0 when the user fills it in later.</summary>
    public decimal Gross { get; set; }

    /// <summary>Category of the line if it differs from the bill's.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Free-text comment.</summary>
    public string? Note { get; set; }

    /// <summary>The line of an existing bill, as a template line.</summary>
    public static BillTemplateLine From(BillLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return new()
        {
            Description = line.Description,
            Quantity = line.Quantity,
            Unit = line.Unit,
            UnitPrice = line.UnitPrice,
            Net = line.Net,
            VatPercent = line.VatPercent,
            Vat = line.Vat,
            Gross = line.Gross,
            CategoryId = line.CategoryId,
            Note = line.Note,
        };
    }

    /// <summary>A new bill line with this content.</summary>
    public BillLine ToBillLine(Guid billId, int sortOrder) => new()
    {
        BillId = billId,
        Description = Description,
        Quantity = Quantity,
        Unit = Unit,
        UnitPrice = UnitPrice,
        Net = Net,
        VatPercent = VatPercent,
        Vat = Vat,
        Gross = Gross,
        CategoryId = CategoryId,
        Note = Note,
        SortOrder = sortOrder,
    };
}
