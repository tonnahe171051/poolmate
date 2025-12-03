using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PoolMate.Api.Common;

public static class SlugHelper
{
    public static string GenerateSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }
        string str = name.Normalize(NormalizationForm.FormD);
        var chars = str.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
        str = new string(chars).Normalize(NormalizationForm.FormC).ToLower();
        str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
        str = Regex.Replace(str, @"\s+", "-").Trim('-');

        return str;
    }
}

