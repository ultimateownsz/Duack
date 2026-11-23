# 2D spatial SFX — status

Proven by tests (`tests/Duack.Tests/CodeOnly/SpatialMathTests.cs`, `SpatialConfigTests.cs`,
`SfxSpatialTests.cs`): the pan law, distance models (linear / inverse / exponential), the mute
floor at max distance, clamp-saturation behavior, distance-gain composition with the volume
chain, and `SfxManager` positional play / re-mix routing through `IPositionalSfxBackend`.

Awaiting human listening: whether the default pan saturation (0.7 at the pan distance,
sigma 0.5) and distance rolloff defaults actually feel right on real content. Curve parameters
are debrief-tunable per SHARED §12; nothing here is validated by ear yet.
