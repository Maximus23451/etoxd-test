// Light/dark mode: follows the system setting until the user picks one with a [data-theme-toggle] button.
(function () {
  const KEY = "eto-theme";
  const root = document.documentElement;
  const media = window.matchMedia("(prefers-color-scheme: dark)");
  const ICONS = {
    moon: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5Z"/></svg>',
    sun: '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="4"/><path d="M12 2v2m0 16v2M4.9 4.9l1.4 1.4m11.4 11.4 1.4 1.4M2 12h2m16 0h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/></svg>',
  };

  try {
    const saved = localStorage.getItem(KEY);
    if (saved === "light" || saved === "dark") root.dataset.theme = saved;
  } catch (e) {
    // No storage: just follow the system setting.
  }

  const isDark = () => (root.dataset.theme ? root.dataset.theme === "dark" : media.matches);

  function sync() {
    const dark = isDark();
    root.dataset.resolvedTheme = dark ? "dark" : "light";
    document.querySelectorAll("[data-theme-toggle]").forEach((button) => {
      button.innerHTML = dark ? ICONS.sun : ICONS.moon;
      button.setAttribute("aria-label", dark ? "Világos mód" : "Sötét mód");
      button.title = dark ? "Világos mód" : "Sötét mód";
    });
  }

  document.addEventListener("click", (event) => {
    if (!event.target.closest("[data-theme-toggle]")) return;
    root.dataset.theme = isDark() ? "light" : "dark";
    try {
      localStorage.setItem(KEY, root.dataset.theme);
    } catch (e) {
      // Choice lasts for this page only.
    }
    sync();
  });
  sync();
  document.addEventListener("DOMContentLoaded", sync);
  media.addEventListener("change", sync);
  document.addEventListener("eto-appearance-change", sync);
})();

