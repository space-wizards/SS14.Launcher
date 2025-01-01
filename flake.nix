{
  description = "Flake providing a package for the Space Station 14 Launcher.";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/release-24.11";
  inputs.flake-compat = {
    url = "github:edolstra/flake-compat";
    flake = false;
  };


  outputs = { self, nixpkgs, ... }:
    let
      forAllSystems = function:
        nixpkgs.lib.genAttrs [ "x86_64-linux" ] # TODO: aarch64-linux support
          (system: function (import nixpkgs { inherit system; }));
    in
    {

      packages = forAllSystems (pkgs: {
        default = self.packages.${pkgs.system}.space-station-14-launcher;
        space-station-14-launcher =
          pkgs.callPackage ./nix/package.nix { };
      });

      overlays = {
        default = self.overlays.space-station-14-launcher;
        space-station-14-launcher = final: prev: {
          space-station-14-launcher =
            self.packages.${prev.system}.space-station-14-launcher;
        };
      };

      apps = forAllSystems (pkgs:
        let pkg = self.packages.${pkgs.system}.space-station-14-launcher; in {
          default = self.apps.${pkgs.system}.space-station-14-launcher;
          space-station-14-launcher = {
            type = "app";
            program = "${pkg}/bin/${pkg.meta.mainProgram}";
          };
          fetch-deps = {
            type = "app";
            program = toString
              self.packages.${pkgs.system}.space-station-14-launcher.passthru.fetch-deps;
          };
        });

      formatter = forAllSystems (pkgs: pkgs.nixpkgs-fmt);

    };
}
