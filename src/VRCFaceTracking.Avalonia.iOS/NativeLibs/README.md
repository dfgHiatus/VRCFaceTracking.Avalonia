# fti_osc for iOS

The iOS app links `fti_osc` statically from `fti_osc.xcframework` in this folder. Until it exists the
project still builds, with a warning, but OSC fails at runtime.

Build it from `src/SimpleRustOSC` at the same revision as the other platforms' binaries. Upstream
VRCFaceTracking's `OscValue` includes the blob fields added in SimpleRustOSC `34bcb69`, and older builds
have a different struct layout. On a Mac with Xcode:

```sh
cd src/SimpleRustOSC
rustup target add aarch64-apple-ios aarch64-apple-ios-sim
cargo rustc --release --lib --target aarch64-apple-ios     --crate-type staticlib
cargo rustc --release --lib --target aarch64-apple-ios-sim --crate-type staticlib
xcodebuild -create-xcframework \
  -library target/aarch64-apple-ios/release/libfti_osc.a \
  -library target/aarch64-apple-ios-sim/release/libfti_osc.a \
  -output ../VRCFaceTracking.Avalonia.iOS/NativeLibs/fti_osc.xcframework
```

`FtiOscResolver.cs` points Core's `[DllImport("fti_osc")]` at the main executable, and the
`ReferenceNativeSymbol` items in the csproj keep the exported functions from being stripped.
