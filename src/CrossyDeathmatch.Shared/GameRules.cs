namespace CrossyDeathmatch.Shared;

/// <summary>Tunable numbers shared by the server simulation and the client UI.</summary>
public static class GameRules
{
    public const int GridWidth = 13;
    public const int TickRate = 20;
    public const int DefaultRoundSeconds = 180;
    public const int MaxPlayers = 4;
    public const int MaxNameLength = 16;

    public const int StartSafeLanes = 4;
    public const int BacktrackLimit = 5;
    public const int RespawnDelayMs = 1500;
    public const int InvulnerableMs = 2000;
    public const int MaxLives = 6;

    public const int PointsPerLane = 1;
    public const int PointsPerCoin = 5;

    public const int SpeedBoostMs = 6000;
    public const double SpeedBoostFactor = 0.55;
    public const double RainSlowdown = 1.8;

    public const int TrainWarningMs = 1500;
    public const int BoulderFallMs = 1000;

    public const int MastermindMaxEnergy = 100;
    public const int MastermindStartEnergy = 40;
    public const double MastermindEnergyPerSecond = 6;

    public const int ResultsScreenMs = 10_000;
}
