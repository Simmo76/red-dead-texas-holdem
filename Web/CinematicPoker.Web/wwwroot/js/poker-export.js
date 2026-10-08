// Share / download helpers for the player's session stats CSV export.
// The CSV is sent as a REAL .csv file: on phones/tablets the Web Share API
// opens the system share sheet with the file attached (pick Mail, Messages,
// AirDrop, ...). Where file sharing isn't available (mostly desktop
// browsers), the file is downloaded and the email/SMS composer opens with a
// short note asking to attach it — the spreadsheet is never pasted into the
// message body any more.
window.pokerExport = (function () {
  var FILE_NAME = 'dead-mans-hand-stats.csv';

  function downloadCsv(filename, csv) {
    var blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = filename || FILE_NAME;
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

  // Try to hand the CSV to the system share sheet as an actual file.
  // Returns a result object when the share path was taken (sent or
  // cancelled), or null when this browser can't share files.
  async function shareCsvFile(csv, title) {
    if (typeof File === 'undefined' || !navigator.canShare || !navigator.share) return null;
    var file;
    try {
      file = new File([csv], FILE_NAME, { type: 'text/csv' });
    } catch (e) {
      return null;
    }
    if (!navigator.canShare({ files: [file] })) return null;
    try {
      await navigator.share({ files: [file], title: title || "Dead Man's Hand stats" });
      return { ok: true, shared: true, aborted: false, downloaded: false };
    } catch (err) {
      if (err && err.name === 'AbortError') {
        // The player closed the share sheet — not an error, nothing sent.
        return { ok: false, shared: false, aborted: true, downloaded: false };
      }
      return null; // NotAllowedError etc — fall back to the composer flow
    }
  }

  async function emailCsv(csv, subject) {
    var sharedResult = await shareCsvFile(csv, subject);
    if (sharedResult) return sharedResult;
    // Fallback: download the .csv and open the email composer with a short
    // note (not the raw CSV) asking to attach the downloaded file.
    downloadCsv(FILE_NAME, csv);
    openUri('mailto:?subject=' + encodeURIComponent(subject || "Dead Man's Hand stats") +
      '&body=' + encodeURIComponent(
        "My Dead Man's Hand session stats and coaching notes are in the attached file.\n\n" +
        '(' + FILE_NAME + ' was just downloaded - attach it to this email before sending.)'));
    return { ok: true, shared: false, aborted: false, downloaded: true };
  }

  async function textCsv(csv) {
    var sharedResult = await shareCsvFile(csv, "Dead Man's Hand stats");
    if (sharedResult) return sharedResult;
    // Fallback: download the .csv and open the SMS composer with a short note.
    downloadCsv(FILE_NAME, csv);
    // iOS: sms:&body=  Android: sms:?body= — try the ampersand form first.
    openUri('sms:&body=' + encodeURIComponent(
      "My Dead Man's Hand session stats are in " + FILE_NAME +
      ' (just downloaded) - attaching it now.'));
    return { ok: true, shared: false, aborted: false, downloaded: true };
  }

  return {
    emailCsv: emailCsv,
    textCsv: textCsv,
    downloadCsv: downloadCsv
  };
})();
