using System.Text;

namespace AgentBridge.Application;

/// <summary>Pure Unicode whitespace/scalar projection; envelope/profile reader принадлежит приложению.</summary>
public static class DialogCatalogText
{
    /// <summary>Нормализует whitespace и ограничивает Unicode scalars, не разрывая surrogate pairs.</summary>
    public static string Limit(string text, int maxScalars)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxScalars);
        StringBuilder result = new();
        bool space = false;
        int count = 0;
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                space = result.Length > 0;
                continue;
            }

            if (space && count < maxScalars)
            {
                result.Append(' ');
                count++;
            }

            if (count == maxScalars) break;
            result.Append(rune.ToString());
            count++;
            space = false;
        }

        return result.ToString().TrimEnd();
    }

    /// <summary>Form C/invariant uppercase; substring остаётся literal ordinal, не SQL wildcard.</summary>
    public static string SearchKey(string title) => title.Normalize(NormalizationForm.FormC).ToUpperInvariant();
}
