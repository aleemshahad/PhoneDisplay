(function () {
  'use strict';

  var canvas = document.getElementById('view');
  var ctx = canvas.getContext('2d', { alpha: false, desynchronized: true });
  var dot = document.getElementById('dot');
  var label = document.getElementById('text');
  var fpsLabel = document.getElementById('fps');

  var params = new URLSearchParams(window.location.search);
  var host = params.get('host') || window.location.hostname || '127.0.0.1';
  var port = params.get('port') || (window.location.port || '8090');

  var socket = null;
  var pending = null;
  var shown = null;
  var retry = 0;
  var closing = false;
  var frames = 0;
  var lastFpsAt = Date.now();
  var lastFrameAt = 0;
  var wakeLock = null;

  function setStatus(text, live) {
    label.textContent = text;
    dot.className = live ? 'live' : '';
  }

  function resolveSize() {
    var dpr = Math.min(window.devicePixelRatio || 1, 2);
    var w = Math.max(1, Math.round(window.innerWidth * dpr));
    var h = Math.max(1, Math.round(window.innerHeight * dpr));
    if (canvas.width !== w || canvas.height !== h) {
      canvas.width = w;
      canvas.height = h;
    }
  }

  function draw() {
    if (pending) {
      var next = pending;
      pending = null;
      if (shown && shown.close) {
        shown.close();
      }
      shown = next;

      resolveSize();

      var scale = Math.min(canvas.width / next.width, canvas.height / next.height);
      var drawWidth = Math.round(next.width * scale);
      var drawHeight = Math.round(next.height * scale);
      var offsetX = Math.round((canvas.width - drawWidth) / 2);
      var offsetY = Math.round((canvas.height - drawHeight) / 2);

      ctx.fillStyle = '#000';
      ctx.fillRect(0, 0, canvas.width, canvas.height);
      ctx.drawImage(next, offsetX, offsetY, drawWidth, drawHeight);

      frames++;
      lastFrameAt = Date.now();
    }

    requestAnimationFrame(draw);
  }

  function scheduleReconnect() {
    if (closing) {
      return;
    }
    retry = Math.min(retry + 1, 8);
    var wait = Math.min(500 * retry, 5000);
    setStatus('reconnecting in ' + Math.round(wait / 1000) + 's', false);
    window.setTimeout(connect, wait);
  }

  function connect() {
    if (closing) {
      return;
    }

    var url = 'ws://' + host + ':' + port + '/ws';
    setStatus('connecting to ' + host, false);

    try {
      socket = new WebSocket(url);
    } catch (err) {
      scheduleReconnect();
      return;
    }

    socket.binaryType = 'arraybuffer';

    socket.onopen = function () {
      retry = 0;
      setStatus('live - ' + host, true);
      requestWakeLock();
    };

    socket.onmessage = function (event) {
      if (typeof event.data === 'string' || event.data.byteLength === 0) {
        return;
      }
      var blob = new Blob([event.data], { type: 'image/jpeg' });
      createImageBitmap(blob).then(function (bitmap) {
        pending = bitmap;
      }).catch(function () {});
    };

    socket.onerror = function () {};

    socket.onclose = function () {
      socket = null;
      setStatus('disconnected', false);
      releaseWakeLock();
      scheduleReconnect();
    };
  }

  function requestWakeLock() {
    if (!('wakeLock' in navigator)) {
      return;
    }
    navigator.wakeLock.request('screen').then(function (lock) {
      wakeLock = lock;
    }).catch(function () {});
  }

  function releaseWakeLock() {
    if (wakeLock) {
      try {
        wakeLock.release();
      } catch (err) {}
      wakeLock = null;
    }
  }

  document.addEventListener('visibilitychange', function () {
    if (document.visibilityState === 'visible' && socket && socket.readyState === 1 && !wakeLock) {
      requestWakeLock();
    }
  });

  window.addEventListener('resize', resolveSize);
  window.addEventListener('orientationchange', resolveSize);

  window.addEventListener('touchstart', function () {
    document.body.classList.toggle('clean');
  }, { passive: true });

  window.addEventListener('click', function () {
    document.body.classList.toggle('clean');
  });

  window.setInterval(function () {
    var now = Date.now();
    var elapsed = now - lastFpsAt;

    if (elapsed >= 1000) {
      var rate = Math.round((frames * 1000) / elapsed);
      var stale = now - lastFrameAt > 4000;
      fpsLabel.textContent = stale ? 'no frames' : rate + ' fps';
      frames = 0;
      lastFpsAt = now;
    }
  }, 500);

  window.addEventListener('beforeunload', function () {
    closing = true;
    releaseWakeLock();
    if (socket) {
      socket.close();
    }
  });

  resolveSize();
  draw();
  connect();
})();