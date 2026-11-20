// Victory carousel: live horizontal drag (touch / pen / mouse) with snap on release.
(function (w) {
    var THRESH = 48;
    var MAX_DRAG_RATIO = 0.45;
    var EDGE_RESIST = 0.3;

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

    function clampDx(root, dx) {
        var st = stage(root);
        var w = st ? st.getBoundingClientRect().width : 320;
        var max = w * MAX_DRAG_RATIO;
        var prev = root.querySelector('.victory-nav-prev:not(:disabled)');
        var next = root.querySelector('.victory-nav-next:not(:disabled)');
        if (!prev && dx > 0) dx *= EDGE_RESIST;
        if (!next && dx < 0) dx *= EDGE_RESIST;
        return Math.max(-max, Math.min(max, dx));
    }

    function setDragX(tr, px) {
        tr.style.setProperty('--victory-drag-x', px + 'px');
    }

    function clearDrag(root) {
        if (!root) return;
        var tr = track(root);
        if (!tr) return;
        tr.classList.remove('victory-carousel-dragging');
        root.classList.remove('victory-carousel-dragging');
        setDragX(tr, 0);
    }

    function onPointerDown(e) {
        if (e.pointerType === 'mouse' && e.button !== 0) return;
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
            lockHoriz: false
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
            if (Math.abs(dx) < 6 && Math.abs(dy) < 6) return;
            drag.lockHoriz = Math.abs(dx) >= Math.abs(dy) * 0.85;
            if (!drag.lockHoriz) return;
        }
        dx = clampDx(drag.root, dx);
        setDragX(drag.track, dx);
        e.preventDefault();
    }

    function onPointerUp(e) {
        if (!drag || e.pointerId !== drag.pointerId) return;
        var root = drag.root;
        var tr = drag.track;
        var st = drag.stage;
        var dx = e.clientX - drag.startX;
        var dy = e.clientY - drag.startY;
        try { st.releasePointerCapture(e.pointerId); } catch (err) { /* ignore */ }

        tr.classList.remove('victory-carousel-dragging');
        root.classList.remove('victory-carousel-dragging');
        setDragX(tr, 0);

        var prev = root.querySelector('.victory-nav-prev:not(:disabled)');
        var next = root.querySelector('.victory-nav-next:not(:disabled)');
        if (drag.lockHoriz && Math.abs(dx) >= THRESH && Math.abs(dx) > Math.abs(dy) * 1.2) {
            if (dx > 0 && prev) prev.click();
            else if (dx < 0 && next) next.click();
        }
        drag = null;
    }

    function onPointerCancel(e) {
        if (!drag || e.pointerId !== drag.pointerId) return;
        clearDrag(drag.root);
        drag = null;
    }

    function install() {
        document.addEventListener('pointerdown', onPointerDown, { passive: false });
        document.addEventListener('pointermove', onPointerMove, { passive: false });
        document.addEventListener('pointerup', onPointerUp, { passive: true });
        document.addEventListener('pointercancel', onPointerCancel, { passive: true });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', install);
    } else {
        install();
    }
})(window);