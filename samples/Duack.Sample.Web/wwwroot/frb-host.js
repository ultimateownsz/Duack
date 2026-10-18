// Duack web host script — drives the KNI game loop via requestAnimationFrame.
let dotnetHelper = null;
let pendingStart = false;

function resizeCanvas() {
    const c = document.getElementById('theCanvas');
    if (!c) return;
    c.width = window.innerWidth;
    c.height = window.innerHeight;
}

function startGame() {
    if (dotnetHelper) {
        dotnetHelper.invokeMethodAsync('StartGame');
        pendingStart = false;
    } else {
        // Blazor not ready yet — will start when initRenderJS fires.
        pendingStart = true;
    }
}

function initRenderJS(dotnetHelperRef) {
    dotnetHelper = dotnetHelperRef;
    resizeCanvas();
    if (pendingStart) {
        dotnetHelper.invokeMethodAsync('StartGame');
        pendingStart = false;
    }
    requestAnimationFrame(tickJS);
}

function tickJS() {
    try {
        if (dotnetHelper) {
            dotnetHelper.invokeMethod('TickDotNet');
        }
    } catch (e) {
        // instance not ready yet
    }
    requestAnimationFrame(tickJS);
}

window.addEventListener('resize', resizeCanvas);
window.addEventListener('DOMContentLoaded', () => {
    const btn = document.getElementById('start-btn');
    if (btn) btn.addEventListener('click', startGame);
    const boot = document.getElementById('boot-screen');
    if (boot) boot.addEventListener('click', (e) => {
        if (e.target === boot || e.target.closest('.start-btn')) startGame();
    });
});
