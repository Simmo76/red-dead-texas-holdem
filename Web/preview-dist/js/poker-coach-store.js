// Local-only coach settings and sanitized hand history. Quota and privacy
// errors must not break the table; the C# side keeps an in-memory copy.
window.pokerCoachStore = {
  loadMode() {
    try { return localStorage.getItem('cinematic-poker-coach-mode') || 'request'; }
    catch (e) { return 'request'; }
  },
  saveMode(mode) {
    try { localStorage.setItem('cinematic-poker-coach-mode', mode); return true; }
    catch (e) { return false; }
  },
  loadHistory() {
    try { return localStorage.getItem('cinematic-poker-coach-history') || ''; }
    catch (e) { return ''; }
  },
  saveHistory(json) {
    try { localStorage.setItem('cinematic-poker-coach-history', json); return true; }
    catch (e) { return false; }
  },
  loadTip() {
    try { return localStorage.getItem('cinematic-poker-coach-tip') || '2'; }
    catch (e) { return '2'; }
  },
  saveTip(n) {
    try { localStorage.setItem('cinematic-poker-coach-tip', String(n)); return true; }
    catch (e) { return false; }
  },
  prefersReducedMotion() {
    return !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
  },
  loadMetrics() {
    try { return localStorage.getItem('cinematic-poker-coach-metrics') || ''; }
    catch (e) { return ''; }
  },
  saveMetrics(text) {
    try { localStorage.setItem('cinematic-poker-coach-metrics', text || ''); return true; }
    catch (e) { return false; }
  },
  bindEscape(dotNet) {
    if (this._escapeBound) return;
    this._escapeBound = true;
    window.addEventListener('keydown', function (e) {
      if (e.key !== 'Escape') return;
      if (!document.querySelector('.coach-layer')) return;
      e.preventDefault();
      dotNet.invokeMethodAsync('CloseCoachFromKey');
    }, true);
  }
};
