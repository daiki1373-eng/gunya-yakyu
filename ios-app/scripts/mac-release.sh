#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ "$(uname -s)" != Darwin ]]; then echo 'Run this script on a Mac with Xcode.' >&2; exit 1; fi
command -v node >/dev/null || { echo 'Install Node.js 22 or later.' >&2; exit 1; }
command -v npm >/dev/null || { echo 'npm is required.' >&2; exit 1; }
command -v pod >/dev/null || { echo 'Install CocoaPods, then retry.' >&2; exit 1; }
XCODE_MAJOR=$(xcodebuild -version | awk '/Xcode/{split($2,v,".");print v[1]}')
if [[ "$XCODE_MAJOR" -lt 26 ]]; then echo 'Xcode 26 or later is required.' >&2; exit 1; fi
npm ci
npm test
npm run build
npx cap sync ios
if [[ "${1:-}" == "--simulator" ]]; then
  xcodebuild -workspace ios/App/App.xcworkspace -scheme App -configuration Release -sdk iphonesimulator -destination 'generic/platform=iOS Simulator' -derivedDataPath build/simulator CODE_SIGNING_ALLOWED=NO build
  echo 'Simulator build completed: build/simulator/Build/Products/Release-iphonesimulator/App.app'
  exit 0
fi
if [[ -z "${APPLE_TEAM_ID:-}" ]]; then
  echo 'Simulator build is available with --simulator. For a signed archive, run APPLE_TEAM_ID=YOUR_TEAM_ID bash scripts/mac-release.sh' >&2
  open ios/App/App.xcworkspace
  exit 2
fi
mkdir -p build
xcodebuild -workspace ios/App/App.xcworkspace -scheme App -configuration Release -destination 'generic/platform=iOS' -archivePath "$PWD/build/GunyaYakyu.xcarchive" -allowProvisioningUpdates DEVELOPMENT_TEAM="$APPLE_TEAM_ID" archive
open build/GunyaYakyu.xcarchive
echo 'Archive ready. In Xcode Organizer: Distribute App > App Store Connect > Upload. Test the uploaded build in TestFlight before submitting for review.'
