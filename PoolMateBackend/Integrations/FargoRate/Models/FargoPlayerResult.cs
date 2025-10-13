namespace PoolMate.Api.Integrations.FargoRate.Models
{
    public class FargoPlayerResult
    {
        public string FargoId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string? Location { get; set; }      
        public int? Robustness { get; set; }

        public static FargoPlayerResult FromFargoPlayerData(FargoPlayerData data)
        {
            return new FargoPlayerResult
            {
                FargoId = data.MembershipId ?? string.Empty,
                FullName = $"{data.FirstName ?? ""} {data.LastName ?? ""}".Trim(),
                Rating = int.TryParse(data.Rating, out var rating) ? rating : 0,  // ← Parse string to int
                Location = data.Location,
                Robustness = int.TryParse(data.Robustness, out var robustness) ? robustness : (int?)null  // ← Parse string to int
            };
        }
    }
}
