{ lib
, buildDotnetModule
, dotnetCorePackages
, fetchFromGitHub
, wrapGAppsHook
, iconConvTools
, copyDesktopItems
, makeDesktopItem
, libX11
, libICE
, libSM
, libXi
, libXcursor
, libXext
, libXrandr
, fontconfig
, glew
, SDL2
, glfw
, glibc
, libGL
, freetype
, openal
, fluidsynth
, gtk3
, pango
, atk
, cairo
, zlib
, glib
, gdk-pixbuf
, soundfont-fluid

# Path to set ROBUST_SOUNDFONT_OVERRIDE to, essentially the default soundfont used.
, soundfont-path ? "${soundfont-fluid}/share/soundfonts/FluidR3_GM2-2.sf2"

}:
let
  version = "0.24.0";
  pname = "space-station-14-launcher";
in
buildDotnetModule rec {
  inherit pname;

  # Workaround to prevent buildDotnetModule from overriding assembly versions.
  name = "${pname}-${version}";

  # A bit redundant but I don't trust this package to be maintained by anyone else.
  src = fetchFromGitHub {
    owner = "space-wizards";
    repo = "SS14.Launcher";
    rev = "v${version}";
    hash = "sha256-n0OiNxw9QDibX5HBSzq6jdOxyUd0bPkjKd+mtb/S/BY=";
    fetchSubmodules = true;
  };

  buildType = "Release";
  selfContainedBuild = false;

  projectFile = [
    "SS14.Loader/SS14.Loader.csproj"
    "SS14.Launcher/SS14.Launcher.csproj"
  ];

  nugetDeps = ./deps.nix;

  passthru = {
    inherit version;
  };

  dotnet-sdk = with dotnetCorePackages; combinePackages [ sdk_7_0 sdk_6_0 ];
  dotnet-runtime = dotnetCorePackages.runtime_7_0;

  dotnetFlags = [
    "-p:FullRelease=true"
    "-p:RobustILLink=true"
    "-nologo"
  ];

  nativeBuildInputs = [ wrapGAppsHook iconConvTools copyDesktopItems ];

  runtimeDeps = [
    # Required by the game.
    glfw
    SDL2
    glibc
    libGL
    openal
    freetype
    fluidsynth

    # Needed for file dialogs.
    gtk3
    pango
    cairo
    atk
    zlib
    glib
    gdk-pixbuf

    # Avalonia UI dependencies.
    libX11
    libICE
    libSM
    libXi
    libXcursor
    libXext
    libXrandr
    fontconfig
    glew

    # TODO: Figure out dependencies for CEF support.
  ];

  makeWrapperArgs = [ ''--set ROBUST_SOUNDFONT_OVERRIDE "${soundfont-path}"'' ];

  executables = [ "SS14.Launcher" ];

  desktopItems = [
    (makeDesktopItem {
      name = pname;
      exec = meta.mainProgram;
      icon = pname;
      desktopName = "Space Station 14 Launcher";
      comment = meta.description;
      categories = [ "Game" ];
      startupWMClass = meta.mainProgram;
    })
  ];

  postInstall = ''
    mkdir -p $out/lib/space-station-14-launcher/loader
    cp -r SS14.Loader/bin/${buildType}/net7.0/* $out/lib/space-station-14-launcher/loader/

    icoFileToHiColorTheme SS14.Launcher/Assets/icon.ico space-station-14-launcher $out
  '';

  dontWrapGApps = true;

  preFixup = ''
    makeWrapperArgs+=("''${gappsWrapperArgs[@]}")
  '';

  meta = with lib; {
    description = "Launcher for Space Station 14, a multiplayer game about paranoia and disaster";
    homepage = "https://spacestation14.io";
    license = licenses.mit;
    maintainers = [ maintainers.zumorica ];
    platforms = [ "x86_64-linux" ];
    mainProgram = "SS14.Launcher";
  };
}
