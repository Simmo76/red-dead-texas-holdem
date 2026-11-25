// Apple-style circular magnifier over HUD cards (top-right window).
// Press or press-and-drag on a card to peek without expanding the panel.
window.pokerCardLoupe = (function () {
  var SIZE = 132;
  var OFFSET_Y = 76;
  var ZOOM = 2.35;
  var DRAG_PX = 10;
  var HOLD_MS = 380;

  var board = null;
  var loupe = null;
  var pointerId = -1;
  var startX = 0;
  var startY = 0;
  var holdTimer = null;
  var active = false;
  var moved = false;
  var suppressClick = false;

  function ensureLoupe() {
    if (loupe) return;
    loupe = document.createElement('div');
    loupe.className = 'card-loupe';
    loupe.setAttribute('aria-hidden', 'true');
    loupe.innerHTML =
      '<div class="card-loupe-ring">'
      + '<div class="card-loupe-viewport">'
      + '<img class="card-loupe-img" alt="" decoding="sync" />'
      + '</div></div>';
    document.body.appendChild(loupe);
  }

  function cardUnder(clientX, clientY) {
    loupe.style.visibility = 'hidden';
    var el = document.elementFromPoint(clientX, clientY);
    loupe.style.visibility = '';
    if (!el || !board || !board.contains(el)) return null;
    return el.closest('.hud-card');
  }

  function setLoupeCard(card) {
    if (!card || !loupe) return;
    var img = loupe.querySelector('.card-loupe-img');
    var viewport = loupe.querySelector('.card-loupe-viewport');
    if (img.getAttribute('data-src') !== card.src) {
      img.src = card.currentSrc || card.src;
      img.setAttribute('data-src', card.src);
    }
    img.classList.toggle('facedown', card.classList.contains('facedown'));
    viewport.style.setProperty('--card-loupe-zoom', String(ZOOM));
  }

  function placeLoupe(clientX, clientY) {
    var half = SIZE / 2;
    loupe.style.transform =
      'translate3d(' + (clientX - half) + 'px,' + (clientY - OFFSET_Y - half) + 'px,0)';
  }

  function showLoupe(clientX, clientY, card) {
    ensureLoupe();
    setLoupeCard(card);
    loupe.classList.add('visible');
    placeLoupe(clientX, clientY);
    active = true;
    document.documentElement.classList.add('card-loupe-active');
  }

  function hideLoupe() {
    if (!loupe) return;
    loupe.classList.remove('visible');
    active = false;
    document.documentElement.classList.remove('card-loupe-active');
  }

  function clearHoldTimer() {
    if (holdTimer) {
      clearTimeout(holdTimer);
      holdTimer = null;
    }
  }

  function onBoardClickCapture(e) {
    if (!suppressClick) return;
    suppressClick = false;
    e.stopPropagation();
    e.preventDefault();
  }

  function blockNativeDrag(e) {
    e.preventDefault();
  }

  function onPointerDown(e) {
    if (!board || e.button > 0) return;
    var card = e.target.closest('.hud-card');
    if (!card) return;

    pointerId = e.pointerId;
    startX = e.clientX;
    startY = e.clientY;
    moved = false;
    suppressClick = false;

    try { board.setPointerCapture(e.pointerId); } catch (err) { /* ignore */ }

    clearHoldTimer();
    holdTimer = setTimeout(function () {
      if (pointerId !== e.pointerId) return;
      showLoupe(e.clientX, e.clientY, card);
      suppressClick = true;
    }, HOLD_MS);
  }

  function onPointerMove(e) {
    if (!board || e.pointerId !== pointerId) return;
    var dx = e.clientX - startX;
    var dy = e.clientY - startY;
    if (Math.hypot(dx, dy) >= DRAG_PX) {
      moved = true;
      clearHoldTimer();
      if (!active) {
        var card = cardUnder(e.clientX, e.clientY) || e.target.closest('.hud-card');
        if (card) {
          showLoupe(e.clientX, e.clientY, card);
          suppressClick = true;
        }
      }
    }
    if (!active) return;
    e.preventDefault();
    var hover = cardUnder(e.clientX, e.clientY);
    if (hover) setLoupeCard(hover);
    placeLoupe(e.clientX, e.clientY);
    suppressClick = true;
  }

  function endPointer(e) {
    if (!board || e.pointerId !== pointerId) return;
    clearHoldTimer();
    pointerId = -1;
    try { board.releasePointerCapture(e.pointerId); } catch (err) { /* ignore */ }
    if (active || moved) suppressClick = true;
    hideLoupe();
  }

  function attach(el) {
    detach();
    if (!el) return;
    board = el;
    board.addEventListener('pointerdown', onPointerDown);
    board.addEventListener('pointermove', onPointerMove);
    board.addEventListener('pointerup', endPointer);
    board.addEventListener('pointercancel', endPointer);
    board.addEventListener('click', onBoardClickCapture, true);
    board.addEventListener('dragstart', blockNativeDrag);
  }

  function detach() {
    if (!board) return;
    board.removeEventListener('pointerdown', onPointerDown);
    board.removeEventListener('pointermove', onPointerMove);
    board.removeEventListener('pointerup', endPointer);
    board.removeEventListener('pointercancel', endPointer);
    board.removeEventListener('click', onBoardClickCapture, true);
    board.removeEventListener('dragstart', blockNativeDrag);
    board = null;
    clearHoldTimer();
    hideLoupe();
    pointerId = -1;
  }

  function bindWhenPresent() {
    var el = document.querySelector('.hud-hand-rail .hud-board');
    if (el && el !== board) attach(el);
    else if (!el && board) detach();
  }

  function watchHud() {
    bindWhenPresent();
    var root = document.getElementById('app');
    if (!root || root._cardLoupeObs) return;
    var obs = new MutationObserver(bindWhenPresent);
    obs.observe(root, { childList: true, subtree: true });
    root._cardLoupeObs = obs;
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', watchHud);
  } else {
    watchHud();
  }

  return { attach: attach, detach: detach, bindWhenPresent: bindWhenPresent };
})();