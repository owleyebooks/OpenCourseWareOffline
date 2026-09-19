#!/usr/bin/env python3
"""Drive sync wrapper — shared implementation lives in ~/workspace/muse-ops.
Do not fork this file; edit notes/drive_sync.json instead."""
import os
import sys

sys.path.insert(0, os.path.expanduser("~/workspace/muse-ops"))
from drive_sync import main  # noqa: E402

main(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                  "drive_sync.json"))
