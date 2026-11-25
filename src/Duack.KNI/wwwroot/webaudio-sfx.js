// Duack WebAudio SFX backend — one-shot cues over the WebAudio API, independent of
// MediaPlayer so SFX overlap the music track while the core dips the music volume.
// Attached to globalThis for synchronous [JSImport] calls.
//
// Voice bookkeeping: every play is tracked by id (source -> gain -> panner -> sfx bus),
// so a single voice can be stopped (stealing) or re-mixed (positional) later; the id map
// is cleaned on natural end, stopSource and stopAll. All volumes/pans arrive composed
// and clamped from the core; this bridge only applies the numbers.

(function () {
    let audioContext = null;
    let nextSourceId = 1;
    const buffers = new Map();          // url -> AudioBuffer
    const sourcesById = new Map();      // id -> { source, gain, panner } tracked voices
    const busNodes = new Map();         // bus name -> persistent GainNode of the playing chain
    let masterNode = null;
    let sfxNode = null;

    function initAudioContext() {
        if (audioContext) return;
        const Ctx = window.AudioContext || window.webkitAudioContext;
        if (!Ctx) throw new Error('WebAudio not supported');
        audioContext = new Ctx();
        // iOS Safari hands back a suspended context even inside a click; without this
        // resume every sound would queue up silently.
        if (audioContext.state === 'suspended') {
            audioContext.resume();
        }
        // One persistent gain node per bus of the playing chain: every play's per-source
        // gain routes through the sfx node into the master node, then the destination, so
        // live bus volume changes move the currently playing voices.
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

    // Fire-and-forget; failures are logged so one bad file cannot block the rest.
    function preloadBuffers(urls) {
        initAudioContext();
        for (const url of urls) {
            if (buffers.has(url)) continue;
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

    function detach(handle) {
        try { if (handle.panner) handle.panner.disconnect(); } catch (e) {}
        try { handle.gain.disconnect(); } catch (e) {}
        try { handle.source.disconnect(); } catch (e) {}
    }

    function playSourceTrackedAt(bufferUrl, pan, pitch, volume) {
        initAudioContext();
        const buffer = buffers.get(bufferUrl);
        if (!buffer) throw new Error('Buffer not loaded: ' + bufferUrl);

        const source = audioContext.createBufferSource();
        source.buffer = buffer;
        // XNA pitch units (-1..1) to WebAudio playbackRate (2^pitch).
        source.playbackRate.value = Math.pow(2, pitch);

        // Per-source gain carries the fully composed volume; a 3 ms ramp kills the click
        // an instant jump would cause (the same ramp every later re-mix keeps using).
        const gain = audioContext.createGain();
        const now = audioContext.currentTime;
        gain.gain.setValueAtTime(0, now);
        gain.gain.linearRampToValueAtTime(volume, now + 0.003);

        // Panned voices get one StereoPannerNode (constant-power law); pan 0 plays plain
        // mono-centered through the gain only, so the un-panned path has no extra node.
        let panner = null;
        if (pan !== 0) {
            panner = audioContext.createStereoPanner();
            panner.pan.value = pan;
            source.connect(gain);
            gain.connect(panner);
            panner.connect(sfxNode);
        } else {
            source.connect(gain);
            gain.connect(sfxNode);
        }

        source.start();

        const handle = { source, gain, panner };
        const id = nextSourceId++;
        // Capture the id now: the onended closure must delete THIS source's mapping, not
        // whatever id the counter holds when the source happens to end.
        sourcesById.set(id, handle);

        source.onended = function () {
            sourcesById.delete(id);
            detach(handle);
        };

        return id;
    }

    function playSource(bufferUrl, pitch, volume) {
        playSourceTrackedAt(bufferUrl, 0, pitch, volume);
    }

    function playSourceTracked(bufferUrl, pitch, volume) {
        return playSourceTrackedAt(bufferUrl, 0, pitch, volume);
    }

    // Re-mixes one tracked voice: volume re-ramped, pan moved (panner created on demand
    // for a voice that started un-panned). Unknown or finished ids are no-ops.
    function setVoiceMix(id, volume, pan) {
        const handle = sourcesById.get(id);
        if (!handle) return;

        const now = audioContext.currentTime;
        handle.gain.gain.cancelScheduledValues(now);
        handle.gain.gain.setValueAtTime(handle.gain.gain.value, now);
        handle.gain.gain.linearRampToValueAtTime(volume, now + 0.003);

        const clampedPan = Math.max(-1, Math.min(1, pan));
        if (handle.panner) {
            handle.panner.pan.cancelScheduledValues(now);
            handle.panner.pan.setValueAtTime(handle.panner.pan.value, now);
            handle.panner.pan.linearRampToValueAtTime(clampedPan, now + 0.003);
        } else if (clampedPan !== 0) {
            // This voice started centered; give it a panner mid-flight.
            handle.panner = audioContext.createStereoPanner();
            handle.panner.pan.value = clampedPan;
            handle.gain.disconnect();
            handle.gain.connect(handle.panner);
            handle.panner.connect(sfxNode);
        }
    }

    function stopSource(id) {
        const handle = sourcesById.get(id);
        if (!handle) return;
        sourcesById.delete(id);
        try { handle.source.stop(); } catch (e) {}
        detach(handle);
    }

    // Sets the live volume of a bus's persistent gain node (clamped [0, 1], 3 ms ramp so a
    // slider drag does not zipper). Unknown names are created as leaf nodes under master on
    // demand so a head that drives them has the node ready.
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

    // Warms the browser HTTP cache for URLs other elements fetch by URL (the platform
    // <audio> music element): plain fetches, no decode. Fire-and-forget, per-URL errors
    // logged so one bad file cannot block the rest.
    function warmHttpCache(urls) {
        for (const url of urls) {
            fetch(url).catch(function (e) {
                console.error('[Duack WebAudio] Could not warm', url, e);
            });
        }
    }

    function stopAll() {
        // Iterate over a copy: stop() fires onended handlers synchronously, which would
        // mutate the map being iterated.
        const ids = [...sourcesById.keys()];
        for (const id of ids) {
            const handle = sourcesById.get(id);
            sourcesById.delete(id);
            try { handle.source.stop(); } catch (e) {}
            detach(handle);
        }
    }

    function releaseAllBuffers() {
        buffers.clear();
    }

    window.DuackWebAudio = {
        initAudioContext,
        hasBuffer,
        preloadBuffers,
        playSource,
        playSourceTracked,
        playSourceTrackedAt,
        setVoiceMix,
        stopSource,
        stopAll,
        setBusVolume,
        warmHttpCache,
        releaseAllBuffers,
    };
})();
