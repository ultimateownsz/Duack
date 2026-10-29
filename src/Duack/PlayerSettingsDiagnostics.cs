namespace Duack;

/// <summary>
/// Receives diagnostic messages from the player-settings layer (load/save failures).
/// The default implementation is silent; consumers opt in by assigning one to
/// <see cref="PlayerSettings.Diagnostics"/> (e.g. <see cref="ConsolePlayerSettingsDiagnostics"/>).
/// </summary>
public interface IPlayerSettingsDiagnostics
{
    void Report(string message);
}

/// <summary>Silent diagnostic sink used by default.</summary>
public sealed class NullPlayerSettingsDiagnostics : IPlayerSettingsDiagnostics
{
    public void Report(string message) { }
}

/// <summary>Diagnostics sink that writes to <see cref="Console"/>.</summary>
public sealed class ConsolePlayerSettingsDiagnostics : IPlayerSettingsDiagnostics
{
    public void Report(string message) => Console.WriteLine(message);
}
