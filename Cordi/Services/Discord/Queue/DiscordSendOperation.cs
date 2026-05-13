using System;
using System.Threading.Tasks;

namespace Cordi.Services.Discord.Queue;

public class DiscordSendOperation
{
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = "general";
    public Func<Task> Action { get; init; } = null!;
    public Action<Exception>? OnPermanentFailure { get; init; }
    public int MaxAttempts { get; init; } = 3;

    public int Attempts { get; internal set; }
    public DateTime EnqueuedAt { get; } = DateTime.UtcNow;
}
