import io
import json
import os
import shutil
import urllib.error
import urllib.request
import zipfile
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath

API = "https://api.github.com"
OWNER = os.environ.get("OWNER", "JustinOros")
TOKEN = os.environ.get("GITHUB_TOKEN", "")
REDLOADER_REPO = "ToniMacaroni/RedLoader"
MOD_TOPIC = "sotf-mod"
ROOT = Path(__file__).resolve().parent
SITE_SRC = ROOT / "site"
OUT = ROOT / "_site"


def request(url, accept="application/vnd.github+json"):
    headers = {"Accept": accept, "User-Agent": "sotf-modloader-build"}
    if TOKEN and url.startswith(API):
        headers["Authorization"] = f"Bearer {TOKEN}"
    req = urllib.request.Request(url, headers=headers)
    with urllib.request.urlopen(req, timeout=300) as resp:
        return resp.read()


def api(path):
    return json.loads(request(API + path))


def list_repos():
    repos = []
    page = 1
    while True:
        batch = api(f"/users/{OWNER}/repos?per_page=100&page={page}&type=owner")
        if not batch:
            break
        repos.extend(batch)
        page += 1
    return [r for r in repos if not r.get("archived")]


def latest_release(full_name):
    try:
        return api(f"/repos/{full_name}/releases/latest")
    except urllib.error.HTTPError as e:
        if e.code == 404:
            return None
        raise


def total_downloads(full_name):
    total = 0
    page = 1
    while True:
        batch = api(f"/repos/{full_name}/releases?per_page=100&page={page}")
        if not batch:
            break
        for release in batch:
            for asset in release.get("assets", []):
                total += asset.get("download_count", 0)
        page += 1
    return total


def zip_asset(release):
    for asset in release.get("assets", []):
        if asset["name"].lower().endswith(".zip"):
            return asset
    return None


def slug(value):
    return "".join(c if c.isalnum() or c in "._-" else "-" for c in str(value)) or "x"


def safe_parts(name):
    parts = [p for p in name.replace("\\", "/").split("/") if p not in ("", ".")]
    if not parts or any(p == ".." or ":" in p for p in parts):
        return None
    return parts


def file_entries(zf):
    for info in zf.infolist():
        if info.is_dir() or info.filename.endswith(("/", "\\")):
            continue
        parts = safe_parts(info.filename)
        if parts:
            yield info, parts


def extract(data, dest, prefix=()):
    files = []
    with zipfile.ZipFile(io.BytesIO(data)) as zf:
        for info, parts in file_entries(zf):
            rel = PurePosixPath(*prefix, *parts)
            target = dest.joinpath(*rel.parts)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(zf.read(info))
            files.append({"path": str(rel), "size": info.file_size})
    files.sort(key=lambda f: f["path"].lower())
    return files


def inspect_mod_zip(data):
    meta = {}
    tops = set()
    with zipfile.ZipFile(io.BytesIO(data)) as zf:
        for info, parts in file_entries(zf):
            tops.add(parts[0].lower())
            if not meta and parts[-1].lower() == "manifest.json" and len(parts) >= 2:
                try:
                    meta = json.loads(zf.read(info).decode("utf-8-sig"))
                except ValueError:
                    meta = {}
    prefix = () if tops == {"mods"} else ("Mods",)
    return meta, prefix


def mod_layout(files):
    paths = [f["path"] for f in files]
    dll = next((p for p in paths if p.count("/") == 1 and p.lower().endswith(".dll")), None)
    manifest_path = next((p for p in paths if p.count("/") == 2 and p.lower().endswith("/manifest.json")), None)
    roots = sorted({"/".join(p.split("/")[:2]) for p in paths}, key=str.lower)
    return dll, manifest_path, roots


def build_mod(repo):
    release = latest_release(repo["full_name"])
    if not release:
        print(f"skip {repo['name']}: no release")
        return None
    asset = zip_asset(release)
    if not asset:
        print(f"skip {repo['name']}: latest release has no zip")
        return None
    data = request(asset["browser_download_url"], accept="application/octet-stream")
    meta, prefix = inspect_mod_zip(data)
    tag_version = release["tag_name"].lstrip("vV")
    mod_id = meta.get("id") or repo["name"].removeprefix("sotf-")
    version = meta.get("version") or tag_version
    if version != tag_version:
        print(f"warning {repo['name']}: manifest.json version {version} does not match tag {release['tag_name']}")
    base = PurePosixPath("packages", slug(mod_id), slug(version))
    files = extract(data, OUT.joinpath(*base.parts), prefix)
    dll, manifest_path, roots = mod_layout(files)
    if not dll:
        print(f"skip {repo['name']}: no DLL at the top of the zip")
        shutil.rmtree(OUT.joinpath(*base.parts), ignore_errors=True)
        return None
    downloads = total_downloads(repo["full_name"])
    print(f"mod {mod_id} {version} ({len(files)} files, {downloads} downloads)")
    return {
        "id": mod_id,
        "name": meta.get("name") or mod_id,
        "description": meta.get("description") or repo.get("description") or "",
        "author": meta.get("author") or OWNER,
        "version": version,
        "tag": release["tag_name"],
        "platform": meta.get("platform") or "Client",
        "gameVersion": meta.get("gameVersion") or "",
        "repo": repo["html_url"],
        "releaseUrl": release["html_url"],
        "downloadUrl": asset["browser_download_url"],
        "published": release.get("published_at"),
        "downloads": downloads,
        "base": f"{base}/",
        "dll": dll,
        "manifestPath": manifest_path,
        "roots": roots,
        "files": files,
    }


def build_redloader():
    try:
        release = latest_release(REDLOADER_REPO)
        asset = zip_asset(release) if release else None
        if not asset:
            print("warning: RedLoader release zip not found")
            return None
        data = request(asset["browser_download_url"], accept="application/octet-stream")
    except (urllib.error.URLError, OSError) as e:
        print(f"warning: RedLoader download failed: {e}")
        return None
    version = release["tag_name"]
    base = PurePosixPath("packages", "redloader", slug(version))
    files = extract(data, OUT.joinpath(*base.parts))
    print(f"RedLoader {version} ({len(files)} files)")
    return {
        "name": "RedLoader",
        "version": version,
        "repo": f"https://github.com/{REDLOADER_REPO}",
        "releaseUrl": release["html_url"],
        "downloadUrl": asset["browser_download_url"],
        "license": "Apache-2.0",
        "base": f"{base}/",
        "files": files,
    }


def main():
    if OUT.exists():
        shutil.rmtree(OUT)
    shutil.copytree(SITE_SRC, OUT)
    mods = []
    for repo in sorted(list_repos(), key=lambda r: r["name"].lower()):
        topics = repo.get("topics") or []
        if MOD_TOPIC in topics:
            entry = build_mod(repo)
            if entry:
                mods.append(entry)
    redloader = build_redloader()
    manifest = {
        "generated": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "owner": OWNER,
        "redloader": redloader,
        "mods": mods,
    }
    (OUT / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    (OUT / ".nojekyll").write_text("", encoding="utf-8")
    print(f"done: {len(mods)} mods")


if __name__ == "__main__":
    main()
