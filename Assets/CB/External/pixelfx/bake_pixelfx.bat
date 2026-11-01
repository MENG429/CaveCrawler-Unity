@echo off
REM ===========================================================================
REM NeonFault - pixelfx bake helper (Windows). Thin wrapper around bake.mjs.
REM Clones patch-gg/pixelfx (MIT) outside the Unity project if missing, then
REM bakes the 6 spike VFX with the canonical neon palettes into:
REM   Assets/CB/External/pixelfx/Resources/<cat>_<name>.png + .json
REM Requires git + node v18+. Run once on the dev machine, then let Unity import.
REM ===========================================================================
setlocal
set PIXELFX_REPO=D:/unity111/pixelfx_tmp

if not exist "%PIXELFX_REPO%\tools\lib\fxlib.mjs" (
  git -c http.sslVerify=false clone --depth 1 https://github.com/patch-gg/pixelfx "%PIXELFX_REPO%"
)

pushd "%~dp0"
node "%~dp0bake.mjs"
popd
endlocal
