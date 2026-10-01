# Royal Buddies — GitHub / WebGL

Prepared from `RoyalBuddies_V8_1_Cartes_Gobelin_Tactile` without gameplay/map changes.

## GitHub
Commit the project root (`Assets`, `Packages`, `ProjectSettings`, `.gitignore`). Unity-generated folders are ignored. `SceneBackup`, `ModelSource`, and stray root `InitTestScene*` files were excluded from this lightweight copy.

## WebGL build
Required editor: Unity `6000.6.3f1` with the WebGL Build Support module installed.

1. Open this project in Unity.
2. Use **Royal Buddies → Build → WebGL Production**.
3. Output is created in `Builds/WebGL`.
4. Test through a local HTTP server, not by double-clicking `index.html`.

WebGL is configured for Gzip compression with decompression fallback for better compatibility with static hosting such as GitHub Pages.

## GitHub Pages
After building, publish the *contents* of `Builds/WebGL` to the Pages site (root of the published branch/artifact). If your host supports correct `Content-Encoding: gzip` headers, you can later disable Unity's decompression fallback for a slightly leaner/faster startup.

## Verification status
This environment did not contain the Unity Editor executable, so the WebGL player itself was **not compiled here**. The project settings and one-click build script were prepared, but the final build must be run once in Unity.
