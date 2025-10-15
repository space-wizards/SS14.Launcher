# How to update

1. Setup `nix`

2. Change the version and hash in `nix/package.nix`

3. In the root of the project call the `nix run .#fetch-deps nix/deps.json`

4. Test it out with `nix build .`

5. Profit

6. ???
