mergeInto(LibraryManager.library, {
    js_inputInit__proxy : 'sync',
    js_inputInit : function() {
        ut._HTML = ut._HTML || {};
        ut._HTML.input = {}; // reset input object, reinit on canvas change
        var inp = ut._HTML.input; 
        var canvas = ut._HTML.canvasElement;
        
        if (!canvas) 
            return false;
        
        // pointer lock object forking for cross browser
        canvas.requestPointerLock = canvas.requestPointerLock ||
                                    canvas.mozRequestPointerLock;
        document.exitPointerLock = document.exitPointerLock ||
                                   document.mozExitPointerLock;

        // Calculate the pixelRatio rather than get it from window.devicePixelRatio,
        // for when custom pixel ratios are supported.
        function getPixelRatio() {
            var rect = inp.canvas.getBoundingClientRect();
            return inp.canvas.width / rect.width;
        }
        
        inp.getStream = function(stream,maxLen,destPtr) {
            destPtr>>=2;
            var l = stream.length;
            if ( l>maxLen ) l = maxLen;
            for ( var i=0; i<l; i++ )
                HEAP32[destPtr+i] = stream[i];
            return l;
        };
   
        inp.updateCursor = function() {
            if (ut.inpActiveMouseMode == ut.inpSavedMouseMode)
                return;
            
            var canvas = ut._HTML.canvasElement;
            var hasPointerLock = (document.pointerLockElement === canvas ||
                document.mozPointerLockElement === canvas);

            if (ut.inpSavedMouseMode == 0) {
                // normal
                document.body.style.cursor = 'auto';
                if (hasPointerLock)
                    document.exitPointerLock();
                ut.inpActiveMouseMode = 0;
            }
            else if (ut.inpSavedMouseMode == 1) {
                // hidden
                document.body.style.cursor = 'none';
                if (hasPointerLock)
                    document.exitPointerLock();
                ut.inpActiveMouseMode = 1;
            }
            else {
                // locked + hidden
                canvas.requestPointerLock();
                
                // ut.inpActiveMouseMode won't change until (and if) locking is successful
            }
        };
   
        inp.mouseEventFn = function(ev) {
            if (ut.inpSavedMouseMode != ut.inpActiveMouseMode)
                return;

            var inp = ut._HTML.input;
            var eventType;
            var buttons = 0;
            if (ev.type == "mouseup") { eventType = 0; buttons = ev.button; }
            else if (ev.type == "mousedown") { eventType = 1; buttons = ev.button; }
            else if (ev.type == "mousemove") { eventType = 2; }
            else return;
            var pixelRatio = getPixelRatio();
            var x = Math.round(ev.clientX * pixelRatio) | 0;
            var y = Math.round((ev.target.clientHeight - 1 - ev.clientY) * pixelRatio) | 0;
            var dx = Math.round(ev.movementX * pixelRatio) | 0;
            var dy = Math.round(ev.movementY * pixelRatio) | 0;
            inp.mouseStream.push(eventType|0);
            inp.mouseStream.push(buttons|0);
            inp.mouseStream.push(x);
            inp.mouseStream.push(y);
            inp.mouseStream.push(dx);
            inp.mouseStream.push(dy);
            ev.preventDefault(); 
            ev.stopPropagation();
        };

        // It appears that the scale of scroll wheel input values varies greatly across
        // browsers, different versions of the same browser, OSes and input devices.
        // Trying the approach proposed here to normalize input values:
        // http://jsbin.com/toyaqegumu/edit?html,css,js,output
        var normalizeWheelDelta = function() {
            // Keep a distribution of observed values, and scale by the
            // 33rd percentile.
            var distribution = [];
            var done = null;
            var scale = 1;
            return function(n) {
              // Zeroes don't count.
              if (n == 0) return n;
              // After 500 samples, we stop sampling and keep current factor.
              if (done !== null) return n * done;
              var abs = Math.abs(n);
              // Insert value (sorted in ascending order).
              outer: do { // Just used for break goto
                for (var i = 0; i < distribution.length; ++i) {
                  if (abs <= distribution[i]) {
                    distribution.splice(i, 0, abs);
                    break outer;
                  }
                }
                distribution.push(abs);
              } while (false);
              // Factor is scale divided by 33rd percentile.
              var factor = scale / distribution[Math.floor(distribution.length / 3)];
              if (distribution.length == 500) done = factor;
              return n * factor;
            };
        }();

        inp.wheelEventFn = function(ev) {
            var dx = ev.deltaX;
            var dy = ev.deltaY;
            if (dx) {
                var ndx = Math.round(normalizeWheelDelta(dx));
                if (!ndx) ndx = dx > 0 ? 1 : -1;
                dx = ndx;
            }
            if (dy) {
                var ndy = Math.round(normalizeWheelDelta(dy));
                if (!ndy) ndy = dy > 0 ? 1 : -1;
                dy = ndy;
            }
            inp.wheelStream.push(dx|0);
            inp.wheelStream.push(dy|0);
            ev.preventDefault();
            ev.stopPropagation();
        };
        
        inp.touchEventFn = function(ev) {
            var inp = ut._HTML.input;
            var eventType, x, y, touch, touches = ev.changedTouches;
            var buttons = 0;
            if (ev.type == "touchstart") eventType = 1;
            else if (ev.type == "touchend") eventType = 0;
            else if (ev.type == "touchcancel") eventType = 3;
            else eventType = 2;
            var pixelRatio = getPixelRatio();
            for (var i = 0; i < touches.length; ++i) {
                var t = touches[i];
                x = Math.round(t.clientX * pixelRatio) | 0;
                y = Math.round((t.target.clientHeight - 1 - t.clientY) * pixelRatio) | 0;
                inp.touchStream.push(eventType|0);
                inp.touchStream.push(t.identifier|0);
                inp.touchStream.push(x);
                inp.touchStream.push(y);
            }
            ev.preventDefault();
            ev.stopPropagation();
        };       

        inp.keyEventFn = function(ev) {
            var eventType;
            if (ev.type == "keydown") eventType = 1;
            else if (ev.type == "keyup") eventType = 0;
            else return;
            inp.keyStream.push(eventType|0);
            inp.keyStream.push(ev.keyCode|0);
        };        

        inp.clickEventFn = function() {
            // ensures we can regain focus if focus is lost
            this.focus();
            inp.updateCursor();
        };        

        inp.focusoutEventFn = function() {
            var inp = ut._HTML.input;
            inp.focusLost = true;
            ut.inpActiveMouseMode = 0;
        };
        
        inp.cursorLockChangeFn = function() {
            var canvas = ut._HTML.canvasElement;
            if (document.pointerLockElement === canvas ||
                document.mozPointerLockElement === canvas) 
            {
                // locked successfully
                ut.inpActiveMouseMode = 2;
            }
            else
            {
                // unlocked
                if (ut.inpActiveMouseMode === 2)
                    ut.inpActiveMouseMode = 0;
            }
        };

        inp.mouseStream = [];
        inp.wheelStream = [];
        inp.keyStream = [];  
        inp.touchStream = [];
        inp.canvas = canvas; 
        inp.focusLost = false;
        ut.inpSavedMouseMode = ut.inpSavedMouseMode || 0; // user may have set prior to init
        ut.inpActiveMouseMode = ut.inpActiveMouseMode || 0;        
        
        // @TODO: handle multitouch
        // Pointer events get delivered on Android Chrome with pageX/pageY
        // in a coordinate system that I can't figure out.  So don't use
        // them at all.
        //events["pointerdown"] = events["pointerup"] = events["pointermove"] = html.pointerEventFn;
        var events = {}
        events["keydown"] = inp.keyEventFn;
        events["keyup"] = inp.keyEventFn;        
        events["touchstart"] = events["touchend"] = events["touchmove"] = events["touchcancel"] = inp.touchEventFn;
        events["mousedown"] = events["mouseup"] = events["mousemove"] = inp.mouseEventFn;
        events["wheel"] = inp.wheelEventFn;
        events["focusout"] = inp.focusoutEventFn;
        events["click"] = inp.clickEventFn;

        for (var ev in events)
            canvas.addEventListener(ev, events[ev]);
               
        document.addEventListener('pointerlockchange', inp.cursorLockChangeFn);
        document.addEventListener('mozpointerlockchange', inp.cursorLockChangeFn);
        // Detect when the user changes apps/browser tabs on iOS, to reset the touch events.
        document.addEventListener("visibilitychange", inp.focusoutEventFn);

        return true;   
    },

    js_inputGetFocusLost__proxy : 'sync',
    js_inputGetFocusLost : function () {
        var inp = ut._HTML.input;
        // need to reset all input state in that case
        if ( inp.focusLost ) {
            inp.focusLost = false; 
            return true; 
        }
        return false;
    },
    
    js_inputGetCanvasLost__proxy : 'sync',
    js_inputGetCanvasLost : function () {
        // need to reset all input state in case the canvas element changed and re-init input
        var inp = ut._HTML.input;
        var canvas = ut._HTML.canvasElement;    
        return canvas != inp.canvas; 
    },

    js_inputSetMouseMode__proxy : 'sync',
    js_inputSetMouseMode : function (mode) {
        ut.inpActiveMouseMode = ut.inpActiveMouseMode || 0;
        ut.inpSavedMouseMode = mode;

        if (ut && ut._HTML && ut._HTML.input && ut._HTML.input.focusLost)
            ut._HTML.input.updateCursor();
    },
    
    js_inputGetKeyStream__proxy : 'sync',
    js_inputGetKeyStream : function (maxLen,destPtr) {
        var inp = ut._HTML.input;
        return inp.getStream(inp.keyStream,maxLen,destPtr);            
    },

    js_inputGetTouchStream__proxy : 'sync',
    js_inputGetTouchStream  : function (maxLen,destPtr) {
        var inp = ut._HTML.input;
        return inp.getStream(inp.touchStream,maxLen,destPtr);        
    },

    js_inputGetMouseStream__proxy : 'sync',
    js_inputGetMouseStream  : function (maxLen,destPtr) {
        var inp = ut._HTML.input;
        return inp.getStream(inp.mouseStream,maxLen,destPtr);
    },

    js_inputGetWheelStream__proxy : 'sync',
    js_inputGetWheelStream : function (maxLen,destPtr) {
        var inp = ut._HTML.input;
        return inp.getStream(inp.wheelStream,maxLen,destPtr);
    },
    
    js_inputResetStreams__proxy : 'async',
    js_inputResetStreams : function (maxLen,destPtr) {
        var inp = ut._HTML.input;
        inp.mouseStream.length = 0;
        inp.wheelStream.length = 0;
        inp.keyStream.length = 0;
        inp.touchStream.length = 0;
    }
});
