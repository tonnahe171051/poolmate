using System.Text.RegularExpressions;

namespace PoolMate.Api.Common;

/// <summary>
/// Validation helper for player data
/// </summary>
public static class PlayerDataValidator
{
    // Email regex pattern (RFC 5322 simplified)
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    // Phone regex patterns (supports various formats)
    private static readonly Regex PhoneRegex = new(
        @"^[\d\s\-\+\(\)]{7,20}$",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Validate email format
    /// </summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            return EmailRegex.IsMatch(email);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validate phone format
    /// </summary>
    public static bool IsValidPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return false;

        try
        {
            // Remove whitespace for validation
            var cleanPhone = phone.Replace(" ", "").Replace("-", "");
            return PhoneRegex.IsMatch(phone) && cleanPhone.Length >= 7;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validate skill level range
    /// </summary>
    public static bool IsValidSkillLevel(int? skillLevel)
    {
        if (!skillLevel.HasValue)
            return true;  // Null is acceptable

        return skillLevel.Value >= 1 && skillLevel.Value <= 10;
    }

    /// <summary>
    /// Calculate similarity between two strings (Levenshtein distance)
    /// Returns percentage (0-100)
    /// </summary>
    public static int CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2))
            return 100;

        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
            return 0;

        s1 = s1.ToLower().Trim();
        s2 = s2.ToLower().Trim();

        if (s1 == s2)
            return 100;

        int distance = LevenshteinDistance(s1, s2);
        int maxLength = Math.Max(s1.Length, s2.Length);
        
        if (maxLength == 0)
            return 100;

        double similarity = (1.0 - (double)distance / maxLength) * 100;
        return (int)Math.Round(similarity);
    }

    /// <summary>
    /// Levenshtein distance algorithm
    /// </summary>
    private static int LevenshteinDistance(string s1, string s2)
    {
        int[,] d = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            d[i, 0] = i;

        for (int j = 0; j <= s2.Length; j++)
            d[0, j] = j;

        for (int j = 1; j <= s2.Length; j++)
        {
            for (int i = 1; i <= s1.Length; i++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;

                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost
                );
            }
        }

        return d[s1.Length, s2.Length];
    }

    /// <summary>
    /// Check for suspicious patterns in player data
    /// </summary>
    public static List<string> DetectSuspiciousData(string? fullName, string? email, string? phone)
    {
        var issues = new List<string>();

        // Check for test/dummy names
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var lowerName = fullName.ToLower();
            if (lowerName.Contains("test") || lowerName.Contains("dummy") || 
                lowerName.Contains("fake") || lowerName == "asdf" ||
                lowerName.All(c => c == lowerName[0]))  // All same character
            {
                issues.Add("Suspicious name pattern detected");
            }
        }

        // Check for invalid email patterns
        if (!string.IsNullOrWhiteSpace(email))
        {
            var lowerEmail = email.ToLower();
            if (lowerEmail.Contains("test@") || lowerEmail.Contains("fake@") ||
                lowerEmail.Contains("dummy@") || lowerEmail.Contains("temp@"))
            {
                issues.Add("Suspicious email pattern detected");
            }
        }

        // Check for repeated phone patterns
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length > 0 && digitsOnly.All(c => c == digitsOnly[0]))
            {
                issues.Add("Suspicious phone pattern detected (all same digits)");
            }
        }

        return issues;
    }
}

