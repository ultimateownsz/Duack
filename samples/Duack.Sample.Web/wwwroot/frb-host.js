// Duack web host script — drives the KNI game loop via requestAnimationFrame.
function resizeCanvas() {
    const c = document.getElementById('theCanvas');
    if (!c) return;
    c.width = window.innerWidth;
    c.height = window.innerHeight;
}

function tickJS() {
    try {
        if (window.theInstance) {
            window.theInstance.invokeMethod('TickDotNet');
        }
    } catch (e) {
        // instance not ready yet
    }
    requestAnimationFrame(tickJS);
}

function initRenderJS(dotnetHelper) {
    window.theInstance = dotnetHelper;
    resizeCanvas();
    requestAnimationFrame(tickJS);
}

window.addEventListener('resize', resizeCanvas);
