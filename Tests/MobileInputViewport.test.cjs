const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

// Execute the project's Emscripten plugin against keyboard focus/removal events,
// including delayed close and starting another edit before restoration completes.
function browser(mobile = true) {
  const listeners = {};
  const styles = [];
  const viewport = { content: 'width=device-width, height=device-height, initial-scale=1.0, user-scalable=no, maximum-scale=1' };
  const document = {
    activeElement: null,
    createElement: tag => ({ tagName: tag.toUpperCase() }),
    head: { appendChild: style => styles.push(style) },
    querySelector: () => viewport,
    addEventListener: (type, fn) => (listeners[type] ||= []).push(fn)
  };
  let nextId = 1;
  const timers = new Map();
  const scrolls = [];
  const events = [];
  const context = vm.createContext({
    document,
    navigator: { maxTouchPoints: mobile ? 5 : 0, userAgent: mobile ? 'iPhone' : 'Windows' },
    window: { scrollTo: (...args) => scrolls.push(args), dispatchEvent: event => events.push(event.type) },
    Event: function(type) { this.type = type; },
    LibraryManager: { library: {} },
    mergeInto: (target, source) => Object.assign(target, source),
    setTimeout: (fn, delay) => { const id = nextId++; timers.set(id, { fn, delay }); return id; },
    clearTimeout: id => timers.delete(id),
    mobile_input: null
  });
  vm.runInContext(fs.readFileSync(path.join(__dirname, '../Assets/Plugins/WebGL/MobileInputViewport.jslib'), 'utf8'), context);
  const library = context.LibraryManager.library;
  context.VDMobileInputViewport = library.$VDMobileInputViewport;
  const fire = (type, target) => (listeners[type] || []).forEach(fn => fn({ target }));
  const focus = input => { context.mobile_input = { input }; document.activeElement = input; fire('focusin', input); };
  const close = input => { document.activeElement = null; fire('focusout', input); context.mobile_input = null; };
  const flush = () => {
    const pending = [...timers.entries()].sort((a,b) => a[1].delay - b[1].delay);
    pending.forEach(([id, timer]) => { if (timers.delete(id)) timer.fn(); });
  };
  return { library, context, document, viewport, styles, focus, close, fire, flush, scrolls, events, timers };
}

const mobile = browser();
mobile.library.VD_InstallMobileInputViewport();
mobile.library.VD_InstallMobileInputViewport();
assert.equal(mobile.styles.length, 1, 'Install must be idempotent');
assert.match(mobile.styles[0].textContent, /input.*textarea.*font-size: 16px !important/s, 'Prevent focus zoom before the DOM input is created');
assert.doesNotMatch(mobile.viewport.content, /user-scalable|maximum-scale|minimum-scale/, 'Keep manual pinch zoom available');
assert.match(mobile.viewport.content, /initial-scale=1.0/, 'Preserve initial viewport size');
const input = { tagName: 'INPUT' };
mobile.focus(input);
mobile.library.VD_RestoreMobileInputViewport();
mobile.flush();
assert.equal(mobile.scrolls.length, 0, 'Never restore while the user is still typing');
mobile.close(input);
mobile.flush();
assert.equal(mobile.scrolls.length, 3, 'Restore after keyboard close, including animation delay');
assert.ok(mobile.scrolls.every(([x,y]) => x === 0 && y === 0));
assert.ok(mobile.events.every(type => type === 'resize'));

mobile.focus(input);
mobile.close(input);
mobile.focus({ tagName: 'TEXTAREA' });
mobile.flush();
assert.equal(mobile.scrolls.length, 3, 'A new edit must cancel pending viewport restoration');

// Unity clears mobile_input.input before removing the element. The C# onEndEdit
// bridge must still restore the view when focusout can no longer identify it.
mobile.context.mobile_input = null;
mobile.document.activeElement = null;
mobile.library.VD_RestoreMobileInputViewport();
mobile.flush();
assert.equal(mobile.scrolls.length, 6, 'Programmatic nickname submit restores after DOM removal');

const desktop = browser(false);
desktop.library.VD_InstallMobileInputViewport();
desktop.library.VD_RestoreMobileInputViewport();
desktop.flush();
assert.equal(desktop.styles.length, 0, 'Desktop page styling stays untouched');
assert.equal(desktop.scrolls.length, 0, 'Desktop page scroll stays untouched');
assert.match(desktop.viewport.content, /user-scalable=no/, 'Desktop viewport stays untouched');
console.log('VD_MOBILE_VIEWPORT_CHECKS_OK: pre-focus font, pinch zoom, delayed close, refocus race, programmatic submit and desktop isolation.');
