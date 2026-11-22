// Victory Cover Flow: horizontal swipe snaps to previous/next win (no live drag).
(function (w) {
    var THRESH = 0.26;
    var DRAG_WIDTH_RATIO = 0.55;

    var drag = null;

    function carouselRoot(el) {
        return el && el.closest ? el.closest('.victory-carousel-nav') : null;
    }

    function stage(root) {
        return root ? root.querySelector('.victory-swipe-stage, .victory-carousel-viewport') : null;
    }

    function skipTarget(t) {
        return t && t.closest && t.closest(
            'button, a, input, textarea, select, .victory-nav, .victory-shot, .cert-coach');
    }

    function resetAllTracks() {
        document.querySelectorAll('.victory-carousel-track').forEach(function (tr) {
            tr.classList.remove('victory-carousel-dragging');
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
        var st = stage(root);
        if (!st) return;
        drag = {
            root: root,
            stage: st,
            pointerId: e.pointerId,
            startX: e.clientX,
            startY: e.clientY,
            lockHoriz: false
        };
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
        e.preventDefault();
    }

    function onPointerUp(e) {
        if (!drag || e.pointerId !== drag.pointerId) return;
        var root = drag.root;
        var st = drag.stage;
        try { st.releasePointerCapture(e.pointerId); } catch (err) { /* ignore */ }

        var dx = e.clientX - drag.startX;
        var lockHoriz = drag.lockHoriz;
        drag = null;

        var prev = root.querySelector('.victory-nav-prev:not(:disabled)');
        var next = root.querySelector('.victory-nav-next:not(:disabled)');
        if (!lockHoriz) return;
        var wPx = st.getBoundingClientRect().width || 320;
        var p = dx / (wPx * DRAG_WIDTH_RATIO);
        if (p >= THRESH && prev) scheduleNav(function () { prev.click(); });
        else if (p <= -THRESH && next) scheduleNav(function () { next.click(); });
    }

    function onPointerCancel(e) {
        if (!drag || e.pointerId !== drag.pointerId) return;
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