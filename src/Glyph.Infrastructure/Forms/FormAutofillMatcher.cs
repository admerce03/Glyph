namespace Glyph.Infrastructure.Forms;

/// <summary>
/// Maps AcroForm field names to profile properties using simple keyword heuristics.
/// </summary>
public static class FormAutofillMatcher
{
    public enum ProfileField
    {
        None = 0,
        Name,
        Address,
        Email,
        Phone,
    }

    public static ProfileField Match(string? fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return ProfileField.None;
        }

        var n = fieldName.Trim().ToLowerInvariant();
        // Normalize separators.
        n = n.Replace('_', ' ').Replace('-', ' ').Replace('.', ' ');
        while (n.Contains("  ", StringComparison.Ordinal))
        {
            n = n.Replace("  ", " ", StringComparison.Ordinal);
        }

        if (ContainsAny(n, "email", "e-mail", "e mail")
            || n is "mail" or "e mail address")
        {
            return ProfileField.Email;
        }

        if (ContainsAny(n, "phone", "mobile", "cellphone", "cell phone", "telephone", "tel "))
        {
            return ProfileField.Phone;
        }

        if (n is "tel" or "cell" or "fax")
        {
            return ProfileField.Phone;
        }

        if (ContainsAny(n, "address", "street", "city", "zip", "postal", "postcode", "addr"))
        {
            return ProfileField.Address;
        }

        if (ContainsAny(n, "full name", "first name", "last name", "given name", "family name", "your name")
            || n is "name" or "applicant" or "signer" or "contact name")
        {
            return ProfileField.Name;
        }

        // Trailing/leading "name" token without matching file/user names loosely.
        var tokens = n.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 0
            && tokens[^1] == "name"
            && !tokens.Contains("file")
            && !tokens.Contains("user")
            && !tokens.Contains("field"))
        {
            return ProfileField.Name;
        }

        return ProfileField.None;
    }

    public static string? ResolveValue(FormAutofillProfile profile, string? fieldName)
    {
        return Match(fieldName) switch
        {
            ProfileField.Name => NullIfEmpty(profile.Name),
            ProfileField.Address => NullIfEmpty(profile.Address),
            ProfileField.Email => NullIfEmpty(profile.Email),
            ProfileField.Phone => NullIfEmpty(profile.Phone),
            _ => null,
        };
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (haystack.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
