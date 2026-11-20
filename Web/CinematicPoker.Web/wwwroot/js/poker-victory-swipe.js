// Victory Cover Flow: finger/mouse drags certificates in real time (page-turn),
// then snaps on release.
(function (w) {
    var THRESH = 0.26;
    var DRAG_WIDTH_RATIO = 0.55;
    var EDGE_RESIST = 0.22;

    var LAYOUT = {
        '-2': { tx: -132, tz: -140, ry: 52, sc: 0.66, op: 0.42, br: 0.72, z: 3 },
        '-1': { tx: -88, tz: -48, ry: 42, sc: 0.84, op: 0.78, br: 0.72, z: 6 },
        '0': { tx: 0, tz: 72, ry: 0, sc: 1, op: 1, br: 1, z: 10 },
        '1': { tx: 88, tz: -48, ry: -42, sc: 0.84, op: 0.78, br: 0.72, z: 6 },
        '2': { tx: 132, tz: -140, ry: -52, sc: 0.66, op: 0.42, br: 0.72, z: 3 }
    };

    var drag = null;

    function carouselRoot(el) {
        return el && el.closest ? el.closest('.victory-carousel-nav') : null;
    }

    function track(root) {
        return root ? root.querySelector('.victory-carousel-track') : null;
    }

    function stage(root) {
        return root ? root.querySelector('.victory-swipe-stage, .victory-carousel-viewport') : null;
    }

    function skipTarget(t) {
        return t && t.closest && t.closest(
            'button, a, input, textarea, select, .victory-nav, .victory-shot, .cert-coach');
    }

    function lerp(a, b, t) {
        return a + (b - a) * t;
    }

    function lerpState(a, b, t) {
        return {
            tx: lerp(a.tx, b.tx, t),
            tz: lerp(a.tz, b.tz, t),
            ry: lerp(a.ry, b.ry, t),
            sc: lerp(a.sc, b.sc, t),
            op: lerp(a.op, b.op, t),
            br: lerp(a.br, b.br, t),
            z: Math.round(lerp(a.z, b.z, t))
        };
    }

    function layoutAt(virtualOffset) {
        var clamped = Math.max(-2, Math.min(2, virtualOffset));
        var lo = Math.floor(clamped);
        var hi = Math.ceil(clamped);
        var a = LAYOUT[String(lo)];
        var b = LAYOUT[String(hi)];
        if (!a || !b) return LAYOUT['0'];
        if (lo === hi) return a;
        return lerpState(a, b, clamped - lo);
    }

    function applyState(slide, st) {
        slide.style.transform = 'translateX(' + st.tx + '%) translateZ(' + st.tz +
            'px) rotateY(' + st.ry + 'deg) scale(' + st.sc + ')';
        slide.style.opacity = String(st.op);
        slide.style.filter = 'brightness(' + st.br + ')';
        slide.style.zIndex = String(st.z);
        slide.style.visibility = st.op < 0.06 ? 'hidden' : 'visible';
    }

    function clearSlideStyles(tr) {
        tr.querySelectorAll('.victory-carousel-slide').forEach(function (slide) {
            slide.style.transform = '';
            slide.style.opacity = '';
            slide.style.filter = '';
            slide.style.zIndex = '';
            slide.style.visibility = '';
        });
    }

    function readOffset(slide) {
        var raw = slide.getAttribute('data-victory-offset');
        if (raw !== null && raw !== '') {
            var n = parseInt(raw, 10);
            if (!Number.isNaN(n)) return n;
        }
        if (slide.classList.contains('victory-carousel-slide-active')) return 0;
        if (slide.classList.contains('victory-carousel-slide-prev')) return -1;
        if (slide.classList.contains('victory-carousel-slide-next')) return 1;
        if (slide.classList.contains('victory-carousel-slide-far-prev')) return -2;
        if (slide.classList.contains('victory-carousel-slide-far-next')) return 2;
        return 0;
    }

    function applyDragProgress(root, tr, p) {
        var prev = root.querySelector('.victory-nav-prev:not(:disabled)');
        var next = root.querySelector('.victory-nav-next:not(:disabled)');
        if (!prev && p > 0) p *= EDGE_RESIST;
        if (!next && p < 0) p *= EDGE_RESIST;
        p = Math.max(-1, Math.min(1, p));
        tr.querySelectorAll('.victory-carousel-slide').forEach(function (slide) {
            var base = readOffset(slide);
            var v = base - p;
            applyState(slide, layoutAt(v));
        });
        return p;
    }

    function resetAllTracks() {
        document.querySelectorAll('.victory-carousel-track').forEach(function (tr) {
            tr.classList.remove('victory-carousel-dragging');
            clearSlideStyles(tr);
            var root = tr.closest('.victory-carousel-nav');
            if (root) root.classList.remove('victory-carousel-dragging');
        });
    }

    function scheduleNav(fn) {
        requestAnimationFrame(function () {
            requestAnimationFrame(fn);
        });
    }

    function onPointerDown(e) {
        if (e.pointerType === 'mouse' && e.button !== 0) return;
        if (e.target && e.target.closest && e.target.closest('.victory-nav')) return;
        var root = carouselRoot(e.target);
        if (!root || skipTarget(e.target)) return;
        var tr = track(root);
        var st = stage(root);
        if (!tr || !st) return;
        drag = {
            root: root,
            track: tr,
            stage: st,
            pointerId: e.pointerId,
            startX: e.clientX,
            startY: e.clientY,
            lockHoriz: false,
            progress: 0
        };
        tr.classList.add('victory-carousel-dragging');
        root.classList.add('victory-carousel-dragging');
        try { st.setPointerCapture(e.pointerId); } catch (err) { /* ignore */ }
    }

    function onPointerMove(e) {
        if (!drag || e.pointerId !== drag.pointerId) return;
        var dx = e.clientX - drag.startX;
        var dy = e.clientY - drag.startY;
        if (!drag.lockHoriz) {
            if (Math.abs(dx) < 5 && Math.abs(dy) < 5) return;
            drag.lockHoriz = Math.abs(dx) >= Math.abs(dy) * 0.82;
            if (!drag.lockHoriz) return;
        }
        var w = drag.stage.getBoundingClientRect().width || 320;
        var p = dx / (w * DRAG_WIDTH_RATIO);
        drag.progress = applyDragProgress(drag.root, drag.track, p);
        e.preventDefault();
    }

    function onPointerUp(e) {
        if (!drag || e.pointerId !== drag.pointerId) return;
        var root = drag.root;
        var tr = drag.track;
        var st = drag.stage;
        try { st.releasePointerCapture(e.pointerId); } catch (err) { /* ignore */ }

        tr.classList.remove('victory-carousel-dragging');
        root.classList.remove('victory-carousel-dragging');
        var p = drag.progress;
        var lockHoriz = drag.lockHoriz;
        drag = null;
        clearSlideStyles(tr);

        var prev = root.querySelector('.victory-nav-prev:not(:disabled)');
        var next = root.querySelector('.victory-nav-next:not(:disabled)');
        if (!lockHoriz) return;
        if (p >= THRESH && prev) scheduleNav(function () { prev.click(); });
        else if (p <= -THRESH && next) scheduleNav(function () { next.click(); });
    }

    function onPointerCancel(e) {
        if (!drag || e.pointerId !== drag.pointerId) return;
        clearSlideStyles(drag.track);
        drag.track.classList.remove('victory-carousel-dragging');
        drag.root.classList.remove('victory-carousel-dragging');
        drag = null;
    }

    function install() {
        document.addEventListener('pointerdown', onPointerDown, { passive: false });
        document.addEventListener('pointermove', onPointerMove, { passive: false });
        document.addEventListener('pointerup', onPointerUp, { passive: true });
        document.addEventListener('pointercancel', onPointerCancel, { passive: true });
    }

    w.pokerVictorySwipe = { reset: resetAllTracks };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', install);
    } else {
        install();
    }
})(window);