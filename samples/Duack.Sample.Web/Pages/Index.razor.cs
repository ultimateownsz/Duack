using System;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Xna.Framework;

namespace Duack.Sample.Web.Pages;

public partial class Index
{
    [Inject] public Func<Game> GameFactory { get; set; } = default!;

    private Game? _game;

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);
        if (firstRender)
        {
            JsRuntime.InvokeAsync<object>("initRenderJS", DotNetObjectReference.Create(this));
        }
    }

    [JSInvokable]
    public void TickDotNet()
    {
        if (_game == null)
        {
            _game = GameFactory();
            _game.Run();
        }
        _game.Tick();
    }
}
