# Flyleaf playback smoke test

This Windows-only tool exercises CryptoBook's real `MediaPlayerService` with a
WPF dispatcher. It verifies that the packaged FFmpeg libraries load, the input
can be demuxed and decoded, and playback time advances.

Run it with a short local media file:

```powershell
dotnet run --project tools/FlyleafPlaybackSmoke -c Release -- path\to\sample.mp4
```

The tool returns `0` and prints `FLYLEAF_PLAYBACK_SMOKE: PASS` only after two
seconds of successful playback. It does not display a window or modify the
input file.

CI can validate the Flyleaf/MaterialDesign WPF template contract without a
media file:

```powershell
dotnet run --project tools/FlyleafPlaybackSmoke -c Release -- --template-only
```
