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
        _started = true;
        // Hide the boot-screen overlay now that the user has gestured (audio can play).
        JsRuntime.InvokeVoidAsync("eval", "document.getElementById('boot-screen').style.display = 'none';");
    }

    /// <summary>Called from JS once per animation frame.</summary>
    [JSInvokable]
    public void TickDotNet()
    {
        if (!_started) return;

        if (_game == null)
        {
            _game = GameFactory();
            _game.Run();
        }
        _game.Tick();
    }
}
