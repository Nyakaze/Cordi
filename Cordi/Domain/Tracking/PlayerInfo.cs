namespace Cordi.Domain.Tracking;

public class PlayerInfo
{
    [TrackHistory] public string Name { get; set; } = string.Empty;
    [TrackHistory] public string World { get; set; } = string.Empty;
    [TrackHistory] public byte? RaceId { get; set; }
    [TrackHistory] public byte? TribeId { get; set; }
    [TrackHistory] public byte? Gender { get; set; }
    [TrackHistory] public string? FreeCompanyTag { get; set; }
}
