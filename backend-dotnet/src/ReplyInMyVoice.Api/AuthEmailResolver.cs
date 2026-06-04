using System.Security.Claims;

namespace ReplyInMyVoice.Api;

public static class AuthEmailResolver
{
    private const string MappedObjectIdentifierClaim =
        "http://schemas.microsoft.com/identity/claims/objectidentifier";

    public static string? ResolveEmailFromClaims(ClaimsPrincipal user)
    {
        var direct = FirstClaimValue(user, ClaimTypes.Email, "email");
        if (direct is not null)
        {
            return direct;
        }

        var verifiedPrimaryEmail = FirstClaimValue(user, "verified_primary_email");
        if (verifiedPrimaryEmail is not null)
        {
            return verifiedPrimaryEmail;
        }

        var emails = FirstClaimValue(user, "emails");
        if (emails is not null)
        {
            return emails;
        }

        var preferredUsername = FirstClaimValue(user, "preferred_username");
        return preferredUsername is not null && !IsSyntheticEntraUpn(preferredUsername, user)
            ? preferredUsername
            : null;
    }

    private static string? FirstClaimValue(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = user
                .FindAll(claimType)
                .Select(claim => claim.Value.Trim())
                .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static bool IsSyntheticEntraUpn(string value, ClaimsPrincipal user)
    {
        var candidate = value.Trim();
        var atIndex = candidate.IndexOf('@', StringComparison.Ordinal);
        if (atIndex <= 0 || atIndex >= candidate.Length - 1)
        {
            return false;
        }

        var localPart = candidate[..atIndex];
        var domain = candidate[(atIndex + 1)..];
        if (domain.EndsWith(".onmicrosoft.com", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return UserIdentifierClaims(user)
            .Any(identifier => string.Equals(identifier, localPart, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> UserIdentifierClaims(ClaimsPrincipal user)
    {
        foreach (var claimType in new[]
        {
            "oid",
            MappedObjectIdentifierClaim,
            "sub",
            ClaimTypes.NameIdentifier,
        })
        {
            foreach (var claim in user.FindAll(claimType))
            {
                var value = claim.Value.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
            }
        }
    }
}
