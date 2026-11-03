#!/usr/bin/env python3
"""Prevent accidental publication of internal infrastructure identifiers.

This generic guard deliberately contains no real internal names or domains.
Code review must additionally check for organization-specific public domains,
person names, screenshots, certificate names and instance-specific addresses.
"""
from __future__ import annotations

import pathlib
import re
import subprocess
import sys
from urllib.parse import urlsplit

ROOT = pathlib.Path(__file__).resolve().parents[1]
EXCLUDED = {".svg", ".png", ".jpg", ".jpeg", ".gif", ".ico", ".woff", ".woff2", ".pdf", ".lock"}
SUSPICIOUS = [
    ("private DNS suffix", re.compile(
        rb"(?i)\b(?:[a-z0-9][a-z0-9_-]*\.)+(?:local|lan|corp|internal)\b")),
    ("non-example credential URL", re.compile(
        rb"(?i)https?://[^/\s'\"\x00]+:[^/\s'\"\x00]+@[^/\s'\"\x00]+")),
    ("embedded private key", re.compile(
        rb"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----")),
]

def main() -> int:
    result = subprocess.run(
        ["git", "ls-files", "-z"], cwd=ROOT, capture_output=True, check=True
    )
    findings = []
    for pathbytes in result.stdout.split(b"\x00"):
        if not pathbytes:
            continue
        rel = pathlib.Path(pathbytes.decode("utf-8"))
        if rel.suffix.lower() in EXCLUDED:
            continue
        try:
            content = (ROOT / rel).read_bytes()
        except (FileNotFoundError, PermissionError):
            continue
        if b"\x00" in content[:8192]:
            continue
        for label, pattern in SUSPICIOUS:
            for match in pattern.finditer(content):
                # Test fixtures may deliberately contain synthetic userinfo
                # URLs, provided the destination is an RFC example/invalid host.
                if label == "non-example credential URL":
                    host = urlsplit(match.group().decode("utf-8", "replace")).hostname or ""
                    if host.endswith((".example.invalid", ".example.com",
                                      ".example.org", ".example.net")):
                        continue
                findings.append(f"{rel}: {label}")
                break
    if findings:
        print("Internal data must not be committed. Replace with generic examples:")
        print("\n".join(findings))
        return 1
    print("Public repository guard: no generic private infrastructure identifiers found.")
    return 0

if __name__ == "__main__":
    sys.exit(main())
