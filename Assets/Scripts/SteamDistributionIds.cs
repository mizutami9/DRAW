namespace DrawBody.Prototype
{
    /// <summary>
    /// Steam application/depot identifiers shared by the player and Editor build pipeline.
    /// Keeping them in one place prevents a correctly uploaded build from restarting into
    /// a different Steam application at runtime.
    /// </summary>
    public static class SteamDistributionIds
    {
        public const uint DemoAppId = 5231000;
        public const uint DemoDepotId = 5231001;
        public const uint PlaytestAppId = 5230890;
        public const uint PlaytestDepotId = 5230891;
    }
}
