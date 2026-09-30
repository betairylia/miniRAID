namespace miniRAID
{
    // Optional scene-local completion policy; legacy encounters keep their current behavior.
    public interface ICombatCompletion { bool Finished { get; } }
}
