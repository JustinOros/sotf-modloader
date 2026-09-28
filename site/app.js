const STATE_FILE = "sotf-modloader.json";
const DB_NAME = "sotf-modloader";
const CONCURRENCY = 6;
const supportsFs = "showDirectoryPicker" in window;

const state = {
  manifest: null,
  root: null,
  kind: null,
  needsPermission: false,
  folderError: null,
  busy: false,
  redloader: null,
  statuses: new Map(),
};

const $ = (id) => document.getElementById(id);

function el(tag, props = {}, ...children) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(props)) {
    if (value === null || value === undefined || value === false) continue;
    if (key.startsWith("on")) node.addEventListener(key.slice(2), value);
    else if (key === "class") node.className = value;
    else node.setAttribute(key, value === true ? "" : value);
  }
  for (const child of children.flat()) {
    if (child !== null && child !== undefined && child !== false) node.append(child);
  }
  return node;
}

function setStatus(message, kind = "") {
  const node = $("status");
  node.textContent = message;
  node.dataset.kind = kind;
}

function idb(mode, fn) {
  return new Promise((resolve, reject) => {
    const open = indexedDB.open(DB_NAME, 1);
    open.onupgradeneeded = () => open.result.createObjectStore("kv");
    open.onerror = () => reject(open.error);
    open.onsuccess = () => {
      const tx = open.result.transaction("kv", mode);
      const req = fn(tx.objectStore("kv"));
      tx.oncomplete = () => resolve(req.result);
      tx.onerror = () => reject(tx.error);
    };
  });
}

const kvGet = (key) => idb("readonly", (store) => store.get(key));
const kvSet = (key, value) => idb("readwrite", (store) => store.put(value, key));

function splitPath(path) {
  const parts = path.split("/");
  const name = parts.pop();
  return { parts, name };
}

async function getDir(root, parts, create) {
  let dir = root;
  for (const part of parts) dir = await dir.getDirectoryHandle(part, { create });
  return dir;
}

async function exists(root, path) {
  const { parts, name } = splitPath(path);
  let dir;
  try {
    dir = await getDir(root, parts, false);
  } catch {
    return false;
  }
  try {
    await dir.getFileHandle(name);
    return true;
  } catch {}
  try {
    await dir.getDirectoryHandle(name);
    return true;
  } catch {
    return false;
  }
}

async function readText(root, path) {
  const { parts, name } = splitPath(path);
  try {
    const dir = await getDir(root, parts, false);
    const file = await (await dir.getFileHandle(name)).getFile();
    return (await file.text()).replace(/^\uFEFF/, "");
  } catch {
    return null;
  }
}

