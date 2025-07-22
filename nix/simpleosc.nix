{lib, stdenv, fetchFromGitHub, rustPlatform}:
rustPlatform.buildRustPackage rec {
  name = "SimpleRustOSC";

  src = fetchFromGitHub {
    owner = "benaclejames";
    repo = "SimpleRustOSC";
    rev = "9163766ebdd33d1b6f2aa413fc645e478e96956f";
    hash = "sha256-zywSRT/o47uk/tLLZxS+G44bDzq48TlWhkqeyFNVUQo=";
  };
  cargoLock.lockFile = ./Cargo.lock;
  postPatch = ''
    ln -s ${./Cargo.lock} Cargo.lock
  '';

  meta = with lib; {
    platforms = platforms.unix;
    license = licenses.unlicense;
    homepage = "https://github.com/benaclejames/SimpleRustOSC";
    description = "A simple one-file OSC library with C exports made in Rust.";
  };
}
