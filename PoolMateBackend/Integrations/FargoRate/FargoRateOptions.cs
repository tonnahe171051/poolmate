namespace PoolMate.Api.Integrations.FargoRate
{
    public class FargoRateOptions
    {
        public const string SectionName = "FargoRate";

        public string ApiBaseUrl { get; set; } = "https://dashboard.fargorate.com/api";
        public int TimeoutSeconds { get; set; } = 30;
        public int CacheDurationMinutes { get; set; } = 1440; // 24 hours
        public int MaxRetryAttempts { get; set; } = 2;
    }
}