async function readJson(root, path) {
  const text = await readText(root, path);
  if (text === null) return null;
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

async function writeFile(root, path, data) {
  const { parts, name } = splitPath(path);
  const dir = await getDir(root, parts, true);
  const handle = await dir.getFileHandle(name, { create: true });
  const writable = await handle.createWritable();
  try {
    await writable.write(data);
    await writable.close();
  } catch (e) {
    await writable.abort().catch(() => {});
    throw e;
  }
}

async function removePath(root, path) {
  const { parts, name } = splitPath(path);
  try {
    const dir = await getDir(root, parts, false);
    await dir.removeEntry(name, { recursive: true });
  } catch (e) {
    if (e.name !== "NotFoundError") throw e;
  }
}

function fileUrl(pkg, path) {
  return pkg.base + path.split("/").map(encodeURIComponent).join("/");
}

async function downloadPackage(pkg, onProgress) {
  const total = pkg.files.length;
  const blobs = new Array(total);
  let next = 0;
  let done = 0;
  async function worker() {
    while (next < total) {
      const index = next++;
      const file = pkg.files[index];
      const res = await fetch(fileUrl(pkg, file.path), { cache: "no-cache" });
      if (!res.ok) throw new Error(`Download failed for ${file.path} (HTTP ${res.status}).`);
      blobs[index] = await res.blob();
      done++;
      onProgress(`Downloading ${done} of ${total} files`);
    }
  }
  await Promise.all(Array.from({ length: Math.min(CONCURRENCY, total) }, worker));
  return blobs;
}

async function installPackage(pkg, cleanRoots) {
  const label = `${pkg.name} ${pkg.version}`;
  const blobs = await downloadPackage(pkg, (msg) => setStatus(`${label}: ${msg}`));
  for (const root of cleanRoots) await removePath(state.root, root);
  for (let i = 0; i < pkg.files.length; i++) {
    setStatus(`${label}: Writing ${i + 1} of ${pkg.files.length} files`);
    await writeFile(state.root, pkg.files[i].path, blobs[i]);
  }
}

async function detectKind(root) {
  if (await exists(root, "SonsOfTheForest.exe")) return "client";
  if (await exists(root, "SonsOfTheForestDS.exe")) return "server";
  return null;
}

async function modStatus(mod) {
  if (!(await exists(state.root, mod.dll))) return { installed: false };
  const meta = mod.manifestPath ? await readJson(state.root, mod.manifestPath) : null;
  const version = meta && meta.version ? String(meta.version) : null;
  return { installed: true, version, current: version === mod.version };
}

async function redloaderStatus(rl) {
  const installed = (await exists(state.root, "version.dll")) && (await exists(state.root, "_Redloader"));
  if (!installed) return { installed: false };
  const saved = await readJson(state.root, STATE_FILE);
  const version = saved && saved.redloader ? String(saved.redloader) : null;
  return { installed: true, version, current: version === rl.version };
}

async function refresh() {
  if (!state.root || !state.kind) return;
  const { mods, redloader } = state.manifest;
  state.redloader = redloader ? await redloaderStatus(redloader) : null;
  state.statuses.clear();
  for (const mod of mods) state.statuses.set(mod.id, await modStatus(mod));
}

function explain(e) {
  const name = e && e.name;
  if (name === "NoModificationAllowedError" || name === "InvalidModificationError") {
    return "A file is in use. Close Sons of the Forest and try again.";
  }
  if (name === "NotAllowedError" || name === "SecurityError") {
    return "The browser blocked access to the game folder. Reconnect the folder and try again.";
  }
  if (name === "AbortError") {
    return "The browser stopped the write, possibly a download safety check. Try again, or use the release page link to install manually.";
  }
  return (e && e.message) || String(e);
}

async function run(task) {
  if (state.busy) return;
  state.busy = true;
  render();
  try {
    await task();
  } catch (e) {
    setStatus(explain(e), "error");
  } finally {
    try {
      await refresh();
    } catch {}
    state.busy = false;
    render();
  }
}

async function connect(handle) {
  const kind = await detectKind(handle);
  if (!kind) {
    state.folderError = `${handle.name} doesn't contain SonsOfTheForest.exe. Choose the folder the game is installed in.`;
    return false;
  }
  state.root = handle;
  state.kind = kind;
  state.needsPermission = false;
  state.folderError = null;
  await kvSet("root", handle).catch(() => {});
  await refresh();
  return true;
}

async function chooseFolder() {
  let handle;
  try {
    handle = await window.showDirectoryPicker({ id: "sotf-game", mode: "readwrite" });
  } catch (e) {
    if (e.name !== "AbortError") setStatus(explain(e), "error");
    return;
  }
  await run(async () => {
    if (await connect(handle)) setStatus(`Connected to ${handle.name}.`, "ok");
    else setStatus(state.folderError, "error");
  });
}

async function reconnect() {
  const permission = await state.root.requestPermission({ mode: "readwrite" });
  if (permission !== "granted") {
    setStatus("Access wasn't granted. Choose the game folder to continue.", "error");
    return;
  }
  await run(async () => {
    if (await connect(state.root)) setStatus(`Connected to ${state.root.name}.`, "ok");
  });
}

function installRedloader() {
  const rl = state.manifest.redloader;
  return run(async () => {
    await installPackage(rl, []);
    const saved = (await readJson(state.root, STATE_FILE)) || {};
    saved.redloader = rl.version;
    await writeFile(state.root, STATE_FILE, JSON.stringify(saved, null, 2));
    setStatus(`RedLoader ${rl.version} installed. Launch the game once so it can finish setting up.`, "ok");
  });
}

function installMod(mod) {
  return run(async () => {
    const before = state.statuses.get(mod.id);
    await installPackage(mod, mod.roots);
    const message = before && before.installed ? `${mod.name} updated to ${mod.version}.` : `${mod.name} ${mod.version} installed.`;
    setStatus(message, "ok");
  });
}

function removeMod(mod) {
  if (!window.confirm(`Remove ${mod.name} from your game?`)) return;
  return run(async () => {
    for (const root of mod.roots) await removePath(state.root, root);
    setStatus(`${mod.name} removed.`, "ok");
  });
}

function updateAll(mods) {
  return run(async () => {
    for (const mod of mods) await installPackage(mod, mod.roots);
    setStatus(`Updated ${mods.length} mod${mods.length === 1 ? "" : "s"}.`, "ok");
  });
}

function visibleMods() {
  const mods = [...state.manifest.mods].sort((a, b) => a.name.localeCompare(b.name));
  if (!state.kind) return mods;
  return mods.filter((mod) => {
    const platform = (mod.platform || "Client").toLowerCase();
    return platform === "universal" || platform === state.kind;
  });
}

function connected() {
  return Boolean(state.root && state.kind && !state.needsPermission);
}

function versionLine(pkg, status) {
  const items = [el("span", {}, `Latest ${pkg.version}`)];
  if (status && status.installed) {
    if (status.current) {
      items.push(el("span", { class: "state-current" }, "Installed, up to date"));
    } else if (status.version) {
      items.push(el("span", { class: "state-update" }, `Installed ${status.version}, update available`));
    } else {
      items.push(el("span", { class: "state-update" }, "Installed, version unknown"));
    }
  } else if (status) {
    items.push(el("span", {}, "Not installed"));
  }
  return items;
}

function renderFolder() {
  const node = $("folder");
  node.replaceChildren();
  node.dataset.state = state.folderError ? "error" : connected() ? "connected" : "idle";

  if (!supportsFs) {
    node.append(
      el("div", { class: "folder-text" },
        el("p", { class: "folder-name" }, "This browser can't install mods"),
        el("p", { class: "folder-help" }, "Open this page in Chrome or Edge on Windows to install mods with one click. You can still download each mod from its release page below."),
      ),
    );
    return;
  }

  if (connected()) {
    node.append(
      el("div", { class: "folder-text" },
        el("p", { class: "folder-name" }, state.root.name),
        el("p", { class: "folder-help" }, state.kind === "server" ? "Dedicated server folder" : "Game folder"),
      ),
      el("button", { class: "secondary", disabled: state.busy, onclick: chooseFolder }, "Change folder"),
    );
    return;
  }

  if (state.root && state.needsPermission) {
    node.append(
      el("div", { class: "folder-text" },
        el("p", { class: "folder-name" }, state.root.name),
        el("p", { class: "folder-help" }, "Allow access to this folder again to manage your mods."),
      ),
      el("div", { class: "actions" },
        el("button", { class: "secondary", disabled: state.busy, onclick: chooseFolder }, "Choose another"),
        el("button", { class: "primary", disabled: state.busy, onclick: reconnect }, "Reconnect"),
      ),
    );
    return;
  }

  node.append(
    el("div", { class: "folder-text" },
      el("p", { class: "folder-name" }, "Choose your game folder"),
      el("p", { class: "folder-help" },
        state.folderError ? `${state.folderError} ` : "",
        "On Steam it's usually ",
        el("code", {}, "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Sons Of The Forest"),
        ". In Steam you can find it with Manage, Browse local files.",
      ),
    ),
    el("button", { class: "primary", disabled: state.busy, onclick: chooseFolder }, "Choose game folder"),
  );
}

function renderRedloader() {
  const node = $("redloader");
  const rl = state.manifest.redloader;
  node.hidden = !rl;
  if (!rl) return;
  const status = connected() ? state.redloader : null;
  const actions = [];
  if (connected() && status) {
    if (!status.installed) {
      actions.push(el("button", { class: "primary", disabled: state.busy, onclick: installRedloader }, "Install RedLoader"));
    } else if (!status.current) {
      actions.push(el("button", { class: status.version ? "primary" : "secondary", disabled: state.busy, onclick: installRedloader }, status.version ? "Update" : `Reinstall ${rl.version}`));
    }
  }
  node.replaceChildren(
    el("div", { class: "block-head" }, el("h2", { id: "redloader-title" }, "Mod framework")),
    el("ul", { class: "rows" },
      el("li", { class: "row" },
        el("div", {},
          el("h3", {}, el("a", { href: rl.repo, target: "_blank", rel: "noopener" }, "RedLoader")),
          el("p", { class: "desc" }, "Every mod on this page runs on RedLoader. Install it before adding mods."),
          el("p", { class: "meta" }, versionLine(rl, status), el("a", { href: rl.releaseUrl, target: "_blank", rel: "noopener" }, "Release notes")),
        ),
        el("div", { class: "actions" }, actions),
      ),
    ),
  );
}

function modRow(mod) {
  const status = connected() ? state.statuses.get(mod.id) : null;
  const actions = [];
  if (connected() && status) {
    if (!status.installed) {
      actions.push(el("button", { class: "primary", disabled: state.busy, onclick: () => installMod(mod) }, "Install"));
    } else {
      if (!status.current) {
        actions.push(el("button", { class: "primary", disabled: state.busy, onclick: () => installMod(mod) }, status.version ? "Update" : "Reinstall"));
      }
      actions.push(el("button", { class: "danger", disabled: state.busy, onclick: () => removeMod(mod) }, "Remove"));
    }
  }
  return el("li", { class: "row" },
    el("div", {},
      el("h3", {}, el("a", { href: mod.repo, target: "_blank", rel: "noopener" }, mod.name)),
      mod.description ? el("p", { class: "desc" }, mod.description) : null,
      el("p", { class: "meta" },
        versionLine(mod, status),
        el("a", { href: mod.releaseUrl, target: "_blank", rel: "noopener" }, supportsFs ? "Release notes" : "Download"),
      ),
    ),
    el("div", { class: "actions" }, actions),
  );
}

function renderMods() {
  const node = $("mods");
  const mods = visibleMods();
  const outdated = connected()
    ? mods.filter((mod) => {
        const status = state.statuses.get(mod.id);
        return status && status.installed && !status.current;
      })
    : [];
  const head = el("div", { class: "block-head" },
    el("h2", { id: "mods-title" }, "Mods"),
    outdated.length > 1
      ? el("button", { class: "primary", disabled: state.busy, onclick: () => updateAll(outdated) }, `Update all (${outdated.length})`)
      : null,
  );
  const notes = [];
  if (connected() && state.redloader && !state.redloader.installed && state.manifest.redloader) {
    notes.push(el("p", { class: "block-note" }, "RedLoader isn't installed yet. Mods won't load until it is."));
  }
  if (!mods.length) {
    notes.push(el("p", { class: "block-note" }, state.kind === "server" ? "No mods here run on a dedicated server yet." : "No mods are published yet."));
  }
  node.replaceChildren(head, ...notes, el("ul", { class: "rows" }, mods.map(modRow)));
}

function renderFooter() {
  const { owner, generated, redloader } = state.manifest;
  const updated = generated ? new Date(generated).toLocaleString() : "unknown";
  $("footer").replaceChildren(
    el("p", {}, "Mods by ", el("a", { href: `https://github.com/${owner}`, target: "_blank", rel: "noopener" }, owner), "."),
    redloader
      ? el("p", {}, "RedLoader by ToniMacaroni, redistributed unmodified under the ",
          el("a", { href: `${redloader.repo}/blob/main/LICENSE.md`, target: "_blank", rel: "noopener" }, "Apache License 2.0"), ".")
      : null,
    el("p", {}, `Mod list updated ${updated}.`),
  );
}

function render() {
  if (!state.manifest) return;
  renderFolder();
  renderRedloader();
  renderMods();
  renderFooter();
}

async function init() {
  try {
    const res = await fetch("manifest.json", { cache: "no-cache" });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    state.manifest = await res.json();
  } catch {
    setStatus("The mod list couldn't be loaded. Refresh the page to try again.", "error");
    return;
  }
  if (supportsFs) {
    const saved = await kvGet("root").catch(() => null);
    if (saved) {
      state.root = saved;
      const permission = await saved.queryPermission({ mode: "readwrite" }).catch(() => "denied");
      if (permission === "granted") {
        const ok = await connect(saved).catch(() => false);
        if (!ok) state.root = null;
      } else {
        state.needsPermission = true;
      }
    }
  }
  render();
}

init();