// Personal palette, accessible appearance dialog and browser-local idea notebook.
(function () {
  const palettes = [
    {id:'classic', name:'ETO Klasszikus', note:'Az eredeti zöld, arany és krém.', bg:'#f5f5f0', surface:'#ffffff', extra:'#176131'},
    {id:'sand', name:'Homokkő', note:'Az ETO színei meleg homok és terrakotta mellett.', bg:'#f4e9db', surface:'#fffaf3', extra:'#974d36'},
    {id:'slate', name:'Palakék', note:'Az ETO színei visszafogott palakékkel.', bg:'#eaf0f3', surface:'#f9fcfd', extra:'#365d73'},
    {id:'forest', name:'Erdő', note:'Az ETO színei zsályával és mély erdőzölddel.', bg:'#e9eee5', surface:'#f8faf5', extra:'#486443'}
  ];
  const root = document.documentElement;
  const read = (key, fallback) => { try { return JSON.parse(localStorage.getItem(key)) ?? fallback; } catch { return fallback; } };
  const save = (key, value) => { try { localStorage.setItem(key, JSON.stringify(value)); return true; } catch { return false; } };
  const selected = read('eto-palette', 'classic');
  root.dataset.palette = palettes.some(p => p.id === selected) ? selected : 'classic';
  const escape = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  document.addEventListener('DOMContentLoaded', () => {
    const trigger = document.createElement('button');
    trigger.type = 'button'; trigger.className = 'appearance-trigger'; trigger.textContent = 'Megjelenés & ötletek';
    const bar = document.querySelector('.topbar-user');
    if (bar) bar.insertBefore(trigger, bar.querySelector('[data-theme-toggle]'));
    else { trigger.classList.add('appearance-floating'); document.body.append(trigger); }
    const dialog = document.createElement('dialog'); dialog.className = 'appearance-dialog';
    dialog.setAttribute('aria-labelledby', 'appearance-title');
    dialog.innerHTML = `<header class="appearance-head"><div><span class="eyebrow">SAJÁT FELÜLETED</span><h2 id="appearance-title">Színek & ötletek</h2></div><button type="button" class="appearance-close" aria-label="Bezárás">✕</button></header>
      <p class="appearance-intro">Négy színterv, egy klub. Válassz palettát és megjelenítési módot; a választást ez a böngésző megjegyzi.</p>
      <fieldset class="mode-options"><legend>Megjelenítési mód</legend><label><input type="radio" name="appearance-mode" value="system"> Rendszer</label><label><input type="radio" name="appearance-mode" value="light"> Világos</label><label><input type="radio" name="appearance-mode" value="dark"> Sötét</label></fieldset>
      <fieldset class="palette-options"><legend>Színtervek · élő előnézet</legend><div class="palette-grid">${palettes.map(p => `<label class="palette-option"><input type="radio" name="palette" value="${p.id}"><span class="palette-name">${p.name}</span><span class="palette-preview" style="--preview-bg:${p.bg};--preview-surface:${p.surface};--preview-extra:${p.extra}"><span class="preview-bar">ETO <span>JÁTÉKOSPORTÁL</span></span><span class="preview-body"><strong>Ma a pályán</strong><span class="preview-card"><b>16:00 · Csapatedzés</b><small>U17 · Füves pálya</small><span class="preview-button">BELÉPEK →</span></span><span class="preview-accent">Csapatban erősebbek</span></span></span><span class="palette-note">${p.note}</span><span class="palette-swatches" aria-hidden="true"><i style="background:#176131"></i><i style="background:#b8994d"></i><i style="background:#f9f5ef"></i><i style="background:${p.extra}"></i></span></label>`).join('')}</div></fieldset>
      <section class="idea-box"><h3>Ötletdoboz</h3><p>Mivel legyen jobb a portál? Saját ötleteid itt, ebben a böngészőben maradnak; nem kerülnek elküldésre a klubnak.</p><form id="idea-form"><label for="idea-text">Az ötleted</label><textarea id="idea-text" name="idea" rows="3" maxlength="500" required placeholder="Pl. heti naptárnézet, edzésemlékeztető…"></textarea><button type="submit" class="idea-submit">Ötlet mentése</button></form><p class="idea-status" role="status"></p><ul class="idea-list"></ul></section>`;
    document.body.append(dialog);
    function showIdeas() {
      const ideas = read('eto-ideas', []);
      dialog.querySelector('.idea-list').innerHTML = (Array.isArray(ideas) ? ideas : []).map((idea, i) => `<li><span>${escape(idea)}</span><button type="button" data-remove-idea="${i}" aria-label="Ötlet törlése: ${escape(idea)}">Törlés</button></li>`).join('');
    }
    function syncChoices() {
      dialog.querySelectorAll('[name="palette"]').forEach(input => input.checked = input.value === root.dataset.palette);
      dialog.querySelectorAll('[name="appearance-mode"]').forEach(input => input.checked = input.value === (root.dataset.theme || 'system'));
    }
    trigger.addEventListener('click', () => { syncChoices(); showIdeas(); dialog.showModal(); });
    dialog.querySelector('.appearance-close').addEventListener('click', () => dialog.close());
    dialog.addEventListener('change', event => {
      if (event.target.name === 'palette') { root.dataset.palette = event.target.value; save('eto-palette', event.target.value); }
      if (event.target.name === 'appearance-mode') {
        const mode = event.target.value;
        if (mode === 'system') delete root.dataset.theme; else root.dataset.theme = mode;
        try { if (mode === 'system') localStorage.removeItem('eto-theme'); else localStorage.setItem('eto-theme', mode); } catch {}
        // Existing toggle updates its icon and accessible name on system changes.
        document.dispatchEvent(new Event('eto-appearance-change'));
      }
    });
    dialog.querySelector('#idea-form').addEventListener('submit', event => {
      event.preventDefault(); const input = dialog.querySelector('#idea-text'); const idea = input.value.trim();
      if (!idea) { input.setCustomValidity('Írj be egy ötletet.'); input.reportValidity(); return; }
      const stored = read('eto-ideas', []); const ideas = Array.isArray(stored) ? stored : [];
      const saved = save('eto-ideas', [idea, ...ideas].slice(0,50));
      dialog.querySelector('.idea-status').textContent = saved ? 'Ötlet elmentve ebben a böngészőben.' : 'A böngésző nem engedélyezi a mentést.';
      if (saved) { input.value = ''; showIdeas(); }
    });
    dialog.querySelector('#idea-text').addEventListener('input', event => event.target.setCustomValidity(''));
    dialog.addEventListener('click', event => { const button = event.target.closest('[data-remove-idea]'); if (!button) return;
      const ideas = read('eto-ideas', []); if (!Array.isArray(ideas)) return;
      ideas.splice(Number(button.dataset.removeIdea),1); if (save('eto-ideas',ideas)) showIdeas();
    });
  });
})();
