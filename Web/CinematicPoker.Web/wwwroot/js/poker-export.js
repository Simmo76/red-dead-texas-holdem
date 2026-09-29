// Share / download helpers for the player's session stats CSV export.
// Opens the device email or SMS composer with the CSV in the message body,
// or falls back to a .csv file download when the body would be too long.
window.pokerExport = (function () {
  // SMS URI length varies by OS; stay conservative so the message isn't truncated.
  var SMS_BODY_LIMIT = 1200;
  // mailto bodies are larger, but some clients still clip around ~2k–8k.
  var MAIL_BODY_LIMIT = 6000;

  function downloadCsv(filename, csv) {
    var blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = filename || 'cinematic-poker-stats.csv';
    a.rel = 'noopener';
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(function () { URL.revokeObjectURL(url); }, 1500);
  }

  function openUri(uri) {
    // Prefer a temporary anchor so iOS Safari reliably hands off to Mail/Messages.
    var a = document.createElement('a');
    a.href = uri;
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    a.remove();
  }

  function emailCsv(csv, subject) {
    var sub = subject || 'Cinematic Poker stats';
    var body = csv;
    var truncated = false;
    if (body.length > MAIL_BODY_LIMIT) {
      body = body.slice(0, MAIL_BODY_LIMIT) +
        '\n\n…(CSV truncated for email; download the full file from the table.)\n';
      truncated = true;
      downloadCsv('cinematic-poker-stats.csv', csv);
    }
    openUri('mailto:?subject=' + encodeURIComponent(sub) +
      '&body=' + encodeURIComponent(body));
    return { ok: true, truncated: truncated, downloaded: truncated };
  }

  function textCsv(csv) {
    var body = csv;
    var truncated = false;
    if (body.length > SMS_BODY_LIMIT) {
      // Keep a short summary in SMS and download the full CSV for attaching.
      var lines = csv.split(/\r?\n/).filter(Boolean);
      var summary = lines.slice(0, 12).join('\n');
      body = 'Cinematic Poker stats (CSV attached via Downloads):\n\n' +
        summary +
        '\n\n…full CSV saved as cinematic-poker-stats.csv';
      truncated = true;
      downloadCsv('cinematic-poker-stats.csv', csv);
    }
    // iOS: sms:&body=  Android: sms:?body= — try the ampersand form first.
    openUri('sms:&body=' + encodeURIComponent(body));
    return { ok: true, truncated: truncated, downloaded: truncated };
  }

  return {
    emailCsv: emailCsv,
    textCsv: textCsv,
    downloadCsv: downloadCsv
  };
})();
