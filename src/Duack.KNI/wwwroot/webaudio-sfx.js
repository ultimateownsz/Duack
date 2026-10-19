// Duack WebAudio SFX backend — plays one-shot cues over the music track using the
// browser WebAudio API. Attached to globalThis for synchronous [JSImport] calls from C#.

(function () {
    let audioContext = null;
    const buffers = new Map();          // url -> AudioBuffer
    const playingSources = [];          // { source, gain }

    function initAudioContext() {
        if (audioContext) return;
        const Ctx = window.AudioContext || window.webkitAudioContext;
        if (!Ctx) throw new Error('WebAudio not supported');
        audioContext = new Ctx();
        // iOS Safari hands back a suspended context even inside a click;
        // without this resume every sound would queue silently.
        if (audioContext.state === 'suspended') {
            audioContext.resume();
        }
    }

    function hasBuffer(url) {
        return buffers.has(url);
    }

    // Fire-and-forget; failures logged so one bad file cannot block the rest.
    function preloadBuffers(urls) {
        initAudioContext();
        for (const url of urls) {
            if (!buffers.has(url)) {
                fetch(url)
                    .then(function (response) {
                        if (!response.ok) throw new Error('Fetch failed: ' + url + ' (' + response.status + ')');
                        return response.arrayBuffer();
                    })
                    .then(function (arrayBuffer) {
                        return audioContext.decodeAudioData(arrayBuffer);
                    })
                    .then(function (buffer) {
                        buffers.set(url, buffer);
                    })
                    .catch(function (e) {
                        console.error('[Duack WebAudio] Failed to load', url, e);
                    });
            }
        }
    }

    function playSource(bufferUrl, pitch, volume) {
        initAudioContext();

        const buffer = buffers.get(bufferUrl);
        if (!buffer) throw new Error('Buffer not loaded: ' + bufferUrl);

        const source = audioContext.createBufferSource();
        source.buffer = buffer;
        // Convert XNA pitch units (-1..1) to WebAudio playbackRate (2^pitch).
        source.playbackRate.setValueAtTime(Math.pow(2, pitch), audioContext.currentTime);

        const gain = audioContext.createGain();
        // 3 ms ramp kills the onset click an instant jump would cause.
        const now = audioContext.currentTime;
        gain.gain.setValueAtTime(0, now);
        gain.gain.linearRampToValueAtTime(volume, now + 0.003);

        source.connect(gain);
        gain.connect(audioContext.destination);
        source.start();

        const handle = { source: source, gain: gain };
        playingSources.push(handle);

        source.onended = function () {
            const idx = playingSources.indexOf(handle);
            if (idx >= 0) playingSources.splice(idx, 1);
            try { gain.disconnect(); } catch (e) {}
            try { source.disconnect(); } catch (e) {}
        };
    }

    function stopAll() {
        for (const handle of playingSources) {
            try { handle.source.stop(); } catch (e) {}
            try { handle.gain.disconnect(); } catch (e) {}
            try { handle.source.disconnect(); } catch (e) {}
        }
        playingSources.length = 0;
    }

    window.DuackWebAudio = {
        initAudioContext,
        hasBuffer,
        preloadBuffers,
        playSource,
        stopAll,
    };
})();
