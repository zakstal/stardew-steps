#!/usr/bin/env bash
# Builds everything Zak copies to Fedora / the phone into dist/.
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1
if [ -z "${JAVA_HOME:-}" ] && command -v brew >/dev/null; then
  export JAVA_HOME="$(brew --prefix openjdk@17)/libexec/openjdk.jdk/Contents/Home"
fi

rm -rf dist && mkdir -p dist

# 1. SMAPI mod (Release build has no self-test code)
dotnet build mod/StardewSteps -c Release -p:EnableModDeploy=false -nologo -v q
cp "mod/StardewSteps/bin/Release/net6.0/StardewSteps 0.1.0.zip" dist/StardewSteps-mod.zip

# 2. Bridge
mkdir -p dist/stardew-steps-bridge
cp -R bridge/stardew_steps_bridge bridge/stardew-steps-bridge.service dist/stardew-steps-bridge/
find dist/stardew-steps-bridge -name __pycache__ -prune -exec rm -rf {} +

# 3. Phone app
(cd android && ./gradlew -q assembleDebug)
cp android/app/build/outputs/apk/debug/app-debug.apk dist/stardew-steps.apk

ls -la dist
