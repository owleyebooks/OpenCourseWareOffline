"""Prints the center tap coordinates of the Wait button in an ANR dialog dump.

Usage: find_wait.py <uiautomator-xml>
Prints "<x> <y>" when a node with text "Wait" is found, nothing otherwise.
"""
import sys
import xml.etree.ElementTree as ET

try:
    tree = ET.parse(sys.argv[1])
except Exception:
    sys.exit(0)

for node in tree.iter("node"):
    if node.get("text") == "Wait":
        try:
            bounds = [int(v) for v in node.get("bounds").replace("[", "").replace("]", ",").strip(",").split(",")]
        except (ValueError, AttributeError):
            break
        print(f"{(bounds[0] + bounds[2]) // 2} {(bounds[1] + bounds[3]) // 2}")
        break
