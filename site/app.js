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

function optionCard(number, title, text, action, note, recommended) {
  return el("div", { class: recommended ? "get-card recommended" : "get-card" },
    el("p", { class: "get-option" }, recommended ? `Option ${number} (Recommended)` : `Option ${number}`),
    el("h3", {}, title),
    el("p", {}, text),
    action,
    note ? el("p", { class: "get-note" }, note) : null,
  );
}

function renderGet() {
  const node = $("get");
  const { app, bundle, mods } = state.manifest;
  const options = [];
  if (app) {
    options.push((n) => optionCard(n, "Windows app",
      "Finds your game automatically. Installs, updates and removes RedLoader and mods with one click.",
      el("a", { class: "button primary", href: app.url }, `Download app (${formatSize(app.size)})`),
      `Version ${app.version}. If Windows shows a warning, click More info, then Run anyway.`,
      true));
  }
  if (bundle) {
    options.push((n) => optionCard(n, "Zip download",
      "RedLoader and every mod in one zip. Extract it into your game folder, the one with SonsOfTheForest.exe.",
      el("a", { class: "button secondary", href: bundle.url, download: "sotf-mods.zip" }, `Download zip (${formatSize(bundle.size)})`),
      "Download it again to update. Remove mods by deleting them from the Mods folder."));
  }
  if ((mods || []).some((mod) => mod.installCommand)) {
    options.push((n) => optionCard(n, "PowerShell scripts",
      "Install one mod at a time. Copy a mod's command from the list below and paste it into PowerShell.",
      el("a", { class: "button secondary", href: "#mods" }, "Go to the mod list"),
      "Installs RedLoader too if needed. Run the command again to update."));
  }
  node.hidden = !options.length;
  node.replaceChildren(
    el("div", { class: "block-head" }, el("h2", { id: "get-title" }, "Choose how to install")),
    el("div", { class: "get-grid" }, options.map((make, i) => make(i + 1))),
  );
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
          el("p", { class: "desc" }, "Every mod on this page runs on RedLoader. All three install options include it."),
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
    el("p", {}, "Free code signing provided by ", link("https://about.signpath.io", "SignPath.io"),
      ", certificate by ", link("https://signpath.org", "SignPath Foundation"), "."),
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
