const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const messages = [], listeners = {}, unityKeys = [];
let input;
const unityKeyHandler = { eventTypeString:'keypress', handlerFunc:e=>unityKeys.push(e.target) };
const context = {
  JSEvents: { eventHandlers: [unityKeyHandler, { eventTypeString:'mousedown', handlerFunc(){} }] },
  LibraryManager: { library: {} }, mergeInto: (a,b)=>Object.assign(a,b),
  UTF8ToString: x=>x, SendMessage: (...args)=>messages.push(args),
  Module: { canvas: { getBoundingClientRect: ()=>({left:10,top:20,width:450,height:800}) } },
  window: { addEventListener:(key,fn)=>listeners[key]=fn, removeEventListener:key=>delete listeners[key] },
  document: { body: { appendChild:x=>input=x }, createElement: ()=>({
    style:{}, events:{}, value:'', setAttribute(){},
    addEventListener(key,fn){this.events[key]=fn;},remove(){this.removed=true;}
  }) }
};
vm.createContext(context);
vm.runInContext(fs.readFileSync('Assets/Plugins/WebGL/NicknameInput.jslib','utf8'),context);
context.VDNickname=context.LibraryManager.library.$VDNickname;
context.LibraryManager.library.VD_OpenNickname('Opening');
assert.equal(input.style.fontSize,'17px');
assert.equal(input.style.left,'42px');
// Unity's window key handler must not see (and cancel) keys typed into the field, but still sees other keys.
unityKeyHandler.handlerFunc({target:input}); unityKeyHandler.handlerFunc({target:'canvas'});
assert.deepEqual(unityKeys,['canvas']);
input.events.compositionstart();input.value='ㅎ';input.events.input();
assert.equal(messages.filter(x=>x[1]==='BrowserNicknameChanged').length,0);
let prevented=false;
input.events.keydown({key:'Enter',isComposing:true,keyCode:229,stopPropagation(){},preventDefault(){prevented=true;}});
assert.equal(prevented,false);
assert.equal(messages.filter(x=>x[1]==='BrowserNicknameSubmit').length,0);
input.value='한글★_!';input.events.compositionend();
assert.equal(messages.find(x=>x[1]==='BrowserNicknameChanged')[2],'한글★_!');
input.events.keydown({key:'Enter',isComposing:false,keyCode:13,stopPropagation(){},preventDefault(){prevented=true;}});
assert.equal(messages.find(x=>x[1]==='BrowserNicknameSubmit')[2],'한글★_!');
context.LibraryManager.library.VD_CloseNickname();
assert.equal(input.removed,true);assert.equal(listeners.resize,undefined);
unityKeyHandler.handlerFunc({target:input});
assert.deepEqual(unityKeys,['canvas',input]);
console.log('VD_NICKNAME_BROWSER_OK: composition, Korean/symbol commit, Enter, scaling, Unity key release/restore and cleanup');
