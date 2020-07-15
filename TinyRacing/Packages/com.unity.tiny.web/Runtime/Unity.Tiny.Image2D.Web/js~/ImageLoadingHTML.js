mergeInto(LibraryManager.library, {

  js_html_initImageLoading__proxy : 'sync',
  js_html_initImageLoading : function() {
    ut = ut || {};
    ut._HTML = ut._HTML || {};

    ut._HTML.images = [null];             // referenced by drawable, direct index to loaded image. maps 1:1 to Image2D component
                                    // { image, mask, loaderror, hasAlpha}
    ut._HTML.tintedSprites = [null];      // referenced by drawable, sub-sprite with colorization
                                    // { image, pattern }
    ut._HTML.tintedSpritesFreeList = [];

    // local helper functions
    ut._HTML.initImage = function(idx ) {
      ut._HTML.images[idx] = {
        image: null,
        mask: null,
        loaderror: false,
        hasAlpha: true,
        glTexture: null,
        glDisableSmoothing: false
      };
    };

    ut._HTML.ensureImageIsReadable = function (idx, w, h) {
      if (ut._HTML.canvasMode == 'webgl2' || ut._HTML.canvasMode == 'webgl') {
        var gl = ut._HTML.canvasContext;
        if (ut._HTML.images[idx].isrt) { // need to readback
          if (!ut._HTML.images[idx].glTexture)
            return false;
          // create fbo, read back bytes, write to image pixels
          var pixels = new Uint8Array(w*h*4);
          var fbo = gl.createFramebuffer();
          gl.bindFramebuffer(gl.FRAMEBUFFER, fbo);
          gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, ut._HTML.images[idx].glTexture, 0);
          gl.viewport(0,0,w,h);
          if (gl.checkFramebufferStatus(gl.FRAMEBUFFER)==gl.FRAMEBUFFER_COMPLETE) {
            gl.readPixels(0, 0, w, h, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
          } else {
            console.log("Warning, can not read back from WebGL framebuffer.");
            gl.bindFramebuffer(gl.FRAMEBUFFER, null);
            gl.deleteFramebuffer(fbo);
            return false;
          }
          // restore default fbo
          gl.bindFramebuffer(gl.FRAMEBUFFER, null);
          gl.deleteFramebuffer(fbo);
          // put pixels onto an image
          var canvas = document.createElement('canvas');
          canvas.width = w;
          canvas.height = h;
          var cx = canvas.getContext('2d');
          var imd = cx.createImageData(w, h);
          imd.data.set(pixels);
          cx.putImageData(imd,0,0);
          ut._HTML.images[idx].image = canvas;
          return true;
        }
      }
      if (ut._HTML.images[idx].isrt)
        return ut._HTML.images[idx].image && ut._HTML.images[idx].width==w && ut._HTML.images[idx].height==h;
      else
        return ut._HTML.images[idx].image && ut._HTML.images[idx].image.naturalWidth===w && ut._HTML.images[idx].image.naturalHeight===h;
    };

    ut._HTML.readyCanvasForReadback = function (idx, w, h) {
      if (!ut._HTML.ensureImageIsReadable(idx,w,h)) 
        return null;
      if (ut._HTML.images[idx].image instanceof HTMLCanvasElement) {
        // directly use canvas if the image is already a canvas (RTT case)
        return ut._HTML.images[idx].image;
      } else {
        // otherwise copy to a temp canvas
        var cvs = document.createElement('canvas');
        cvs.width = w;
        cvs.height = h;
        var cx = cvs.getContext('2d');
        var srcimg = ut._HTML.images[idx].image;
        cx.globalCompositeOperation = 'copy';
        cx.drawImage(srcimg, 0, 0, w, h);
        return cvs;
      }
    };

    ut._HTML.loadWebPFallback = function(url, idx) {
      function decode_base64(base64) {
        var size = base64.length;
        while (base64.charCodeAt(size - 1) == 0x3D)
          size--;
        var data = new Uint8Array(size * 3 >> 2);
        for (var c, cPrev = 0, s = 6, d = 0, b = 0; b < size; cPrev = c, s = s + 2 & 7) {
          c = base64.charCodeAt(b++);
          c = c >= 0x61 ? c - 0x47 : c >= 0x41 ? c - 0x41 : c >= 0x30 ? c + 4 : c == 0x2F ? 0x3F : 0x3E;
          if (s < 6)
            data[d++] = cPrev << 2 + s | c >> 4 - s;
        }
        return data;
      }
      if(!url)
        return false;
      if (!(typeof WebPDecoder == "object"))
        return false; // no webp fallback installed, let it fail on it's own
      if (WebPDecoder.nativeSupport)
        return false; // regular loading
      var webpCanvas;
      var webpPrefix = "data:image/webp;base64,";
      if (!url.lastIndexOf(webpPrefix, 0)) { // data url 
        webpCanvas = document.createElement("canvas");
        WebPDecoder.decode(decode_base64(url.substring(webpPrefix.length)), webpCanvas);
        webpCanvas.naturalWidth = webpCanvas.width;
        webpCanvas.naturalHeight = webpCanvas.height;
        webpCanvas.complete = true;
        ut._HTML.initImage(idx);
        ut._HTML.images[idx].image = webpCanvas;
        return true;
      }
      if (url.lastIndexOf("data:image/", 0) && url.match(/\.webp$/i)) {
        webpCanvas = document.createElement("canvas");
        webpCanvas.naturalWidth = 0;
        webpCanvas.naturalHeight = 0;
        webpCanvas.complete = false;
        ut._HTML.initImage(idx);
        ut._HTML.images[idx].image = webpCanvas;
        var webpRequest = new XMLHttpRequest();
        webpRequest.responseType = "arraybuffer";
        webpRequest.open("GET", url);
        webpRequest.onerror = function () {
          ut._HTML.images[idx].loaderror = true;
        };
        webpRequest.onload = function () {
          WebPDecoder.decode(new Uint8Array(webpRequest.response), webpCanvas);
          webpCanvas.naturalWidth = webpCanvas.width;
          webpCanvas.naturalHeight = webpCanvas.height;
          webpCanvas.complete = true;
        };
        webpRequest.send();
        return true;
      }
      return false; 
    };

  },

  // start loading image 
  js_html_loadImage__proxy : 'sync',
  js_html_loadImage : function(colorName, maskName) {
    colorName = colorName ? UTF8ToString(colorName) : null;
    maskName = maskName ? UTF8ToString(maskName) : null;

    // rewrite some special urls 
    if (colorName == "::white1x1") {
      colorName = "data:image/gif;base64,R0lGODlhAQABAIAAAP7//wAAACH5BAAAAAAALAAAAAABAAEAAAICRAEAOw==";
    } else if (colorName && colorName.substring(0, 9) == "ut-asset:") {
      colorName = UT_ASSETS[colorName.substring(9)];
    }
    if (maskName && maskName.substring(0, 9) == "ut-asset:") {
      maskName = UT_ASSETS[maskName.substring(9)];
    }

    // grab first free index
    var idx;
    for (var i = 1; i <= ut._HTML.images.length; i++) {
      if (!ut._HTML.images[i]) {
        idx = i;
        break;
      }
    }
    ut._HTML.initImage(idx);

    // webp fallback if needed (extra special case)
    if (ut._HTML.loadWebPFallback(colorName, idx) )
      return idx;

    // start actual load
    if (colorName) {
      var imgColor = new Image();
      var isjpg = !!colorName.match(/\.jpe?g$/i);
      ut._HTML.images[idx].image = imgColor;
      ut._HTML.images[idx].hasAlpha = !isjpg;
      imgColor.onerror = function() { ut._HTML.images[idx].loaderror = true; };
      imgColor.src = colorName;
    }

    if (maskName) {
      var imgMask = new Image();
      ut._HTML.images[idx].mask = imgMask;
      ut._HTML.images[idx].hasAlpha = true;
      imgMask.onerror = function() { ut._HTML.images[idx].loaderror = true; };
      imgMask.src = maskName;
    }

    return idx; 
  },

  // check loaded
  js_html_checkLoadImage__proxy : 'sync',
  js_html_checkLoadImage : function(idx) {
    var img = ut._HTML.images[idx];

    if ( img.loaderror ) {
      return 2;
    }

    if (img.image) {
      if (!img.image.complete || !img.image.naturalWidth || !img.image.naturalHeight)
        return 0; // null - not yet loaded
    }

    if (img.mask) {
      if (!img.mask.complete || !img.mask.naturalWidth || !img.mask.naturalHeight)
        return 0; // null - not yet loaded
    }

    return 1; // ok
  },

  js_html_finishLoadImage__proxy : 'sync',
  js_html_finishLoadImage : function(idx, wPtr, hPtr, alphaPtr) {
    var img = ut._HTML.images[idx];
    // check three combinations of mask and image
    if (img.image && img.mask) { // image and mask, merge mask into image 
      var width = img.image.naturalWidth;
      var height = img.image.naturalHeight;
      var maskwidth = img.mask.naturalWidth;
      var maskheight = img.mask.naturalHeight;

      // construct the final image
      var cvscolor = document.createElement('canvas');
      cvscolor.width = width;
      cvscolor.height = height;
      var cxcolor = cvscolor.getContext('2d');
      cxcolor.globalCompositeOperation = 'copy';
      cxcolor.drawImage(img.image, 0, 0);

      var cvsalpha = document.createElement('canvas');
      cvsalpha.width = width;
      cvsalpha.height = height;
      var cxalpha = cvsalpha.getContext('2d');
      cxalpha.globalCompositeOperation = 'copy';
      cxalpha.drawImage(img.mask, 0, 0, width, height);

      var colorBits = cxcolor.getImageData(0, 0, width, height);
      var alphaBits = cxalpha.getImageData(0, 0, width, height);
      var cdata = colorBits.data, adata = alphaBits.data;
      var sz = width * height;
      for (var i = 0; i < sz; i++)
        cdata[(i<<2) + 3] = adata[i<<2];
      cxcolor.putImageData(colorBits, 0, 0);

      img.image = cvscolor;
      img.image.naturalWidth = width;
      img.image.naturalHeight = height; 
      img.hasAlpha = true; 
    } else if (!img.image && img.mask) { // mask only, create image
      var width = img.mask.naturalWidth;
      var height = img.mask.naturalHeight;

      // construct the final image: copy R to all channels 
      var cvscolor = document.createElement('canvas');
      cvscolor.width = width;
      cvscolor.height = height;
      var cxcolor = cvscolor.getContext('2d');
      cxcolor.globalCompositeOperation = 'copy';
      cxcolor.drawImage(img.mask, 0, 0);

      var colorBits = cxcolor.getImageData(0, 0, width, height);
      var cdata = colorBits.data;
      var sz = width * height;
      for (var i = 0; i < sz; i++) {
        cdata[(i<<2) + 1] = cdata[i<<2];
        cdata[(i<<2) + 2] = cdata[i<<2];
        cdata[(i<<2) + 3] = cdata[i<<2];
      }
      cxcolor.putImageData(colorBits, 0, 0);

      img.image = cvscolor;
      img.image.naturalWidth = width;
      img.image.naturalHeight = height; 
      img.hasAlpha = true; 
    } // else img.image only, nothing else to do here

    // done, return valid size and hasAlpha
    HEAP32[wPtr>>2] = img.image.naturalWidth;
    HEAP32[hPtr>>2] = img.image.naturalHeight;
    HEAP32[alphaPtr>>2] = img.hasAlpha;
  },

  js_html_freeImage__proxy : 'async',
  js_html_freeImage : function (idx) {
    ut._HTML.images[idx] = null;
  },

  js_html_extractAlphaFromImage__proxy : 'sync',
  js_html_extractAlphaFromImage : function (idx, destPtr, w, h) {
    var cvs = document.createElement('canvas');
    cvs.width = w;
    cvs.height = h;
    var cx = cvs.getContext('2d');
    var srcimg = ut._HTML.images[idx].image;
    cx.globalCompositeOperation = 'copy';
    cx.drawImage(srcimg, 0, 0, w, h);
    var pixels = cx.getImageData(0, 0, w, h);
    var src = pixels.data;
    for (var o = 0; o < w * h; o++)
      HEAPU8[destPtr+o] = src[o * 4 + 3];
  },

  js_html_imageToDataURI__proxy : 'sync',
  js_html_imageToDataURI : function (idx, w, h) {
    var cvs = ut._HTML.readyCanvasForReadback(idx,w,h);
    if (!cvs)
      return 0;
    var s = cvs.toDataURL('image/png');
    var buffer = Module._malloc(s.length + 1);
    writeAsciiToMemory(s, buffer);
    return buffer;
  },

  js_html_imagePostRequestStatus__proxy : 'sync',
  js_html_imagePostRequestStatus : function (idx) {
    if (idx<=0 || idx>=ut._HTML.postingImages)
      return 0; // failed
    var r = ut._HTML.postingImages[idx];
    if (r!==-1)
      ut._HTML.postingImages[idx] = -2; // re-use
    return r;
  },

  js_html_imagePostRequest__proxy : 'sync',
  js_html_imagePostRequest : function (idx, w, h, uri) {
    var cvs = ut._HTML.readyCanvasForReadback(idx,w,h);
    if (!cvs)
      return -1;
    uri = UTF8ToString(uri);
    var postidx = -1;
    var blobf = function (blob) {
      var xhr = new XMLHttpRequest();
      xhr.open('POST', uri, true);
      xhr.setRequestHeader("Content-Type", "image/png");
      xhr.send(blob);
      xhr.onreadystatechange = function() { // Call a function when the state changes.
        if (this.readyState === XMLHttpRequest.DONE) {
          ut._HTML.postingImages[postidx] = this.status; // 200=ok, 0 or other=failed
        }
      };
    };
    if (!ut._HTML.postingImages) {
      ut._HTML.postingImages = [null,-1];
      postidx = 1;
    } else {
      postidx = ut._HTML.postingImages.length;
      for (var i=1;i<ut._HTML.postingImages.length;i++) {
        if (ut._HTML.postingImages[i]==-2) { // re-use
          postidx = i;
          break;
        }
      }
      ut._HTML.postingImages[postidx] = -1; // no status yet
    }

    var s = cvs.toBlob(blobf, 'image/png');
    return postidx;
  },

  js_html_imageToMemory__proxy : 'sync',
  js_html_imageToMemory : function (idx, w, h, dest) {
    // TODO: there could be a fast(ish) path for webgl to get gl to directly write to
    // dest when reading from render targets
    var cvs = ut._HTML.readyCanvasForReadback(idx,w,h);
    if (!cvs)
      return 0;
    var cx = cvs.getContext('2d');
    var imd = cx.getImageData(0, 0, w, h);
    HEAPU8.set(imd.data,dest);
    return 1;
  },

  js_html_imageFromMemory__proxy : 'sync',
  js_html_imageFromMemory : function (idx, w, h, src) {
    // this should update existing images ...
    if (idx <= 0 || !ut._HTML.images[idx]) {
      for (var i = 1; i <= ut._HTML.images.length; i++) {
        if (!ut._HTML.images[i]) {
          idx = i;
          break;
        }
      }
      ut._HTML.initImage(idx);
    }
    var img = ut._HTML.images[idx];

    // src is rgba premultiplied
    var cvs = document.createElement('canvas');
    cvs.width = w;
    cvs.height = h;
    var cx = cvs.getContext('2d');
    var imd = cx.createImageData(w, h);
    imd.data.set(HEAPU8.subarray(src,src+((w*h)<<2)));
    cx.putImageData(imd,0,0);
    img.image = cvs;
    img.image.naturalWidth = w;
    img.image.naturalHeight = h; 
    img.hasAlpha = true; 

    return idx;
  }
});

