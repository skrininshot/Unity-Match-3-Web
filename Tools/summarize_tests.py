#!/usr/bin/env python3
"""Print a compact summary of an NUnit3 result XML produced by the Unity Test Runner."""
import sys
import xml.etree.ElementTree as ET


def main(path: str) -> int:
    root = ET.parse(path).getroot()
    total = root.get("total", "?")
    passed = root.get("passed", "?")
    failed = root.get("failed", "?")
    skipped = root.get("skipped", "?")
    duration = root.get("duration", "?")
    print(f"tests: total={total} passed={passed} failed={failed} skipped={skipped} duration={duration}s")

    bad = 0
    for case in root.iter("test-case"):
        if case.get("result") in ("Failed", "Error"):
            bad += 1
            print(f"\nFAILED: {case.get('fullname')}")
            failure = case.find("failure")
            if failure is not None:
                for tag in ("message", "stack-trace"):
                    node = failure.find(tag)
                    if node is not None and node.text:
                        text = node.text.strip()
                        limit = 1200 if tag == "message" else 900
                        print(f"  {tag}: {text[:limit]}")
            if bad >= 25:
                print("\n... more failures truncated ...")
                break
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
