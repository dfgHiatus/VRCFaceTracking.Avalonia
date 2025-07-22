build:
	sudo nix build .#vrchatfacetracking --option sandbox false
clean:
	sudo rm -rf result
	sudo rm -rf .direnv
fetch-deps:
	nix build .#vrchatfacetracking.fetch-deps
appimage:
	sudo nix bundle --bundler github:ralismark/nix-appimage .#vrchatfacetracking --option sandbox false
