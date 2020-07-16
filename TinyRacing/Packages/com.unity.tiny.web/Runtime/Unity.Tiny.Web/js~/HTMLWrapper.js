mergeInto(LibraryManager.library, {
  // helper function for strings 
  $utf16_to_js_string: function(ptr) {
    var str = '';
    ptr >>= 1;
    while (1) {
      var codeUnit = HEAP16[ptr++];
      if (!codeUnit) return str;
      str += String.fromCharCode(codeUnit);
    }
  },

  // Create a variable 'ut' in global scope so that Closure sees it.
  js_html_init__postset : 'var ut;',
  js_html_init__proxy : 'async',
  js_html_init : function() {
    ut = ut || {};
    ut._HTML = ut._HTML || {};

    var html = ut._HTML;
    html.visible = true;
    html.focused = true;
  },

  js_html_getDPIScale__proxy : 'sync',
  js_html_getDPIScale : function () {
    return window.devicePixelRatio;
  },

  js_html_getScreenSize__proxy : 'sync',
  js_html_getScreenSize : function (wPtr, hPtr) {
    HEAP32[wPtr>>2] = screen.width | 0;
    HEAP32[hPtr>>2] = screen.height | 0;
  },
  
  js_html_getFrameSize__proxy : 'sync',
  js_html_getFrameSize : function (wPtr, hPtr) {
    HEAP32[wPtr>>2] = window.innerWidth | 0;
    HEAP32[hPtr>>2] = window.innerHeight | 0;
  },  
  
  js_html_getCanvasSize__proxy : 'sync',
  js_html_getCanvasSize : function (wPtr, hPtr) {
    var html = ut._HTML;
    HEAP32[wPtr>>2] = html.canvasElement.width | 0;
    HEAP32[hPtr>>2] = html.canvasElement.height | 0;
  },

  js_html_validateWebGLContextFeatures__proxy: 'sync',
  js_html_validateWebGLContextFeatures__deps: ['$GL'],
  js_html_validateWebGLContextFeatures: function(requireSrgb) {
    if (requireSrgb && GL.currentContext.version == 1 && !GLctx.getExtension('EXT_sRGB')) {
      fatal('WebGL implementation in current browser does not support sRGB rendering (No EXT_sRGB or WebGL 2), but sRGB is required by this page!');
    }
  },

  js_html_setCanvasSize__proxy : 'sync',
  js_html_setCanvasSize : function(width, height, fbwidth, fbheight) {
    if (!width>0 || !height>0)
        throw "Bad canvas size at init.";
    var canvas = ut._HTML.canvasElement;
    if (!canvas) {
      // take possible user element
      canvas = document.getElementById("UT_CANVAS");
    }
    if (!canvas) {
      // Note -- if you change this here, make sure you also update
      // tiny_shell.html, which is where the default actually lives
      canvas = document.createElement("canvas");
      canvas.setAttribute("id", "UT_CANVAS");
      canvas.setAttribute("tabindex", "1");
      canvas.style.touchAction = "none";
      if (document.body) {
        document.body.style.margin = "0px";
        document.body.style.border = "0";
        document.body.style.overflow = "hidden"; // disable scrollbars
        document.body.style.display = "block";   // no floating content on sides
        document.body.insertBefore(canvas, document.body.firstChild);
      } else {
        document.documentElement.appendChild(canvas);
      }
    }

    ut._HTML.canvasElement = canvas;

    canvas.style.width = width + "px";
    canvas.style.height = height + "px";
    canvas.width = fbwidth || width;
    canvas.height = fbheight || height;

    ut._HTML.canvasMode = 'bgfx';

    if (!canvas.tiny_initialized) {
      canvas.addEventListener("webglcontextlost", function(event) { event.preventDefault(); }, false);
      canvas.focus();
      canvas.tiny_initialized = true;
    }

    if (!window.tiny_initialized) {
      window.addEventListener("focus", function (event) { ut._HTML.focus = true; });
      window.addEventListener("blur", function (event) { ut._HTML.focus = false; });
      window.tiny_initialized = true;
    }

    return true;
  },

  js_html_debugReadback__proxy : 'sync',
  js_html_debugReadback : function(w, h, pixels) {
    if (!ut._HTML.canvasContext || ut._HTML.canvasElement.width<w || ut._HTML.canvasElement.height<h)
      return;
    var gl = ut._HTML.canvasContext;
    var imd = new Uint8Array(w*h*4);
    gl.readPixels(0, 0, w, h, gl.RGBA, gl.UNSIGNED_BYTE, imd); 
    for (var i=0; i<w*h*4; i++)
      HEAPU8[pixels+i] = imd[i];
  },

  js_html_promptText__proxy : 'sync',
  js_html_promptText : function(message, defaultText) {
    var res =
        prompt(UTF8ToString(message), UTF8ToString(defaultText));
    var bufferSize = lengthBytesUTF8(res) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(res, buffer, bufferSize);
    return buffer;
  },
});
