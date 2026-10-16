using System;
using System.Runtime.InteropServices.JavaScript;

namespace Duack.KNI;

/// <summary>
/// <c>localStorage</c> adapter for the KNI/web head. This is the only place in the audio stack that
/// touches JS interop: <see cref="WebPlayerSettingsStore"/> and the whole persistence seam are plain
/// logic over <see cref="IKeyValueStorage"/>, so they are unit-tested headlessly and this thin
/// wrapper is all that needs a browser to verify.
///
/// <para>
/// <b>These bindings are synchronous on purpose, and must stay that way.</b> They used to go through
/// <c>IJSRuntime</c> and wait for the reply with <c>GetAwaiter().GetResult()</c>, on the argument
/// that "they run once at boot and on an explicit Save, never per frame". The second half of that was
/// the mistake: the settings overlay's Save is driven from the game loop, and FRB2's KNI head calls
/// <c>TickDotNet</c> synchronously from a <c>requestAnimationFrame</c> callback. Blazor WASM is single
/// threaded, so a frame that blocks waiting for a managed-to-JS call to be acknowledged cannot
/// receive that acknowledgement - the round trip needs the frame to yield, and the frame is the thing
/// waiting. <c>[JSImport]</c> is synchronous in the way that actually helps: the JS function runs and
/// its return value comes straight back, with no reply to wait for.
/// </para>
/// <para>
/// The imported names must be spelled <c>globalThis.localStorage.*</c>, and both halves of that are
/// load-bearing. The runtime's JS-side resolver effectively starts from an EMPTY OBJECT when no
/// module name is given, and only an identifier whose first segment is literally <c>globalThis</c>
/// re-roots it onto the real global. So <c>[JSImport("localStorage.setItem")]</c> throws
/// "localStorage not found" and a bare custom function name throws "must be a Function but was
/// undefined". The trailing <c>.bind(n)</c> in that resolver is why the middle segment matters:
/// <c>getItem</c>/<c>setItem</c> are invoked with <c>this</c> bound to the Storage object, not to
/// <c>globalThis</c>.
/// </para>
/// <para>
/// The bug that motivated the synchronous rewrite failed as a lie rather than a hang: a blocking wait
/// threw even though <c>localStorage.setItem</c> had already executed, so the store reported "Save
/// failed" over a write that had happened. The boot-time read escaped only because it ran on a
/// genuinely async path, so "settings load fine" and "settings never save" were both true at once.
/// </para>
/// <para>
/// <see cref="Get"/> swallows JS exceptions and returns null, which the store reads as "nothing
/// stored" and therefore falls back to the authored defaults: a browser with storage disabled
/// (private mode, blocked cookies) must not take the game down at boot. That fallback is the right
/// behaviour but a terrible failure mode if it is ever caused by something else - most likely calling
/// into JS before the Blazor runtime has finished booting - so every swallowed exception is logged
/// rather than discarded. The diagnostics use <see cref="Console.WriteLine(string)"/> and must keep
/// doing so: <c>Debug.WriteLine</c> is <c>[Conditional("DEBUG")]</c>, so in a Release build the call
/// and its message are dropped entirely, and this file only ships in Release-only web builds.
/// </para>
/// </summary>
public sealed partial class LocalStorageKeyValueStorage : IKeyValueStorage
{
    // These names MUST be spelled "globalThis.localStorage.*". See the class remarks for the exact
    // resolver semantics and why the two obvious alternatives both fail at runtime.
    // Accessibility rules differ by shape: a void partial method must NOT declare one (CS8795),
    // a non-void one MUST (CS8796). The implementation parts are emitted by the JS interop
    // source generator.
    [JSImport("globalThis.localStorage.getItem")]
    private static partial string? GetItem([JSMarshalAs<JSType.String>] string key);

    [JSImport("globalThis.localStorage.setItem")]
    static partial void SetItem(
        [JSMarshalAs<JSType.String>] string key,
        [JSMarshalAs<JSType.String>] string value);

    [JSImport("globalThis.localStorage.removeItem")]
    static partial void RemoveItem([JSMarshalAs<JSType.String>] string key);

    /// <summary>
    /// Returns the stored value, or null if not present. A JS failure is swallowed so a browser with
    /// storage disabled cannot take the game down at boot; the reason is logged because the fallback
    /// is otherwise indistinguishable from a fresh save.
    /// </summary>
    public string? Get(string key)
    {
        try
        {
            return GetItem(key);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[Settings] localStorage read of '{key}' failed; falling back to authored defaults. " +
                $"If this happens on every launch, settings will never persist. Cause: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Writes the value. Throws on failure so the store reports a real save failure rather than a
    /// false "Saved!" - the reason is logged before it propagates, since a refused quota is otherwise
    /// indistinguishable from success.
    /// </summary>
    public bool Set(string key, string value)
    {
        try
        {
            SetItem(key, value);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Settings] localStorage write of '{key}' failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>Best-effort cleanup; never blocks gameplay, but still says why it did not happen.</summary>
    public void Remove(string key)
    {
        try
        {
            RemoveItem(key);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Settings] localStorage removal of '{key}' failed: {ex.Message}");
        }
    }
}