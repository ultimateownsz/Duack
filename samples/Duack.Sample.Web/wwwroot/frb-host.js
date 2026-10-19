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
    console.log('[Duack] startGame called, dotnetHelper:', !!dotnetHelper);
    if (dotnetHelper) {
        dotnetHelper.invokeMethodAsync('StartGame').catch(e => {
            console.error('[Duack] StartGame failed:', e);
        });
        pendingStart = false;
    } else {
        pendingStart = true;
        console.log('[Duack] dotnetHelper not ready, deferring start');
    }
}

function initRenderJS(dotnetHelperRef) {
    console.log('[Duack] initRenderJS called');
    dotnetHelper = dotnetHelperRef;
    resizeCanvas();
    if (pendingStart) {
        console.log('[Duack] deferred startGame');
        dotnetHelper.invokeMethodAsync('StartGame').catch(e => {
            console.error('[Duack] deferred StartGame failed:', e);
        });
        pendingStart = false;
    }
    // tickJS is already running from DOMContentLoaded; don't start it again.
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
    resizeCanvas();
    requestAnimationFrame(tickJS);

    const btn = document.getElementById('start-btn');
    if (btn) {
        btn.addEventListener('click', (e) => {
            e.preventDefault();
            startGame();
        });
    }
    const boot = document.getElementById('boot-screen');
    if (boot) {
        boot.addEventListener('click', (e) => {
            if (e.target === boot || e.target.closest('.start-btn')) {
                e.preventDefault();
                startGame();
            }
        });
    }
});
