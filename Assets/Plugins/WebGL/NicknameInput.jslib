mergeInto(LibraryManager.library, {
  $VDNickname__deps: ['$JSEvents'],
  $VDNickname: {
    input: null, resize: null, keyHandlers: [],
    // Unity listens for keys on the window in the capture phase and cancels keypress, which
    // empties the browser field. While it is open, Unity's key handlers skip events aimed at it.
    // This replaces WebGLInput.captureAllKeyboardInput, which Unity 6.3 player scripts cannot reference.
    releaseKeys: function (input) {
      VDNickname.keyHandlers = JSEvents.eventHandlers.filter(function (handler) { return /^key/.test(handler.eventTypeString); });
      VDNickname.keyHandlers.forEach(function (handler) {
        var original = handler.handlerFunc;
        handler.vdOriginal = original;
        handler.handlerFunc = function (event) { if (event.target !== input) original(event); };
      });
    },
    restoreKeys: function () {
      VDNickname.keyHandlers.forEach(function (handler) { if (handler.vdOriginal) { handler.handlerFunc = handler.vdOriginal; handler.vdOriginal = null; } });
      VDNickname.keyHandlers = [];
    },
    close: function () {
      VDNickname.restoreKeys();
      if (VDNickname.resize) window.removeEventListener('resize', VDNickname.resize);
      if (VDNickname.input) VDNickname.input.remove();
      VDNickname.input = null;
      VDNickname.resize = null;
    }
  },
  VD_OpenNickname__deps: ['$VDNickname'],
  VD_OpenNickname: function (target) {
    VDNickname.close();
    var objectName = UTF8ToString(target);
    var input = document.createElement('input');
    input.id = 'vd-nickname'; input.type = 'text'; input.autocomplete = 'off';
    input.setAttribute('aria-label', '닉네임'); input.placeholder = '닉네임 입력';
    input.style.cssText = 'position:fixed;box-sizing:border-box;z-index:9999;background:#1f292b;color:#e6dfcc;border:1px solid #59635f;border-radius:0;outline:none;padding:8px 16px;font-family:sans-serif;';
    input.spellcheck = false;
    var composing = false;
    function send() { SendMessage(objectName, 'BrowserNicknameChanged', input.value); }
    input.addEventListener('compositionstart', function () { composing = true; SendMessage(objectName, 'BrowserNicknameComposition', '1'); });
    input.addEventListener('compositionend', function () { composing = false; send(); SendMessage(objectName, 'BrowserNicknameComposition', '0'); });
    input.addEventListener('input', function () { if (!composing) send(); });
    input.addEventListener('keydown', function (event) {
      event.stopPropagation();
      if (event.key === 'Enter' && !composing && !event.isComposing && event.keyCode !== 229) {
        event.preventDefault(); send(); SendMessage(objectName, 'BrowserNicknameSubmit', input.value);
      }
    });
    VDNickname.resize = function () {
      var bounds = Module.canvas.getBoundingClientRect();
      var scale = Math.min(bounds.width / 900, bounds.height / 1600);
      input.style.left = (bounds.left + (bounds.width - 900 * scale) / 2 + 64 * scale) + 'px';
      input.style.top = (bounds.top + (bounds.height - 1600 * scale) / 2 + 668 * scale) + 'px';
      input.style.width = (772 * scale) + 'px'; input.style.height = (108 * scale) + 'px';
      input.style.fontSize = Math.max(16, 34 * scale) + 'px';
    };
    VDNickname.input = input; document.body.appendChild(input);
    VDNickname.releaseKeys(input);
    window.addEventListener('resize', VDNickname.resize); VDNickname.resize();
    // Focus from the user's tap; automatic focus cannot open mobile keyboards reliably.
  },
  VD_CloseNickname__deps: ['$VDNickname'],
  VD_CloseNickname: function () { VDNickname.close(); }
});

