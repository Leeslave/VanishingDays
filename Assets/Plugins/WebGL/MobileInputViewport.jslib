mergeInto(LibraryManager.library, {
  $VDMobileInputViewport: {
    installed: false,
    enabled: false,
    timers: [],
    editing: false,
    isUnityInput: function (element) {
      return typeof mobile_input !== "undefined" && mobile_input && mobile_input.input === element;
    },
    restore: function () {
      var state = VDMobileInputViewport;
      if (!state.enabled) return;
      state.timers.forEach(function (timer) { clearTimeout(timer); });
      state.timers = [];
      // Unity removes its DOM input after a 200ms delay; the keyboard animation
      // can take longer. Recheck focus each time so a new edit is never interrupted.
      [0, 250, 600].forEach(function (delay) {
        state.timers.push(setTimeout(function () {
          var active = document.activeElement;
          if (active && (active.tagName === "INPUT" || active.tagName === "TEXTAREA")) return;
          window.scrollTo(0, 0);
          window.dispatchEvent(new Event("resize"));
        }, delay));
      });
    },
    install: function () {
      var state = VDMobileInputViewport;
      if (state.installed) return;
      state.installed = true;
      if (!navigator.maxTouchPoints && !/Android|iPhone|iPad|iPod/i.test(navigator.userAgent)) return;
      state.enabled = true;

      // Unity 6000.3's MobileKeyboard.js sets no font on its input. The browser's
      // smaller default triggers iOS focus zoom. CSS must exist BEFORE input.focus().
      // Unity's keyboard is a direct child of a div appended to document.body.
      var style = document.createElement("style");
      style.id = "vd-mobile-keyboard-style";
      style.textContent = "body > div > input, body > div > textarea { font-size: 16px !important; }";
      document.head.appendChild(style);

      // The default Unity template disables zoom. Allow pinch zoom so users can
      // also adjust their view, without relying on zoom restrictions to fix input.
      var viewport = document.querySelector('meta[name="viewport"]');
      if (viewport) {
        var directives = (viewport.content || "").split(",").filter(function (directive) {
          return !/^\s*(maximum-scale|minimum-scale|user-scalable)\s*=/i.test(directive);
        });
        viewport.content = directives.join(",");
      }
      document.addEventListener("focusin", function (event) {
        if (!state.isUnityInput(event.target)) return;
        state.editing = true;
        state.timers.forEach(function (timer) { clearTimeout(timer); });
        state.timers = [];
      }, true);
      document.addEventListener("focusout", function (event) {
        if (!state.editing || !state.isUnityInput(event.target)) return;
        state.editing = false;
        state.restore();
      }, true);
    }
  },
  VD_InstallMobileInputViewport__deps: ["$VDMobileInputViewport", "$mobile_input"],
  VD_InstallMobileInputViewport: function () {
    VDMobileInputViewport.install();
  },
  VD_RestoreMobileInputViewport__deps: ["$VDMobileInputViewport", "$mobile_input"],
  VD_RestoreMobileInputViewport: function () {
    VDMobileInputViewport.restore();
  }
});
