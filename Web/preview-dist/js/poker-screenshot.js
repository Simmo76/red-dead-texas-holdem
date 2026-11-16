// Victory screenshot: paint the live table (WebGL) and the HUD into one PNG,
// then put it in the photo library.
//
// Phones and tablets hand the PNG to the system share sheet. On iOS that
// sheet's "Save Image" writes the Photos library; Android's sheet offers the
// gallery. A page cannot write the Photos library without that system step.
// Desktop browsers, which have no photo library, download the PNG instead.
window.pokerShot = (function () {
  function stamp() {
    const d = new Date();
    const p = (n) => String(n).padStart(2, '0');
    return d.getFullYear() + p(d.getMonth() + 1) + p(d.getDate()) + '_' +
      p(d.getHours()) + p(d.getMinutes()) + p(d.getSeconds());
  }

  function dataUrlToBlob(dataUrl) {
    const comma = dataUrl.indexOf(',');
    const mime = (dataUrl.slice(0, comma).match(/:(.*?);/) || [, 'image/png'])[1];
    const bin = atob(dataUrl.slice(comma + 1));
    const arr = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) arr[i] = bin.charCodeAt(i);
    return new Blob([arr], { type: mime });
  }

  function directText(el) {
    let s = '';
    for (const n of el.childNodes) {
      if (n.nodeType === Node.TEXT_NODE) s += n.textContent;
    }
    return s.replace(/\s+/g, ' ').trim();
  }

  function stackZ(el, root) {
    let z = 0;
    for (let n = el; n && n !== root.parentElement; n = n.parentElement) {
      const zi = getComputedStyle(n).zIndex;
      if (zi !== 'auto') {
        const v = parseInt(zi, 10);
        if (!Number.isNaN(v)) z = Math.max(z, v);
      }
    }
    return z;
  }

  function roundRect(ctx, x, y, w, h, r) {
    const radius = Math.max(0, Math.min(r, w / 2, h / 2));
    ctx.beginPath();
    if (!radius) {
      ctx.rect(x, y, w, h);
      return;
    }
    ctx.moveTo(x + radius, y);
    ctx.arcTo(x + w, y, x + w, y + h, radius);
    ctx.arcTo(x + w, y + h, x, y + h, radius);
    ctx.arcTo(x, y + h, x, y, radius);
    ctx.arcTo(x, y, x + w, y, radius);
    ctx.closePath();
  }

  function skipped(el) {
    return el.classList &&
      (el.classList.contains('victory-shot') ||
       el.classList.contains('victory-shot-status') ||
       el.classList.contains('intro-overlay'));
  }

  // Title card and anything fully faded out. Computed opacity is not
  // inherited, so a transparent ancestor still has to hide its children.
  function hiddenBranch(el, root) {
    for (let n = el; n && n !== root.parentElement; n = n.parentElement) {
      if (skipped(n)) return true;
      const s = getComputedStyle(n);
      if (s.display === 'none' || s.visibility === 'hidden') return true;
      const op = parseFloat(s.opacity);
      if (Number.isFinite(op) && op < 0.04) return true;
    }
    return false;
  }

  // Copy the WebGL frame immediately. The drawing buffer is cleared when
  // the browser composites, which can happen as soon as we touch layout,
  // so this has to be the first thing after renderNow().
  function snapshotTable() {
    const gl = document.getElementById('poker-canvas');
    if (!gl) return null;
    if (window.pokerScene && window.pokerScene.renderNow) {
      try { window.pokerScene.renderNow(); } catch (e) { /* keep the last frame */ }
    }
    const snap = document.createElement('canvas');
    snap.width = gl.width || gl.clientWidth || 1;
    snap.height = gl.height || gl.clientHeight || 1;
    try { snap.getContext('2d').drawImage(gl, 0, 0); }
    catch (e) { return null; }
    return snap;
  }

  // Synchronous composite. Reads the WebGL canvas in this turn, after
  // renderNow(), so preserveDrawingBuffer is not required.
  function capture(root) {
    const table = snapshotTable();

    const w = Math.max(1, root.clientWidth || window.innerWidth);
    const h = Math.max(1, root.clientHeight || window.innerHeight);
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(w * dpr);
    canvas.height = Math.round(h * dpr);
    const ctx = canvas.getContext('2d');
    ctx.scale(dpr, dpr);
    ctx.fillStyle = '#0e0906';
    ctx.fillRect(0, 0, w, h);

    const rootRect = root.getBoundingClientRect();
    const nodes = [root, ...root.querySelectorAll('*')];
    const order = nodes.map((el, i) => ({ el, i, z: stackZ(el, root) }));
    order.sort((a, b) => a.z - b.z || a.i - b.i);

    for (const item of order) {
      const el = item.el;
      if (hiddenBranch(el, root)) continue;
      const style = getComputedStyle(el);
      if (style.display === 'none' || style.visibility === 'hidden') continue;
      const rect = el.getBoundingClientRect();
      if (rect.width < 1 || rect.height < 1) continue;
      const x = rect.left - rootRect.left;
      const y = rect.top - rootRect.top;
      const alpha = parseFloat(style.opacity);
      ctx.save();
      ctx.globalAlpha = Number.isFinite(alpha) ? alpha : 1;

      if (el.id === 'poker-canvas') {
        if (table) ctx.drawImage(table, x, y, rect.width, rect.height);
        ctx.restore();
        continue;
      }

      const bg = style.backgroundColor;
      const radius = parseFloat(style.borderRadius) || 0;
      if (bg && bg !== 'transparent' && bg !== 'rgba(0, 0, 0, 0)') {
        ctx.fillStyle = bg;
        roundRect(ctx, x, y, rect.width, rect.height, radius);
        ctx.fill();
      }

      const border = parseFloat(style.borderTopWidth) || 0;
      if (border > 0 && style.borderTopStyle !== 'none' && style.borderTopColor) {
        ctx.strokeStyle = style.borderTopColor;
        ctx.lineWidth = border;
        roundRect(ctx, x + border / 2, y + border / 2, rect.width - border, rect.height - border, radius);
        ctx.stroke();
      }

      if (el.tagName === 'IMG' && el.complete && el.naturalWidth) {
        try { ctx.drawImage(el, x, y, rect.width, rect.height); }
        catch (e) { /* broken image */ }
      }

      const text = directText(el);
      if (text) {
        ctx.fillStyle = style.color || '#fff';
        ctx.font = style.font;
        const align = style.textAlign;
        const centered = align === 'center';
        const right = align === 'right' || align === 'end';
        ctx.textAlign = centered ? 'center' : right ? 'right' : 'left';
        ctx.textBaseline = 'middle';
        const padL = parseFloat(style.paddingLeft) || 0;
        const padR = parseFloat(style.paddingRight) || 0;
        const tx = centered ? x + rect.width / 2 : right ? x + rect.width - padR : x + padL;
        ctx.fillText(text, tx, y + rect.height / 2, Math.max(1, rect.width - padL - padR));
      }
      ctx.restore();
    }
    return canvas;
  }

  function isBlank(canvas) {
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    const spots = [[0.5, 0.5], [0.25, 0.3], [0.75, 0.7], [0.5, 0.15]];
    for (let i = 0; i < spots.length; i++) {
      const x = Math.min(canvas.width - 1, Math.floor(spots[i][0] * canvas.width));
      const y = Math.min(canvas.height - 1, Math.floor(spots[i][1] * canvas.height));
      const p = ctx.getImageData(x, y, 1, 1).data;
      if (p[3] > 8 && (p[0] > 8 || p[1] > 8 || p[2] > 8)) return false;
    }
    return true;
  }

  function downloadBlob(blob, fileName) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.rel = 'noopener';
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1500);
  }

  // 'shared' — the system accepted the image (Save Image writes Photos).
  // 'downloaded' — saved as a PNG file (desktop, or share unavailable).
  // 'aborted' — the player closed the share sheet.
  // 'failed' — nothing was written.
  async function save() {
    const root = document.querySelector('.game3d');
    if (!root) return 'failed';

    let canvas;
    try { canvas = capture(root); }
    catch (e) { return 'failed'; }
    if (!canvas || isBlank(canvas)) return 'failed';

    let blob;
    try { blob = dataUrlToBlob(canvas.toDataURL('image/png')); }
    catch (e) { return 'failed'; }
    const fileName = 'DeadMansHand_victory_' + stamp() + '.png';

    if (typeof File !== 'undefined' && navigator.canShare && navigator.share) {
      try {
        const file = new File([blob], fileName, { type: 'image/png' });
        if (navigator.canShare({ files: [file] })) {
          await navigator.share({ files: [file], title: 'Victory Screen-Shot!' });
          return 'shared';
        }
      } catch (err) {
        if (err && err.name === 'AbortError') return 'aborted';
      }
    }

    try {
      downloadBlob(blob, fileName);
      return 'downloaded';
    } catch (e) {
      return 'failed';
    }
  }

  return { save };
})();
