namespace Cordi.Configuration;

public interface IBlacklistEntry
{
    string Name { get; set; }
    string World { get; set; }
    bool DisableDiscord { get; set; }
}
