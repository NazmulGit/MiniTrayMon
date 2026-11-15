#!/usr/bin/env python3
"""CI-Kit verify: version consistency + local-ci gate. Stdlib only.

Runs in two places with the same code:
  - locally, imported by cikit.py (`cikit ci`, `cikit version`);
  - on GitHub runners, vendored as `.github/cikit/verify.py` (copy of this file; `cikit sync` refreshes it).

Usage:
  verify.py version [--root DIR] [--tag vX.Y.Z]
  verify.py local-ci --repo OWNER/NAME --sha SHA      (needs GITHUB_TOKEN or GH_TOKEN)
Exit 0 = pass, 1 = fail. Output: one JSON line.
"""
import argparse
import json
import os
import re
import sys
import urllib.request
from pathlib import Path

SEMVER = re.compile(r"^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$")
CONTEXT = "local-ci"


def _cfg(root):
    p = Path(root) / ".cikit.json"
    if p.exists():
        try:
            return json.loads(p.read_text(encoding="utf-8")).get("version", {})
        except (OSError, ValueError) as e:
            return {"_error": f".cikit.json unreadable: {e}"}
    return {}


def cargo_version(text):
    """Version from [workspace.package] or [package] in a Cargo.toml text, else None."""
    section = None
    found = {}
    for line in text.splitlines():
        s = line.strip()
        if s.startswith("["):
            section = s.strip("[]").strip()
            continue
        m = re.match(r'version\s*=\s*"([^"]+)"', s)
        if m and section in ("workspace.package", "package") and section not in found:
            found[section] = m.group(1)
    return found.get("workspace.package") or found.get("package")


def changelog_top(text):
    """First released version heading ('## [1.2.3]' or '## 1.2.3'), skipping 'Unreleased'."""
    for line in text.splitlines():
        m = re.match(r"^##\s*\[?v?(\d+\.\d+\.\d+[^\]\s]*)\]?", line)
        if m:
            return m.group(1)
    return None


def check_versions(root=".", tag=None):
    root = Path(root)
    cfg = _cfg(root)
    errors, seen = ([cfg["_error"]] if "_error" in cfg else []), {}
    vfile = root / cfg.get("file", "VERSION")
    version = vfile.read_text(encoding="utf-8").strip() if vfile.exists() else None
    if not version and cfg.get("required") is False:
        # repos without a single version (packs, script collections, docs): nothing to compare; tags not gated
        return {"check": "version", "ok": True, "versions": {"VERSION": None}, "errors": [], "skipped": "no VERSION (required=false)"}
    if not version:
        errors.append(f"missing {vfile.name}")
    elif not SEMVER.match(version):
        errors.append(f"{vfile.name} '{version}' is not semver")
    seen["VERSION"] = version
    ct = root / cfg.get("cargo_toml", "Cargo.toml")
    if ct.exists():
        cv = cargo_version(ct.read_text(encoding="utf-8"))
        seen["Cargo.toml"] = cv
        if cv and version and cv != version:
            errors.append(f"Cargo.toml version {cv} != VERSION {version}")
    pj = root / "package.json"
    if cfg.get("package_json") and pj.exists():
        try:
            pv = json.loads(pj.read_text(encoding="utf-8")).get("version")
        except ValueError:
            pv = None
        seen["package.json"] = pv
        if pv != version:
            errors.append(f"package.json version {pv} != VERSION {version}")
    cl = root / cfg.get("changelog", "CHANGELOG.md")
    if cl.exists():
        top = changelog_top(cl.read_text(encoding="utf-8"))
        seen["CHANGELOG"] = top
        if version and top != version:
            errors.append(f"CHANGELOG top release {top} != VERSION {version}")
    elif cfg.get("changelog_required", True):
        errors.append(f"missing {cl.name}")
    if tag:
        t = tag.removeprefix("refs/tags/")
        seen["tag"] = t
        if version and t != f"v{version}":
            errors.append(f"tag {t} != v{version}")
    return {"check": "version", "ok": not errors, "versions": seen, "errors": errors}


def _api(url, token):
    req = urllib.request.Request(url, headers={"Accept": "application/vnd.github+json",
                                               "Authorization": f"Bearer {token}",
                                               "X-GitHub-Api-Version": "2022-11-28"})
    with urllib.request.urlopen(req, timeout=20) as r:  # noqa: S310 (fixed https host)
        return json.loads(r.read().decode("utf-8"))


def local_ci_gate(repo, sha, token=None):
    token = token or os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if not token:
        return {"check": "local-ci", "ok": False, "errors": ["no GITHUB_TOKEN/GH_TOKEN"]}
    try:
        st = _api(f"https://api.github.com/repos/{repo}/commits/{sha}/status?per_page=100", token)
    except Exception as e:  # network/HTTP errors are a failed gate, never a pass
        return {"check": "local-ci", "ok": False, "errors": [f"status API: {e}"]}
    mine = [s for s in st.get("statuses", []) if s.get("context") == CONTEXT]
    if not mine:
        return {"check": "local-ci", "ok": False, "sha": sha, "errors": [f"no '{CONTEXT}' status on {sha[:10]} (run cikit ci + post)"]}
    s = mine[0]  # newest first
    ok = s.get("state") == "success"
    return {"check": "local-ci", "ok": ok, "sha": sha, "state": s.get("state"), "desc": s.get("description"),
            "errors": [] if ok else [f"local-ci is {s.get('state')}: {s.get('description')}"]}


def main(argv=None):
    ap = argparse.ArgumentParser(prog="verify.py")
    sub = ap.add_subparsers(dest="cmd", required=True)
    v = sub.add_parser("version")
    v.add_argument("--root", default=".")
    v.add_argument("--tag")
    g = sub.add_parser("local-ci")
    g.add_argument("--repo", required=True)
    g.add_argument("--sha", required=True)
    a = ap.parse_args(argv)
    res = check_versions(a.root, a.tag) if a.cmd == "version" else local_ci_gate(a.repo, a.sha)
    print(json.dumps(res, separators=(",", ":")))
    return 0 if res["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
