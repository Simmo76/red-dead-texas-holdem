// In-memory diagnostic log players can copy into Cursor / a bug report.
//
// Must load BEFORE blazor.webassembly.js and the scene module: it wraps the
// console so everything the app prints (C# Console.WriteLine / Console.Error
// included) lands in the log, and it records uncaught errors and unhandled
// promise rejections. The log is a bounded ring buffer — it never grows
// without limit and is never sent anywhere; it only leaves the device when
// the player taps COPY LOG.
window.pokerLog = (function () {
  var MAX_ENTRIES = 1500;
  var MAX_MESSAGE_CHARS = 2000;
  var entries = [];
  var dropped = 0;
  var startedAt = Date.now();
  var inHook = false;

  function pad(n, width) {
    var s = String(n);
    while (s.length < width) s = '0' + s;
    return s;
  }

  function stamp(ms) {
    var d = new Date(ms);
    return pad(d.getHours(), 2) + ':' + pad(d.getMinutes(), 2) + ':' + pad(d.getSeconds(), 2) +
      '.' + pad(d.getMilliseconds(), 3);
  }

  function stringify(arg) {
    if (arg instanceof Error) return arg.stack || (arg.name + ': ' + arg.message);
    if (typeof arg === 'string') return arg;
    if (arg === undefined) return 'undefined';
    if (typeof arg === 'function') return '[function ' + (arg.name || 'anonymous') + ']';
    try {
      var json = JSON.stringify(arg);
      return json === undefined ? String(arg) : json;
    } catch (e) {
      return String(arg);
    }
  }

  function add(level, source, message) {
    var text = String(message);
    if (text.length > MAX_MESSAGE_CHARS) {
      text = text.slice(0, MAX_MESSAGE_CHARS) + '… [' + (text.length - MAX_MESSAGE_CHARS) + ' more chars]';
    }
    entries.push({ t: Date.now(), level: level, source: source, text: text });
    if (entries.length > MAX_ENTRIES) {
      entries.shift();
      dropped++;
    }
  }

  // C# writes "[source] message" lines; lift the tag out so the log is easy
  // to scan, and let "[game]" / "[ui]" style tags double as the source.
  function addFromConsole(level, args) {
    var parts = [];
    for (var i = 0; i < args.length; i++) parts.push(stringify(args[i]));
    var text = parts.join(' ');
    var source = 'console';
    var m = /^\[([a-z][a-z0-9_.-]{0,20})\]\s?([\s\S]*)$/i.exec(text);
    if (m) {
      source = m[1].toLowerCase();
      text = m[2];
    }
    add(level, source, text);
  }

  function hookConsole(method, level) {
    var original = console[method];
    if (typeof original !== 'function') return;
    console[method] = function () {
      if (!inHook) {
        inHook = true;
        try { addFromConsole(level, arguments); } catch (e) { }
        inHook = false;
      }
      return original.apply(console, arguments);
    };
  }

  hookConsole('log', 'info');
  hookConsole('info', 'info');
  hookConsole('debug', 'debug');
  hookConsole('warn', 'warn');
  hookConsole('error', 'error');

  window.addEventListener('error', function (ev) {
    var where = ev.filename ? ' at ' + ev.filename + ':' + ev.lineno + ':' + ev.colno : '';
    var stack = ev.error && ev.error.stack ? '\n' + ev.error.stack : '';
    add('error', 'window', (ev.message || 'Script error') + where + stack);
  });

  window.addEventListener('unhandledrejection', function (ev) {
    add('error', 'promise', 'Unhandled rejection: ' + stringify(ev.reason));
  });

  document.addEventListener('visibilitychange', function () {
    add('info', 'page', 'visibility ' + document.visibilityState);
  });
  window.addEventListener('online', function () { add('info', 'page', 'network online'); });
  window.addEventListener('offline', function () { add('warn', 'page', 'network offline'); });

  function glRenderer() {
    try {
      var c = document.createElement('canvas');
      var gl = c.getContext('webgl2') || c.getContext('webgl');
      if (!gl) return 'no WebGL';
      var ext = gl.getExtension('WEBGL_debug_renderer_info');
      return ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER);
    } catch (e) {
      return 'unknown';
    }
  }

  function header() {
    var lines = [
      '# Dead Man\'s Hand debug log',
      'copied: ' + new Date().toISOString(),
      'page: ' + location.href.split('#')[0],
      'user agent: ' + navigator.userAgent,
      'screen: ' + screen.width + 'x' + screen.height + ' @' + (window.devicePixelRatio || 1) + 'x, ' +
        'viewport: ' + window.innerWidth + 'x' + window.innerHeight,
      'gpu: ' + glRenderer(),
      'online: ' + navigator.onLine,
      'session length: ' + Math.round((Date.now() - startedAt) / 1000) + 's',
      'entries: ' + entries.length + (dropped ? ' (' + dropped + ' older entries dropped)' : ''),
      'format: HH:MM:SS.mmm LEVEL [source] message',
      ''
    ];
    return lines.join('\n');
  }

  function getText() {
    var out = [header()];
    for (var i = 0; i < entries.length; i++) {
      var e = entries[i];
      out.push(stamp(e.t) + ' ' + e.level.toUpperCase() + ' [' + e.source + '] ' + e.text);
    }
    return out.join('\n');
  }

  function fallbackCopy(text) {
    var ta = document.createElement('textarea');
    ta.value = text;
    ta.setAttribute('readonly', '');
    ta.style.cssText = 'position:fixed;top:0;left:0;opacity:0;';
    document.body.appendChild(ta);
    ta.select();
    ta.setSelectionRange(0, text.length);
    var ok = false;
    try { ok = document.execCommand('copy'); } catch (e) { ok = false; }
    ta.remove();
    return ok;
  }

  // Resolves to { ok, entries, chars }. Never rejects.
  async function copy() {
    var text = getText();
    var ok = false;
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        await navigator.clipboard.writeText(text);
        ok = true;
      }
    } catch (e) {
      ok = false;
    }
    if (!ok) ok = fallbackCopy(text);
    add(ok ? 'info' : 'warn', 'log', ok ? 'log copied to clipboard' : 'log copy failed');
    return { ok: ok, entries: entries.length, chars: text.length };
  }

  add('info', 'log', 'log started');

  return {
    add: add,
    getText: getText,
    copy: copy,
    count: function () { return entries.length; },
    clear: function () { entries.length = 0; dropped = 0; }
  };
})();
