const state = { manifest: null };

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

function formatSize(bytes) {
  if (!bytes) return "";
  if (bytes >= 1048576) return `${(bytes / 1048576).toFixed(1)} MB`;
  return `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

function link(href, text) {
  return el("a", { href, target: "_blank", rel: "noopener" }, text);
}

function renderGet() {
  const node = $("get");
  const { app, bundle } = state.manifest;
  const cards = [];
  if (app) {
    cards.push(
      el("div", { class: "get-card" },
        el("h2", {}, "Windows app"),
        el("p", {}, "Finds your game automatically, even inside Program Files, and installs RedLoader and mods with one click."),
        el("a", { class: "button primary", href: app.url }, `Download the app (${formatSize(app.size)})`),
        el("p", { class: "get-note" }, `Version ${app.version}. If Windows shows a warning, click More info, then Run anyway.`),
      ),
    );
  }
  if (bundle) {
    cards.push(
      el("div", { class: "get-card" },
        el("h2", {}, "Zip download"),
        el("p", {}, "RedLoader and every mod in one zip. Extract it into your Sons of the Forest folder, the one that contains SonsOfTheForest.exe."),
        el("a", { class: "button secondary", href: bundle.url, download: "sotf-mods.zip" }, `Download zip (${formatSize(bundle.size)})`),
      ),
    );
  }
  node.hidden = !cards.length;
  node.replaceChildren(...cards);
}

function renderRedloader() {
  const node = $("redloader");
  const rl = state.manifest.redloader;
  node.hidden = !rl;
  if (!rl) return;
  node.replaceChildren(
    el("div", { class: "block-head" }, el("h2", { id: "redloader-title" }, "Mod framework")),
    el("ul", { class: "rows" },
      el("li", { class: "row" },
        el("div", {},
          el("h3", {}, link(rl.repo, "RedLoader")),
          el("p", { class: "desc" }, "Every mod on this page runs on RedLoader. The app and the zip include it."),
          el("p", { class: "meta" }, el("span", {}, `Latest ${rl.version}`), link(rl.releaseUrl, "Release notes")),
        ),
      ),
    ),
  );
}

async function copyText(text) {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    const area = el("textarea", { readonly: true, style: "position:fixed;opacity:0" });
    area.value = text;
    document.body.append(area);
    area.select();
    let ok = false;
    try {
      ok = document.execCommand("copy");
    } catch {}
    area.remove();
    return ok;
  }
}

const COPY_ICON = '<svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="12" height="12" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>';

function copyButton(command) {
  const label = el("span", {}, "Copy PowerShell install");
  const button = el("button", { type: "button", class: "copy", title: command }, label);
  button.insertAdjacentHTML("afterbegin", COPY_ICON);
  button.addEventListener("click", async () => {
    const ok = await copyText(command);
    label.textContent = ok ? "Copied" : "Copy failed";
    setTimeout(() => {
      label.textContent = "Copy PowerShell install";
    }, 2000);
  });
  return button;
}

function modRow(mod) {
  return el("li", { class: "row" },
    el("div", {},
      el("h3", {}, link(mod.repo, mod.name)),
      mod.description ? el("p", { class: "desc" }, mod.description) : null,
      el("p", { class: "meta" },
        el("span", {}, `Latest ${mod.version}`),
        typeof mod.downloads === "number" ? el("span", {}, `${mod.downloads.toLocaleString()} downloads`) : null,
        link(mod.releaseUrl, "Release notes"),
        mod.installCommand ? copyButton(mod.installCommand) : null,
      ),
    ),
  );
}

function renderMods() {
  const mods = [...state.manifest.mods].sort((a, b) => a.name.localeCompare(b.name));
  const notes = mods.length ? [] : [el("p", { class: "block-note" }, "No mods are published yet.")];
  $("mods").replaceChildren(
    el("div", { class: "block-head" }, el("h2", { id: "mods-title" }, "Mods")),
    ...notes,
    el("ul", { class: "rows" }, mods.map(modRow)),
  );
}

function renderFooter() {
  const { owner, generated, redloader } = state.manifest;
  const updated = generated ? new Date(generated).toLocaleString() : "unknown";
  $("footer").replaceChildren(
    el("p", {}, "Mods by ", link(`https://github.com/${owner}`, owner), "."),
    redloader
      ? el("p", {}, "RedLoader by ToniMacaroni, redistributed unmodified under the ",
          link(`${redloader.repo}/blob/main/LICENSE.md`, "Apache License 2.0"), ".")
      : null,
    el("p", {}, `Mod list updated ${updated}.`),
  );
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
  renderGet();
  renderRedloader();
  renderMods();
  renderFooter();
}

init();
