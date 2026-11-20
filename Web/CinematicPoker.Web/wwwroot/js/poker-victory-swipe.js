// Horizontal swipe on the victory certificate (complements Blazor touch
// handlers; passive:false on move so iOS treats the gesture as a carousel).
(function (w) {
    var THRESH = 40;
    var startX = 0;
    var startY = 0;
    var tracking = false;

    function onStart(e) {
        var row = e.target && e.target.closest ? e.target.closest('.victory-carousel-nav') : null;
        if (!row) return;
        var t = e.touches && e.touches[0];
        if (!t) return;
        startX = t.clientX;
        startY = t.clientY;
        tracking = true;
    }

    function onMove(e) {
        if (!tracking || !e.touches || !e.touches[0]) return;
        var dx = e.touches[0].clientX - startX;
        var dy = e.touches[0].clientY - startY;
        if (Math.abs(dx) > 12 && Math.abs(dx) > Math.abs(dy)) {
            e.preventDefault();
        }
    }

    function onEnd(e) {
        if (!tracking) return;
        tracking = false;
        var row = e.target && e.target.closest ? e.target.closest('.victory-carousel-nav') : null;
        if (!row) return;
        var t = e.changedTouches && e.changedTouches[0];
        if (!t) return;
        var dx = t.clientX - startX;
        var dy = t.clientY - startY;
        if (Math.abs(dx) < THRESH || Math.abs(dx) < Math.abs(dy) * 1.2) return;
        var prev = row.querySelector('.victory-nav-prev:not(:disabled)');
        var next = row.querySelector('.victory-nav-next:not(:disabled)');
        if (dx > 0 && prev) prev.click();
        else if (dx < 0 && next) next.click();
    }

    function install() {
        document.addEventListener('touchstart', onStart, { passive: true });
        document.addEventListener('touchmove', onMove, { passive: false });
        document.addEventListener('touchend', onEnd, { passive: true });
        document.addEventListener('touchcancel', onEnd, { passive: true });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', install);
    } else {
        install();
    }
})(window);