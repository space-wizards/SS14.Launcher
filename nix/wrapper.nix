{ soundfont-fluid
, buildFHSEnv
, runCommand
, callPackage
}:

let
  launcher = callPackage ./package.nix { };

  # Workaround for hardcoded soundfont paths in downloaded engine assemblies.
  soundfont-fluid-fixed = runCommand "soundfont-fluid-fixed" { } ''
    mkdir -p "$out/share/soundfonts"
    ln -sf ${soundfont-fluid}/share/soundfonts/FluidR3_GM2-2.sf2 $out/share/soundfonts/FluidR3_GM.sf2
  '';
in
buildFHSEnv rec {
  name = "${launcher.pname}-wrapped-${launcher.version}";

  targetPkgs = pkgs: [
    launcher
    soundfont-fluid-fixed
  ];

  runScript = "SS14.Launcher";

  extraInstallCommands = ''
    mkdir -p $out/share/applications
    ln -s ${launcher}/share/icons $out/share
    cp ${launcher}/share/applications/space-station-14-launcher.desktop "$out/share/applications"
    substituteInPlace "$out/share/applications/space-station-14-launcher.desktop" \
        --replace ${launcher.meta.mainProgram} ${meta.mainProgram}
  '';

  passthru = launcher.passthru // {
    unwrapped = launcher;
  };
  meta = launcher.meta // {
    mainProgram = name;
  };
}
