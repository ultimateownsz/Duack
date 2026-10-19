using System;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Xna.Framework;

namespace Duack.Sample.Web.Pages;

public partial class Index
{
    [Inject] public Func<Game> GameFactory { get; set; } = default!;

    private Game? _game;
    private bool _started;
    private bool _gameRunning;

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);
        if (firstRender)
        {
            JsRuntime.InvokeAsync<object>("initRenderJS", DotNetObjectReference.Create(this));
        }
    }

    /// <summary>Called from JS when the user clicks "Start" on the boot screen.</summary>
    [JSInvokable]
    public void StartGame()
    {
        if (_gameRunning) return; // already running

        _started = true;

        // Hide the boot-screen overlay now that the user has gestured (audio can play).
        try
        {
            JsRuntime.InvokeVoidAsync("eval", "document.getElementById('boot-screen').style.display = 'none';");
        }
        catch { /* ignore - boot screen may already be hidden */ }

        // Create and run the game now that audio is unlocked by the user gesture.
        if (_game == null)
        {
            try
            {
                _game = GameFactory();
                _game.Run();
                _gameRunning = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Duack] Failed to create/run game: {ex.Message}");
                _game = null;
                _gameRunning = false;
                throw;
            }
        }
    }

    /// <summary>Called from JS once per animation frame.</summary>
    [JSInvokable]
    public void TickDotNet()
    {
        if (!_started) return;
        _game?.Tick();
    }
}
