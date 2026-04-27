namespace LocalTranslatorApp.Models;

public sealed record LanguageOption(string Name, params string[] Aliases)
{
    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               Aliases.Any(alias => alias.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    public override string ToString()
    {
        return Name;
    }
}
