param($OutDir)

$sampleRate = 44100

function Write-Wav($path, $channels, $seconds, $script:generate) {
    $bitsPerSample = 16
    $blockAlign = $channels * ($bitsPerSample / 8)
    $byteRate = $sampleRate * $channels * ($bitsPerSample / 8)
    $numSamples = [int]($sampleRate * $seconds)
    $dataSize = $numSamples * $blockAlign
    $fileSize = 36 + $dataSize

    $fs = [System.IO.File]::Create($path)
    $w = New-Object System.IO.BinaryWriter($fs)
    $w.Write([System.Text.Encoding]::ASCII.GetBytes("RIFF"))
    $w.Write($fileSize)
    $w.Write([System.Text.Encoding]::ASCII.GetBytes("WAVE"))
    $w.Write([System.Text.Encoding]::ASCII.GetBytes("fmt "))
    $w.Write(16)
    $w.Write([int16]1)
    $w.Write([int16]$channels)
    $w.Write([int32]$sampleRate)
    $w.Write([int32]$byteRate)
    $w.Write([int16]$blockAlign)
    $w.Write([int16]$bitsPerSample)
    $w.Write([System.Text.Encoding]::ASCII.GetBytes("data"))
    $w.Write([int32]$dataSize)

    $twoPi = 2 * [Math]::PI
    for ($i = 0; $i -lt $numSamples; $i++) {
        $t = $i / $sampleRate
        $sample = &$generate $t $i
        # mono -> duplicate to channels
        for ($c = 0; $c -lt $channels; $c++) {
            $w.Write([int16]$sample)
        }
    }
    $w.Close(); $fs.Close()
}

# ---- Helpers ----
function freq($note) {
    # A4 = 440; semitone ratio
    $semitones = $note - 69
    return 440.0 * [Math]::Pow(2.0, $semitones / 12.0)
}

function adsr($t, $attack, $decay, $sustain, $release, $dur) {
    if ($t -lt $attack) { return $t / $attack }
    if ($t -lt $attack + $decay) { return 1 - (($t - $attack) / $decay) * (1 - $sustain) }
    if ($t -lt $dur - $release) { return $sustain }
    if ($t -lt $dur) { return $sustain * (1 - ($t - ($dur - $release)) / $release) }
    return 0
}

function clip($v) {
    if ($v -gt 1) { return 1 }
    if ($v -lt -1) { return -1 }
    return $v
}

# ---- Overworld: cheerful C-major arpeggio loop (8s) ----
Write-Wav "$OutDir\overworld.wav" 1 8 param($t, $i) {
    $beat = 0.25
    $phase = ($t % 2.0) / 2.0  # 2-bar loop
    # Chord progression: C F Am G
    $bar = [int]($phase * 4)
    $chord = @(@(60,64,67), @(65,69,72), @(69,73,76), @(67,71,74))[$bar]
    $posInBar = $phase * 4 - $bar
    $noteIdx = [int]($posInBar * 6) % 3  # arpeggiate 3 notes
    $f = freq($chord[$noteIdx])
    $env = adsr($t % $beat, 0.01, 0.05, 0.6, 0.04, $beat)
    $s = [Math]::Sin($twoPi * $f * $t) * 0.4
    $s += [Math]::Sin($twoPi * $f * 2 * $t) * 0.15
    $s += [Math]::Sin($twoPi * $f * 0.5 * $t) * 0.1
    return clip($s * $env * 0.5)
}

# ---- Battle: urgent E-minor loop (8s) ----
Write-Wav "$OutDir\battle.wav" 1 8 param($t, $i) {
    $beat = 0.2
    $phase = ($t % 2.0) / 2.0
    $bar = [int]($phase * 4)
    $chord = @(@(64,68,71), @(66,70,73), @(68,72,75), @(66,70,73))[$bar]
    $posInBar = $phase * 4 - $bar
    $noteIdx = [int]($posInBar * 8) % 3
    $f = freq($chord[$noteIdx])
    $env = adsr($t % $beat, 0.005, 0.03, 0.5, 0.03, $beat)
    $s = [Math]::Sin($twoPi * $f * $t) * 0.45
    $s += [Math]::Sin($twoPi * $f * 2 * $t) * 0.2
    $s += [Math]::Sin($twoPi * $f * 3 * $t) * 0.05
    # driving eighth-note pulse
    $pulse = if (($t * 10) % 1 -lt 0.5) { 0.2 } else { 0 }
    return clip(($s * $env + $pulse) * 0.5)
}

# ---- Menu: calm G-major loop (10s) ----
Write-Wav "$OutDir\menu.wav" 1 10 param($t, $i) {
    $beat = 0.5
    $phase = ($t % 4.0) / 4.0
    $bar = [int]($phase * 2)
    $chord = @(@(67,71,74), @(69,73,76))[$bar]
    $posInBar = $phase * 2 - $bar
    $noteIdx = [int]($posInBar * 4) % 3
    $f = freq($chord[$noteIdx])
    $env = adsr($t % $beat, 0.05, 0.1, 0.7, 0.15, $beat)
    $s = [Math]::Sin($twoPi * $f * $t) * 0.3
    $s += [Math]::Sin($twoPi * $f * 2 * $t) * 0.1
    return clip($s * $env * 0.5)
}

# ---- SFX: jump (rising) ----
Write-Wav "$OutDir\jump.wav" 1 0.6 param($t, $i) {
    $dur = 0.6
    if ($t -gt $dur) { return 0 }
    $f = 220 + $t * 400
    $env = adsr($t, 0.01, 0.05, 0.6, 0.3, $dur)
    return clip([Math]::Sin($twoPi * $f * $t) * $env * 0.6)
}

# ---- SFX: hit (noise burst) ----
Write-Wav "$OutDir\hit.wav" 1 0.4 param($t, $i) {
    $dur = 0.4
    if ($t -gt $dur) { return 0 }
    $r = Get-Random -Minimum -1 -Maximum 1
    $env = adsr($t, 0.005, 0.05, 0.3, 0.2, $dur)
    return clip($r * $env * 0.7)
}

# ---- SFX: select (short blip) ----
Write-Wav "$OutDir\select.wav" 1 0.2 param($t, $i) {
    $dur = 0.2
    if ($t -gt $dur) { return 0 }
    $f = 880
    $env = adsr($t, 0.005, 0.02, 0.5, 0.1, $dur)
    return clip([Math]::Sin($twoPi * $f * $t) * $env * 0.5)
}

Write-Host "Generated audio in $OutDir"
Get-ChildItem $OutDir | Select-Object Name, Length
