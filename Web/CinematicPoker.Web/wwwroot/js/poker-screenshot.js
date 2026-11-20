// Victory certificate: paint only the winner's plaque into a PNG.
//
// Phones and tablets hand the PNG to the system share sheet. The player
// chooses what happens there; a web page cannot tell whether the image was
// saved to Photos. Desktop browsers download the PNG instead.
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
       el.classList.contains('cert-coach') ||
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

  // getBoundingClientRect includes CSS transforms. Computed font, border
  // and padding do not, so the painter multiplies those by the same scale
  // the victory plaque uses to grow to the bottom of the screen.
  function cumulativeScale(el) {
    let s = 1;
    for (let n = el; n; n = n.parentElement) {
      const t = getComputedStyle(n).transform;
      if (!t || t === 'none') continue;
      const m = new DOMMatrix(t);
      if (m.a) s *= m.a;
    }
    return s;
  }

  function scaledFont(font, scale) {
    if (Math.abs(scale - 1) < 0.001) return font;
    return font.replace(/(\d*\.?\d+)px/g, function (_, n) {
      return (parseFloat(n) * scale) + 'px';
    });
  }

  function capture(root, captureHeight) {
    const rootRect = root.getBoundingClientRect();
    const w = Math.max(1, rootRect.width || root.clientWidth || window.innerWidth);
    const h = Math.max(1, captureHeight || rootRect.height || root.clientHeight || window.innerHeight);
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(w * dpr);
    canvas.height = Math.round(h * dpr);
    const ctx = canvas.getContext('2d');
    ctx.scale(dpr, dpr);
    ctx.fillStyle = '#0e0906';
    ctx.fillRect(0, 0, w, h);

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
      if (y >= h) continue;
      const scale = cumulativeScale(el);
      const alpha = parseFloat(style.opacity);
      ctx.save();
      ctx.globalAlpha = Number.isFinite(alpha) ? alpha : 1;

      const bg = style.backgroundColor;
      const radius = (parseFloat(style.borderRadius) || 0) * scale;
      const elementHeight = el === root ? h : Math.min(rect.height, h - y);
      if (bg && bg !== 'transparent' && bg !== 'rgba(0, 0, 0, 0)') {
        ctx.fillStyle = bg;
        roundRect(ctx, x, y, rect.width, elementHeight, radius);
        ctx.fill();
      }

      const border = (parseFloat(style.borderTopWidth) || 0) * scale;
      if (border > 0 && style.borderTopStyle !== 'none' && style.borderTopColor) {
        ctx.strokeStyle = style.borderTopColor;
        ctx.lineWidth = border;
        roundRect(ctx, x + border / 2, y + border / 2, rect.width - border, elementHeight - border, radius);
        ctx.stroke();
      }

      if (el.tagName === 'IMG' && el.complete && el.naturalWidth) {
        try { ctx.drawImage(el, x, y, rect.width, rect.height); }
        catch (e) { /* broken image */ }
      }

      const text = directText(el);
      if (text) {
        ctx.fillStyle = style.color || '#fff';
        ctx.font = scaledFont(style.font, scale);
        const align = style.textAlign;
        const centered = align === 'center';
        const right = align === 'right' || align === 'end';
        ctx.textAlign = centered ? 'center' : right ? 'right' : 'left';
        ctx.textBaseline = 'middle';
        const padL = (parseFloat(style.paddingLeft) || 0) * scale;
        const padR = (parseFloat(style.paddingRight) || 0) * scale;
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

  // 'shared' — the system accepted the image; its destination is unknowable.
  // 'downloaded' — saved as a PNG file (desktop, or share unavailable).
  // 'aborted' — the player closed the share sheet.
  // 'failed' — nothing was written.
  async function save() {
    const root = document.querySelector('.banner-win, .overlay-box.table-win');
    if (!root) return 'failed';

    // The action and status live inside the plaque for layout purposes, but
    // the exported certificate ends immediately before those controls.
    const action = root.querySelector('.victory-shot');
    const rootRect = root.getBoundingClientRect();
    const gap = 5 * cumulativeScale(root);
    const captureHeight = action
      ? Math.max(1, Math.floor(action.getBoundingClientRect().top - rootRect.top - gap))
      : rootRect.height;

    let canvas;
    try { canvas = capture(root, captureHeight); }
    catch (e) { return 'failed'; }
    if (!canvas || isBlank(canvas)) return 'failed';

    let blob;
    try { blob = dataUrlToBlob(canvas.toDataURL('image/png')); }
    catch (e) { return 'failed'; }
    const fileName = 'DeadMansHand_certificate_' + stamp() + '.png';

    if (typeof File !== 'undefined' && navigator.canShare && navigator.share) {
      try {
        const file = new File([blob], fileName, { type: 'image/png' });
        if (navigator.canShare({ files: [file] })) {
          await navigator.share({ files: [file], title: 'Victory Certificate' });
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

  // Grow the certified-victor plaque from its top edge until the bottom
  // border sits on the bottom of the screen. Width uses the same scale so
  // the plaque keeps its aspect ratio. Layout size (offsetWidth/Height)
  // ignores the transform, so measuring it does not fight the scale.
  function fitVictoryBanner(banner) {
    if (!banner || !banner.classList.contains('banner-win')) return;
    const carousel = banner.closest('.victory-carousel');
    const parent = banner.offsetParent;
    if (!parent) return;
    const naturalH = banner.offsetHeight;
    const naturalW = banner.offsetWidth;
    if (naturalH < 1 || naturalW < 1) return;
    const anchor = carousel || parent;
    const anchorRect = anchor.getBoundingClientRect();
    const visual = banner.getBoundingClientRect();
    const availH = anchorRect.bottom - visual.top;
    // 1px each side so a width-limited plaque (a narrow phone) can still
    // grow until the height gives out, without clipping the gold border.
    const availW = anchorRect.width - 2;
    if (availH < 1 || availW < 1) return;
    let scale = availH / naturalH;
    if (naturalW * scale > availW) scale = availW / naturalW;
    scale = Math.max(0.4, Math.min(4, scale));
    const next = carousel
      ? 'scale(' + scale.toFixed(4) + ')'
      : 'translateX(-50%) scale(' + scale.toFixed(4) + ')';
    banner.style.transformOrigin = 'top center';
    if (banner.style.transform !== next) banner.style.transform = next;
  }

  function watchVictoryBanner(banner) {
    if (!banner || banner.dataset.victoryFit === '1') return;
    banner.dataset.victoryFit = '1';
    if (typeof ResizeObserver !== 'undefined') {
      const ro = new ResizeObserver(function () { fitVictoryBanner(banner); });
      ro.observe(banner);
    }
    banner.querySelectorAll('img').forEach(function (img) {
      if (!img.complete) {
        img.addEventListener('load', function () { fitVictoryBanner(banner); }, { once: true });
      }
    });
    fitVictoryBanner(banner);
  }

  function scanVictoryBanners() {
    document.querySelectorAll('.banner-win').forEach(watchVictoryBanner);
  }

  function installVictoryFit() {
    const start = function () {
      scanVictoryBanners();
      const mo = new MutationObserver(scanVictoryBanners);
      mo.observe(document.documentElement, { childList: true, subtree: true });
      window.addEventListener('resize', function () {
        document.querySelectorAll('.banner-win').forEach(fitVictoryBanner);
      });
    };
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', start);
    } else {
      start();
    }
  }

  installVictoryFit();

  return { save: save, fitVictoryBanner: fitVictoryBanner };
})();
