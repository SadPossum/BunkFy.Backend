namespace BunkFy.Host.ServiceDefaults.Production;

internal static class BunkFyProductionAdmissionReference
{
    public static bool IsValid(string? value)
    {
        if (value is not { Length: >= 3 and <= 128 })
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) &&
                character is not ('.' or '_' or ':' or '/' or '-'))
            {
                return false;
            }
        }

        return true;
    }
}
