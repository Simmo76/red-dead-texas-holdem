// Share / download helpers for the player's session stats CSV export.
// Designed to never throw into Blazor interop — every entry point returns
 // a result object. Prefers the Web Share API (real Mail/Messages handoff
// on phones), then mailto/sms with a short body + file download.
window.pokerExport = (function () {
  var SMS_BODY_LIMIT = 800;
  var MAIL_BODY_LIMIT = 1800;

  function result(partial) {
    return Object.assign({ ok: false, shared: false, downloaded: false, copied: false, method: '', error: '' }, partial || {});
  }

  function downloadCsv(filename, csv) {
    var name = filename || 'cinematic-poker-stats.csv';
    try {
      if (navigator.msSaveOrOpenBlob) {
        navigator.msSaveOrOpenBlob(new Blob([csv], { type: 'text/csv;charset=utf-8;' }), name);
        return result({ ok: true, downloaded: true, method: 'msSaveBlob' });
      }
    } catch (e) { /* fall through */ }

    try {
      var blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
      var url = URL.createObjectURL(blob);
      var a = document.createElement('a');
      a.href = url;
      a.download = name;
      a.rel = 'noopener';
      a.style.display = 'none';
      document.body.appendChild(a);
      a.click();
      setTimeout(function () {
        try { a.remove(); } catch (e) { }
        try { URL.revokeObjectURL(url); } catch (e) { }
      }, 2000);
      return result({ ok: true, downloaded: true, method: 'blob' });
    } catch (e) { /* fall through to data-URI */ }

    try {
      // data: URI fallback — works in some WebViews that block blob downloads.
      var dataUrl = 'data:text/csv;charset=utf-8,' + encodeURIComponent(csv);
      var a2 = document.createElement('a');
      a2.href = dataUrl;
      a2.download = name;
      a2.rel = 'noopener';
      a2.style.display = 'none';
      document.body.appendChild(a2);
      a2.click();
      setTimeout(function () { try { a2.remove(); } catch (e) { } }, 2000);
      return result({ ok: true, downloaded: true, method: 'dataUri' });
    } catch (e) {
      return result({ ok: false, error: (e && e.message) || 'download blocked' });
    }
  }

  function copyText(text) {
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        // Fire-and-forget; clipboard may require a secure context.
        navigator.clipboard.writeText(text).catch(function () { });
        return true;
      }
    } catch (e) { }
    try {
      var ta = document.createElement('textarea');
      ta.value = text;
      ta.setAttribute('readonly', '');
      ta.style.position = 'fixed';
      ta.style.left = '-9999px';
      document.body.appendChild(ta);
      ta.select();
      var ok = document.execCommand('copy');
      ta.remove();
      return !!ok;
    } catch (e) {
      return false;
    }
  }

  function openUri(uri) {
    try {
      // Never assign mailto:/sms: to the current page — that unloads the game.
      // A temporary anchor click keeps the Blazor WASM session alive.
      var a = document.createElement('a');
      a.href = uri;
      a.rel = 'noopener';
      a.style.display = 'none';
      document.body.appendChild(a);
      a.click();
      setTimeout(function () { try { a.remove(); } catch (e) { } }, 0);
      return true;
    } catch (e) {
      return false;
    }
  }

  function shortBody(csv, limit, label) {
    var lines = String(csv || '').split(/\r?\n/).filter(function (l) { return l.length > 0; });
    var summary = lines.slice(0, 10).join('\n');
    var body = (label || 'Cinematic Poker stats') + '\n\n' + summary;
    if (body.length > limit) body = body.slice(0, limit - 20) + '\n…';
    body += '\n\n(Full CSV also saved as cinematic-poker-stats.csv)';
    return body;
  }

  function canShareFiles() {
    try {
      if (!navigator.share || !navigator.canShare) return false;
      var probe = new File(['x'], 't.csv', { type: 'text/csv' });
      return navigator.canShare({ files: [probe] });
    } catch (e) {
      return false;
    }
  }

  function shareCsv(csv, title) {
    var name = 'cinematic-poker-stats.csv';
    var sub = title || 'Cinematic Poker stats';
    try {
      if (canShareFiles()) {
        var file = new File([csv], name, { type: 'text/csv' });
        // Return a Promise so Blazor can await the share sheet.
        return navigator.share({
          files: [file],
          title: sub,
          text: sub
        }).then(function () {
          return result({ ok: true, shared: true, method: 'webShareFiles' });
        }).catch(function (e) {
          // User cancelled share — still offer a download so they aren't stuck.
          var aborted = e && (e.name === 'AbortError' || /abort|cancel/i.test(e.message || ''));
          var dl = downloadCsv(name, csv);
          return result({
            ok: true,
            shared: false,
            downloaded: dl.downloaded,
            method: aborted ? 'shareCancelled' : 'shareFailed',
            error: aborted ? '' : ((e && e.message) || '')
          });
        });
      }
      if (navigator.share) {
        var body = shortBody(csv, MAIL_BODY_LIMIT, sub);
        return navigator.share({ title: sub, text: body }).then(function () {
          var dl = downloadCsv(name, csv);
          return result({ ok: true, shared: true, downloaded: dl.downloaded, method: 'webShareText' });
        }).catch(function (e) {
          var aborted = e && (e.name === 'AbortError' || /abort|cancel/i.test(e.message || ''));
          var dl = downloadCsv(name, csv);
          return result({
            ok: true,
            shared: false,
            downloaded: dl.downloaded,
            method: aborted ? 'shareCancelled' : 'shareFailed',
            error: aborted ? '' : ((e && e.message) || '')
          });
        });
      }
    } catch (e) {
      // Fall through to non-share path.
    }
    var dl2 = downloadCsv(name, csv);
    var copied = copyText(csv);
    return Promise.resolve(result({
      ok: dl2.downloaded || copied,
      downloaded: dl2.downloaded,
      copied: copied,
      method: 'downloadFallback',
      error: dl2.downloaded || copied ? '' : (dl2.error || 'share unavailable')
    }));
  }

  function emailCsv(csv, subject) {
    try {
      var sub = subject || 'Cinematic Poker stats';
      // On phones, the share sheet is the reliable way to reach Mail.
      if (canShareFiles()) {
        return shareCsv(csv, sub).then(function (r) {
          if (r && r.shared) return r;
          return emailCsvFallback(csv, sub);
        });
      }
      return Promise.resolve(emailCsvFallback(csv, sub));
    } catch (e) {
      var dl2 = downloadCsv('cinematic-poker-stats.csv', csv);
      return Promise.resolve(result({
        ok: dl2.downloaded,
        downloaded: dl2.downloaded,
        method: 'emailError',
        error: (e && e.message) || 'email failed'
      }));
    }
  }

  function emailCsvFallback(csv, sub) {
    var dl = downloadCsv('cinematic-poker-stats.csv', csv);
    var body = shortBody(csv, MAIL_BODY_LIMIT, sub);
    var uri = 'mailto:?subject=' + encodeURIComponent(sub) +
      '&body=' + encodeURIComponent(body);
    var opened = openUri(uri);
    var copied = copyText(csv);
    return result({
      ok: opened || dl.downloaded || copied,
      downloaded: dl.downloaded,
      copied: copied,
      method: opened ? 'mailto' : 'mailtoBlocked',
      error: opened || dl.downloaded ? '' : 'email composer unavailable'
    });
  }

  function textCsv(csv) {
    try {
      // On phones, share sheet reaches Messages more reliably than sms: URIs.
      if (canShareFiles()) {
        return shareCsv(csv, 'Cinematic Poker stats').then(function (r) {
          if (r && r.shared) return r;
          return textCsvFallback(csv);
        });
      }
      return Promise.resolve(textCsvFallback(csv));
    } catch (e) {
      var dl2 = downloadCsv('cinematic-poker-stats.csv', csv);
      return Promise.resolve(result({
        ok: dl2.downloaded,
        downloaded: dl2.downloaded,
        method: 'smsError',
        error: (e && e.message) || 'text failed'
      }));
    }
  }

  function textCsvFallback(csv) {
    var dl = downloadCsv('cinematic-poker-stats.csv', csv);
    var body = shortBody(csv, SMS_BODY_LIMIT, 'Cinematic Poker stats');
    // Android prefers sms:?body= ; iOS accepts sms:&body=.
    var opened = openUri('sms:?body=' + encodeURIComponent(body));
    if (!opened) opened = openUri('sms:&body=' + encodeURIComponent(body));
    var copied = copyText(csv);
    return result({
      ok: opened || dl.downloaded || copied,
      downloaded: dl.downloaded,
      copied: copied,
      method: opened ? 'sms' : 'smsBlocked',
      error: opened || dl.downloaded ? '' : 'messages app unavailable'
    });
  }

  // Flat aliases so Blazor can call either nested or top-level identifiers.
  window.pokerExportDownloadCsv = downloadCsv;
  window.pokerExportEmailCsv = emailCsv;
  window.pokerExportTextCsv = textCsv;
  window.pokerExportShareCsv = shareCsv;

  return {
    downloadCsv: downloadCsv,
    emailCsv: emailCsv,
    textCsv: textCsv,
    shareCsv: shareCsv
  };
})();
