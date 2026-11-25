// Duack WebAudio SFX backend — plays one-shot cues over the music track using the
// browser WebAudio API. Attached to globalThis for synchronous [JSImport] calls from C#.

(function () {
    let audioContext = null;
    const buffers = new Map();          // url -> AudioBuffer
    const playingSources = [];          // { source, gain }
    const busNodes = new Map();         // bus name -> persistent GainNode of the playing chain
    let masterNode = null;
    let sfxNode = null;

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
        // One persistent gain node per bus of the playing chain: the fixed sfx node every
        // play's per-source gain routes through, into the master node, then the destination.
        // Bus volume updates move these nodes, so currently playing voices move with them.
        masterNode = audioContext.createGain();
        sfxNode = audioContext.createGain();
        sfxNode.connect(masterNode);
        masterNode.connect(audioContext.destination);
        busNodes.set('sfx', sfxNode);
        busNodes.set('master', masterNode);
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
        // Route through the fixed sfx bus node into master (created on init); the per-source
        // gain keeps the fully composed per-play volume the core handed the backend.
        gain.connect(sfxNode);
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

    // Sets a bus node's live volume (clamped [0, 1]); the 3 ms ramp matches the onset ramp so
    // a slider drag does not zipper. Unknown bus names are created as leaf nodes under master
    // on demand, so a head that drives them has the node ready; nothing routes into them until
    // the core seam carries the resolved bus per play.
    function setBusVolume(bus, volume) {
        initAudioContext();
        let node = busNodes.get(bus);
        if (!node) {
            node = audioContext.createGain();
            node.connect(masterNode);
            busNodes.set(bus, node);
        }
        const clamped = Math.max(0, Math.min(1, volume));
        const now = audioContext.currentTime;
        node.gain.cancelScheduledValues(now);
        node.gain.setValueAtTime(node.gain.value, now);
        node.gain.linearRampToValueAtTime(clamped, now + 0.003);
    }

    function stopAll() {
        // Iterate over a copy so source.stop() triggering onended (which splices
        // the live array) cannot skip elements.
        const sources = [...playingSources];
        for (const handle of sources) {
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
        setBusVolume,
        stopAll,
    };
})();
