using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>A web link owned by a supplier or a contract.</summary>
public class Link : SyncEntity
{
    /// <summary>Id of the owning supplier or contract.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Purpose of the link.</summary>
    public LinkKind Kind { get; set; }

    /// <summary>Display title; the UI falls back to the localized kind name.</summary>
    public string? Title { get; set; }

    /// <summary>Absolute http/https URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Position among the links of the owner.</summary>
    public int SortOrder { get; set; }

    /// <summary>Returns the URL as an absolute http/https URI, or null if it is not one.</summary>
    public Uri? TryGetUri() =>
        Uri.TryCreate(Url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;
}
