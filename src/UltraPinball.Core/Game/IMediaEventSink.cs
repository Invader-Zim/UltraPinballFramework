namespace UltraPinball.Core.Game;

/// <summary>
/// Receives game events and forwards them to a media controller process.
/// Set <see cref="GameController.Media"/> to an implementation before calling
/// <see cref="GameController.RunAsync"/> to enable media output.
///
/// <para>
/// <see cref="Post"/> is always called on the game-loop thread, so implementations
/// do not need to handle concurrent calls. If the media controller is unavailable,
/// implementations should silently drop the event rather than throwing.
/// </para>
/// </summary>
public interface IMediaEventSink
{
    /// <summary>
    /// Posts a named event to the media controller with an optional data payload.
    /// The payload is serialized to JSON by the implementation.
    /// </summary>
    /// <param name="eventType">Snake-case event name, e.g. <c>"ball_starting"</c>.</param>
    /// <param name="data">
    /// Optional data object. Pass an anonymous type or a record; the implementation
    /// serializes it. Pass <c>null</c> for events with no payload.
    /// </param>
    void Post(string eventType, object? data = null);
}
