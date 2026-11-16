// Pinch-zoom + pan for the poker hand rankings chart.
// The page viewport locks scale (user-scalable=no), so the chart needs its
// own gesture layer. CLOSE stays outside this viewport and is never transformed.
window.pokerHandsZoom = (function () {
  var MIN = 1;
  var MAX = 4;
  var viewport = null;
  var target = null;
  var scale = 1;
  var tx = 0;
  var ty = 0;
  var pointers = new Map();
  var pinchSpan0 = 0;
  var pinchScale0 = 1;
  var pinchMidX = 0;
  var pinchMidY = 0;
  var pinchTx0 = 0;
  var pinchTy0 = 0;
  var panId = -1;
  var panLastX = 0;
  var panLastY = 0;
  var lastTapAt = 0;
  var lastTapX = 0;
  var lastTapY = 0;
  var moved = false;

  function apply() {
    if (!target) return;
    target.style.transform =
      'translate(' + tx + 'px,' + ty + 'px) scale(' + scale + ')';
    if (viewport)
      viewport.classList.toggle('zoomed', scale > 1.01);
  }

  function clampPan() {
    if (!viewport || !target) return;
    if (scale <= 1.01) {
      scale = 1;
      tx = 0;
      ty = 0;
      return;
    }
    var vr = viewport.getBoundingClientRect();
    var maxX = Math.max(0, (target.offsetWidth * scale - vr.width) / 2);
    var maxY = Math.max(0, (target.offsetHeight * scale - vr.height) / 2);
    tx = Math.min(maxX, Math.max(-maxX, tx));
    ty = Math.min(maxY, Math.max(-maxY, ty));
  }

  function setScaleAt(clientX, clientY, next) {
    if (!viewport) return;
    var vr = viewport.getBoundingClientRect();
    var cx = clientX - vr.left - vr.width / 2;
    var cy = clientY - vr.top - vr.height / 2;
    var contentX = (cx - tx) / scale;
    var contentY = (cy - ty) / scale;
    scale = Math.min(MAX, Math.max(MIN, next));
    tx = cx - contentX * scale;
    ty = cy - contentY * scale;
    clampPan();
    apply();
  }

  function pinchSpan() {
    var pts = Array.from(pointers.values());
    if (pts.length < 2) return 0;
    return Math.hypot(pts[0].x - pts[1].x, pts[0].y - pts[1].y);
  }

  function pinchMid() {
    var pts = Array.from(pointers.values());
    return {
      x: (pts[0].x + pts[1].x) / 2,
      y: (pts[0].y + pts[1].y) / 2
    };
  }

  function onPointerDown(e) {
    if (!viewport || e.button > 0) return;
    viewport.setPointerCapture(e.pointerId);
    pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
    moved = false;

    if (pointers.size === 2) {
      panId = -1;
      pinchSpan0 = pinchSpan();
      pinchScale0 = scale;
      var mid = pinchMid();
      pinchMidX = mid.x;
      pinchMidY = mid.y;
      pinchTx0 = tx;
      pinchTy0 = ty;
    } else if (pointers.size === 1 && scale > 1.01) {
      panId = e.pointerId;
      panLastX = e.clientX;
      panLastY = e.clientY;
    } else {
      panId = -1;
    }
  }

  function onPointerMove(e) {
    if (!pointers.has(e.pointerId)) return;
    pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });

    if (pointers.size >= 2) {
      e.preventDefault();
      var span = pinchSpan();
      if (span > 16 && pinchSpan0 > 16) {
        var mid = pinchMid();
        // Pan with the pinch midpoint, then scale around that point.
        tx = pinchTx0 + (mid.x - pinchMidX);
        ty = pinchTy0 + (mid.y - pinchMidY);
        setScaleAt(mid.x, mid.y, pinchScale0 * (span / pinchSpan0));
        moved = true;
      }
      return;
    }

    if (panId === e.pointerId && scale > 1.01) {
      e.preventDefault();
      var dx = e.clientX - panLastX;
      var dy = e.clientY - panLastY;
      if (Math.abs(dx) + Math.abs(dy) > 2) moved = true;
      panLastX = e.clientX;
      panLastY = e.clientY;
      tx += dx;
      ty += dy;
      clampPan();
      apply();
    }
  }

  function onPointerUp(e) {
    if (!pointers.has(e.pointerId)) return;
    pointers.delete(e.pointerId);
    try { viewport.releasePointerCapture(e.pointerId); } catch (err) { /* already released */ }

    if (pointers.size === 1 && scale > 1.01) {
      var remaining = pointers.keys().next().value;
      var pt = pointers.get(remaining);
      panId = remaining;
      panLastX = pt.x;
      panLastY = pt.y;
    } else {
      panId = -1;
    }

    if (pointers.size === 0 && !moved) {
      var now = performance.now();
      if (now - lastTapAt < 320
          && Math.hypot(e.clientX - lastTapX, e.clientY - lastTapY) < 28) {
        lastTapAt = 0;
        if (scale > 1.05) {
          scale = 1;
          tx = 0;
          ty = 0;
          apply();
        } else {
          setScaleAt(e.clientX, e.clientY, 2.25);
        }
      } else {
        lastTapAt = now;
        lastTapX = e.clientX;
        lastTapY = e.clientY;
      }
    }

    if (pointers.size === 2) {
      pinchSpan0 = pinchSpan();
      pinchScale0 = scale;
      var mid = pinchMid();
      pinchMidX = mid.x;
      pinchMidY = mid.y;
      pinchTx0 = tx;
      pinchTy0 = ty;
    }
  }

  function onWheel(e) {
    e.preventDefault();
    var factor = e.deltaY < 0 ? 1.12 : 1 / 1.12;
    setScaleAt(e.clientX, e.clientY, scale * factor);
  }

  function onGesture(e) {
    // Safari legacy gesture events — block page zoom over the chart.
    e.preventDefault();
  }

  function attach(el) {
    detach();
    if (!el) return;
    viewport = el;
    target = el.querySelector('.hands-chart');
    if (!target) {
      viewport = null;
      return;
    }
    scale = 1;
    tx = 0;
    ty = 0;
    pointers.clear();
    apply();
    viewport.addEventListener('pointerdown', onPointerDown);
    viewport.addEventListener('pointermove', onPointerMove, { passive: false });
    viewport.addEventListener('pointerup', onPointerUp);
    viewport.addEventListener('pointercancel', onPointerUp);
    viewport.addEventListener('wheel', onWheel, { passive: false });
    viewport.addEventListener('gesturestart', onGesture, { passive: false });
    viewport.addEventListener('gesturechange', onGesture, { passive: false });
    viewport.addEventListener('gestureend', onGesture, { passive: false });
  }

  function detach() {
    if (!viewport) return;
    viewport.removeEventListener('pointerdown', onPointerDown);
    viewport.removeEventListener('pointermove', onPointerMove);
    viewport.removeEventListener('pointerup', onPointerUp);
    viewport.removeEventListener('pointercancel', onPointerUp);
    viewport.removeEventListener('wheel', onWheel);
    viewport.removeEventListener('gesturestart', onGesture);
    viewport.removeEventListener('gesturechange', onGesture);
    viewport.removeEventListener('gestureend', onGesture);
    if (target) target.style.transform = '';
    viewport.classList.remove('zoomed');
    viewport = null;
    target = null;
    pointers.clear();
    scale = 1;
    tx = 0;
    ty = 0;
  }

  return { attach: attach, detach: detach };
})();
