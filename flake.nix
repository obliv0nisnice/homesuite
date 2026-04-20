{
  description = "HomeSuite development shell";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs = { self, nixpkgs }:
    let
      systems = [ "x86_64-linux" "aarch64-linux" ];
      forAllSystems = f: nixpkgs.lib.genAttrs systems (system: f system);
    in
    {
      devShells = forAllSystems (system:
        let
          pkgs = import nixpkgs { inherit system; };
        in
        {
          default = pkgs.mkShell {
            packages = [
              pkgs.nodejs
              pkgs.playwright-driver
            ];

            PLAYWRIGHT_BROWSERS_PATH = "${pkgs.playwright-driver.browsers}";
            PLAYWRIGHT_CORE_PATH = "${pkgs.playwright-driver}/index.mjs";
            PLAYWRIGHT_SKIP_VALIDATE_HOST_REQUIREMENTS = "true";
            LD_LIBRARY_PATH = pkgs.lib.makeLibraryPath [
              pkgs.glib
              pkgs.nspr
              pkgs.nss
              pkgs.atk
              pkgs.at-spi2-atk
              pkgs.dbus
              pkgs.cups
              pkgs.expat
              pkgs.libxcb
              pkgs.libxkbcommon
              pkgs.alsa-lib
              pkgs.mesa
              pkgs.libx11
              pkgs.libxext
              pkgs.libxcomposite
              pkgs.libxdamage
              pkgs.libxfixes
              pkgs.libxrandr
              pkgs.cairo
              pkgs.pango
              pkgs.systemd
            ];

            shellHook = ''
              echo "HomeSuite dev shell ready."
              echo "Playwright browsers path: $PLAYWRIGHT_BROWSERS_PATH"
              echo "Playwright core path: $PLAYWRIGHT_CORE_PATH"
              echo "Test command:"
              echo "  node scripts/test-playwright-spar.mjs"
            '';
          };
        });
    };
}
