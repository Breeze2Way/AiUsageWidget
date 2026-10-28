using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiUsageWidget.Scheduling;

public interface IConversationRunStore
{
    IReadOnlyList<ConversationRun> Load();
    void Save(IReadOnlyList<ConversationRun> runs);
}

public sealed class ConversationRunStore(string? directory = null) : IConversationRunStore
{
    private readonly string directory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AiUsageWidget");
    private sealed record Envelope(int Version, List<ConversationRun> Runs);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private string FilePath => Path.Combine(directory, "conversation-runs.json");

    public IReadOnlyList<ConversationRun> Load()
    {
        if (!File.Exists(FilePath)) return [];
        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(File.ReadAllText(FilePath), JsonOptions);
            if (envelope is null || envelope.Version != 1 || envelope.Runs is null)
                throw new InvalidDataException("Invalid conversation history version.");
            Validate(envelope.Runs);
            return envelope.Runs;
        }
        catch (JsonException) { throw new InvalidDataException("Unreadable conversation history; automatic actions stopped."); }
    }

    public void Save(IReadOnlyList<ConversationRun> runs)
    {
        Validate(runs);
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $"conversation-runs.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(new Envelope(1, runs.ToList()), JsonOptions));
            File.Move(temp, FilePath, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static void Validate(IReadOnlyList<ConversationRun> runs)
    {
        var ids = new HashSet<Guid>();
        foreach (var run in runs)
        {
            if (run is null || run.RunId == Guid.Empty || !ids.Add(run.RunId) || !Enum.IsDefined(run.App) ||
                !Enum.IsDefined(run.Stage) || run.CleanupAttempts < 0 ||
                !Regex.IsMatch(run.ResultCode ?? "", "^[a-z_]{1,64}$"))
                throw new InvalidDataException("Invalid conversation history entry.");
            if (run.Conversation is { } c && (c.RunId != run.RunId || c.App != run.App ||
                !Guid.TryParseExact(c.ThreadId, "D", out _) || c.CreatedAt == default))
                throw new InvalidDataException("Invalid conversation ownership.");
            if (run.Stage is ConversationRunStage.Created or ConversationRunStage.Sending or ConversationRunStage.CleanupPending && run.Conversation is null)
                throw new InvalidDataException("Missing owned conversation.");
            if (run.SentAt is { } sent && (run.CleanupDueAt is not { } due || due < sent.AddSeconds(60)))
                throw new InvalidDataException("Invalid cleanup deadline.");
        }
    }
}
