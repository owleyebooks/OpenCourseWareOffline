#!/bin/bash
# Dismisses any showing "isn't responding" ANR dialog by tapping its Wait button.
# Idempotent: exits quietly (0) when no dialog is present. Safe to call before
# every screenshot since the dialog can appear at any moment on a loaded emulator.
set -u
XML="${1:-diag/anr.xml}"
adb shell uiautomator dump /sdcard/anr.xml >/dev/null 2>&1 || exit 0
adb pull /sdcard/anr.xml "$XML" >/dev/null 2>&1 || exit 0
TAP=$(python3 "$(dirname "$0")/find_wait.py" "$XML" 2>/dev/null) || true
if [ -n "$TAP" ]; then
  adb shell input tap $TAP >/dev/null 2>&1 || true
  sleep 3
fi
exit 0
