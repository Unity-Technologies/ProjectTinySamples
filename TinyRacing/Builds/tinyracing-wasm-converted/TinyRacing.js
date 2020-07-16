
function out(text) {
  console.log(text);
}
function err(text) {
  console.error(text);
}
function ready() {
  run();
}
function ready() {
  try {
    if (
      typeof ENVIRONMENT_IS_PTHREAD === "undefined" ||
      !ENVIRONMENT_IS_PTHREAD
    )
      run();
  } catch (e) {
    if (e !== "unwind") throw e;
  }
}
(function (global, module) {
  var _allocateArrayOnHeap = function (typedArray) {
    var requiredMemorySize = typedArray.length * typedArray.BYTES_PER_ELEMENT;
    var ptr = _malloc(requiredMemorySize);
    var heapBytes = new Uint8Array(HEAPU8.buffer, ptr, requiredMemorySize);
    heapBytes.set(new Uint8Array(typedArray.buffer));
    return heapBytes;
  };
  var _allocateStringOnHeap = function (string) {
    var bufferSize = lengthBytesUTF8(string) + 1;
    var ptr = _malloc(bufferSize);
    stringToUTF8(string, ptr, bufferSize);
    return ptr;
  };
  var _freeArrayFromHeap = function (heapBytes) {
    if (typeof heapBytes !== "undefined") _free(heapBytes.byteOffset);
  };
  var _freeStringFromHeap = function (stringPtr) {
    if (typeof stringPtr !== "undefined") _free(stringPtr);
  };
  var _sendMessage = function (message, intArr, floatArr, byteArray) {
    if (!Array.isArray(intArr)) {
      intArr = [];
    }
    if (!Array.isArray(floatArr)) {
      floatArr = [];
    }
    if (!Array.isArray(byteArray)) {
      byteArray = [];
    }
    var messageOnHeap, intOnHeap, floatOnHeap, bytesOnHeap;
    try {
      messageOnHeap = _allocateStringOnHeap(message);
      intOnHeap = _allocateArrayOnHeap(new Int32Array(intArr));
      floatOnHeap = _allocateArrayOnHeap(new Float32Array(floatArr));
      bytesOnHeap = _allocateArrayOnHeap(new Uint8Array(byteArray));
      _SendMessage(messageOnHeap,intOnHeap.byteOffset,intArr.length,floatOnHeap.byteOffset,floatArr.length, bytesOnHeap.byteOffset,byteArray.length);
    } finally {
      _freeStringFromHeap(messageOnHeap);
      _freeArrayFromHeap(intOnHeap);
      _freeArrayFromHeap(floatOnHeap);
      _freeArrayFromHeap(bytesOnHeap);
    }
  };
  global["SendMessage"] = _sendMessage;
  module["SendMessage"] = _sendMessage;
})(window, Module);
function abort(what) {
  throw what;
}
var tempRet0 = 0;
var setTempRet0 = function (value) {
  tempRet0 = value;
};
var UTF8Decoder =
  typeof TextDecoder !== "undefined" ? new TextDecoder("utf8") : undefined;
function UTF8ArrayToString(u8Array, idx, maxBytesToRead) {
  var endIdx = idx + maxBytesToRead;
  var endPtr = idx;
  while (u8Array[endPtr] && !(endPtr >= endIdx)) ++endPtr;
  if (endPtr - idx > 16 && u8Array.subarray && UTF8Decoder) {
    return UTF8Decoder.decode(u8Array.subarray(idx, endPtr));
  } else {
    var str = "";
    while (idx < endPtr) {
      var u0 = u8Array[idx++];
      if (!(u0 & 128)) {
        str += String.fromCharCode(u0);
        continue;
      }
      var u1 = u8Array[idx++] & 63;
      if ((u0 & 224) == 192) {
        str += String.fromCharCode(((u0 & 31) << 6) | u1);
        continue;
      }
      var u2 = u8Array[idx++] & 63;
      if ((u0 & 240) == 224) {
        u0 = ((u0 & 15) << 12) | (u1 << 6) | u2;
      } else {
        u0 = ((u0 & 7) << 18) | (u1 << 12) | (u2 << 6) | (u8Array[idx++] & 63);
      }
      if (u0 < 65536) {
        str += String.fromCharCode(u0);
      } else {
        var ch = u0 - 65536;
        str += String.fromCharCode(55296 | (ch >> 10), 56320 | (ch & 1023));
      }
    }
  }
  return str;
}
function UTF8ToString(ptr, maxBytesToRead) {
  return ptr ? UTF8ArrayToString(HEAPU8, ptr, maxBytesToRead) : "";
}
function stringToUTF8Array(str, outU8Array, outIdx, maxBytesToWrite) {
  if (!(maxBytesToWrite > 0)) return 0;
  var startIdx = outIdx;
  var endIdx = outIdx + maxBytesToWrite - 1;
  for (var i = 0; i < str.length; ++i) {
    var u = str.charCodeAt(i);
    if (u >= 55296 && u <= 57343) {
      var u1 = str.charCodeAt(++i);
      u = (65536 + ((u & 1023) << 10)) | (u1 & 1023);
    }
    if (u <= 127) {
      if (outIdx >= endIdx) break;
      outU8Array[outIdx++] = u;
    } else if (u <= 2047) {
      if (outIdx + 1 >= endIdx) break;
      outU8Array[outIdx++] = 192 | (u >> 6);
      outU8Array[outIdx++] = 128 | (u & 63);
    } else if (u <= 65535) {
      if (outIdx + 2 >= endIdx) break;
      outU8Array[outIdx++] = 224 | (u >> 12);
      outU8Array[outIdx++] = 128 | ((u >> 6) & 63);
      outU8Array[outIdx++] = 128 | (u & 63);
    } else {
      if (outIdx + 3 >= endIdx) break;
      outU8Array[outIdx++] = 240 | (u >> 18);
      outU8Array[outIdx++] = 128 | ((u >> 12) & 63);
      outU8Array[outIdx++] = 128 | ((u >> 6) & 63);
      outU8Array[outIdx++] = 128 | (u & 63);
    }
  }
  outU8Array[outIdx] = 0;
  return outIdx - startIdx;
}
function stringToUTF8(str, outPtr, maxBytesToWrite) {
  return stringToUTF8Array(str, HEAPU8, outPtr, maxBytesToWrite);
}
function lengthBytesUTF8(str) {
  var len = 0;
  for (var i = 0; i < str.length; ++i) {
    var u = str.charCodeAt(i);
    if (u >= 55296 && u <= 57343)
      u = (65536 + ((u & 1023) << 10)) | (str.charCodeAt(++i) & 1023);
    if (u <= 127) ++len;
    else if (u <= 2047) len += 2;
    else if (u <= 65535) len += 3;
    else len += 4;
  }
  return len;
}
var DYNAMICTOP_PTR = 804928;
var wasmMaximumMemory = 134217728;
var wasmMemory = new WebAssembly.Memory({
  initial: 134217728 >> 16,
  maximum: wasmMaximumMemory >> 16,
});
var buffer = wasmMemory.buffer;
var wasmTable = new WebAssembly.Table({
  initial: 5359,
  maximum: 5359 + 0,
  element: "anyfunc",
});
var HEAP8 = new Int8Array(buffer);
var HEAP16 = new Int16Array(buffer);
var HEAP32 = new Int32Array(buffer);
var HEAPU8 = new Uint8Array(buffer);
var HEAPU16 = new Uint16Array(buffer);
var HEAPU32 = new Uint32Array(buffer);
var HEAPF32 = new Float32Array(buffer);
var HEAPF64 = new Float64Array(buffer);
HEAP32[DYNAMICTOP_PTR >> 2] = 6047984;
function unSign(value, bits, ignore) {
  if (value >= 0) {
    return value;
  }
  return bits <= 32
    ? 2 * Math.abs(1 << (bits - 1)) + value
    : Math.pow(2, bits) + value;
}
function reSign(value, bits, ignore) {
  if (value <= 0) {
    return value;
  }
  var half = bits <= 32 ? Math.abs(1 << (bits - 1)) : Math.pow(2, bits - 1);
  if (value >= half && (bits <= 32 || value > half)) {
    value = -2 * half + value;
  }
  return value;
}
var ASM_CONSTS = {
  4629: function () {
    debugger;
  },
};
function _emscripten_asm_const_iii(code, sigPtr, argbuf) {
  var args = readAsmConstArgs(sigPtr, argbuf);
  return ASM_CONSTS[code].apply(null, args);
}
function ___lock() {}
function ___setErrNo(value) {
  return 0;
}
var PATH = {
  splitPath: function (filename) {
    var splitPathRe = /^(\/?|)([\s\S]*?)((?:\.{1,2}|[^\/]+?|)(\.[^.\/]*|))(?:[\/]*)$/;
    return splitPathRe.exec(filename).slice(1);
  },
  normalizeArray: function (parts, allowAboveRoot) {
    var up = 0;
    for (var i = parts.length - 1; i >= 0; i--) {
      var last = parts[i];
      if (last === ".") {
        parts.splice(i, 1);
      } else if (last === "..") {
        parts.splice(i, 1);
        up++;
      } else if (up) {
        parts.splice(i, 1);
        up--;
      }
    }
    if (allowAboveRoot) {
      for (; up; up--) {
        parts.unshift("..");
      }
    }
    return parts;
  },
  normalize: function (path) {
    var isAbsolute = path.charAt(0) === "/",
      trailingSlash = path.substr(-1) === "/";
    path = PATH.normalizeArray(
      path.split("/").filter(function (p) {
        return !!p;
      }),
      !isAbsolute
    ).join("/");
    if (!path && !isAbsolute) {
      path = ".";
    }
    if (path && trailingSlash) {
      path += "/";
    }
    return (isAbsolute ? "/" : "") + path;
  },
  dirname: function (path) {
    var result = PATH.splitPath(path),
      root = result[0],
      dir = result[1];
    if (!root && !dir) {
      return ".";
    }
    if (dir) {
      dir = dir.substr(0, dir.length - 1);
    }
    return root + dir;
  },
  basename: function (path) {
    if (path === "/") return "/";
    var lastSlash = path.lastIndexOf("/");
    if (lastSlash === -1) return path;
    return path.substr(lastSlash + 1);
  },
  extname: function (path) {
    return PATH.splitPath(path)[3];
  },
  join: function () {
    var paths = Array.prototype.slice.call(arguments, 0);
    return PATH.normalize(paths.join("/"));
  },
  join2: function (l, r) {
    return PATH.normalize(l + "/" + r);
  },
};
var SYSCALLS = {
  buffers: [null, [], []],
  printChar: function (stream, curr) {
    var buffer = SYSCALLS.buffers[stream];
    if (curr === 0 || curr === 10) {
      (stream === 1 ? out : err)(UTF8ArrayToString(buffer, 0));
      buffer.length = 0;
    } else {
      buffer.push(curr);
    }
  },
  varargs: undefined,
  get: function () {
    SYSCALLS.varargs += 4;
    var ret = HEAP32[(SYSCALLS.varargs - 4) >> 2];
    return ret;
  },
  getStr: function (ptr) {
    var ret = UTF8ToString(ptr);
    return ret;
  },
  get64: function (low, high) {
    return low;
  },
};
function ___syscall221(fd, cmd, varargs) {
  SYSCALLS.varargs = varargs;
  return 0;
}
function ___syscall4(fd, buf, count) {
  for (var i = 0; i < count; i++) {
    SYSCALLS.printChar(fd, HEAPU8[buf + i]);
  }
  return count;
}
function ___syscall5(path, flags, varargs) {
  SYSCALLS.varargs = varargs;
}
function ___syscall54(fd, op, varargs) {
  SYSCALLS.varargs = varargs;
  return 0;
}
function ___unlock() {}
function __emscripten_fetch_free(id) {
  delete Fetch.xhrs[id - 1];
}
function _abort() {
  throw "abort";
}
function _clock() {
  if (_clock.start === undefined) _clock.start = Date.now();
  return ((Date.now() - _clock.start) * (1e6 / 1e3)) | 0;
}
function _emscripten_get_now_res() {
  return 1e3;
}
var _emscripten_get_now_is_monotonic = true;
function _clock_getres(clk_id, res) {
  var nsec;
  if (clk_id === 0) {
    nsec = 1e3 * 1e3;
  } else if (clk_id === 1 && _emscripten_get_now_is_monotonic) {
    nsec = _emscripten_get_now_res();
  } else {
    ___setErrNo(28);
    return -1;
  }
  HEAP32[res >> 2] = (nsec / 1e9) | 0;
  HEAP32[(res + 4) >> 2] = nsec;
  return 0;
}
var _emscripten_get_now;
_emscripten_get_now = function () {
  return performance.now();
};
function _clock_gettime(clk_id, tp) {
  var now;
  if (clk_id === 0) {
    now = Date.now();
  } else if (
    (clk_id === 1 || clk_id === 4) &&
    _emscripten_get_now_is_monotonic
  ) {
    now = _emscripten_get_now();
  } else {
    ___setErrNo(28);
    return -1;
  }
  HEAP32[tp >> 2] = (now / 1e3) | 0;
  HEAP32[(tp + 4) >> 2] = ((now % 1e3) * 1e3 * 1e3) | 0;
  return 0;
}
function __webgl_acquireInstancedArraysExtension(ctx) {
  var ext = ctx.getExtension("ANGLE_instanced_arrays");
  if (ext) {
    ctx["vertexAttribDivisor"] = function (index, divisor) {
      ext["vertexAttribDivisorANGLE"](index, divisor);
    };
    ctx["drawArraysInstanced"] = function (mode, first, count, primcount) {
      ext["drawArraysInstancedANGLE"](mode, first, count, primcount);
    };
    ctx["drawElementsInstanced"] = function (
      mode,
      count,
      type,
      indices,
      primcount
    ) {
      ext["drawElementsInstancedANGLE"](mode, count, type, indices, primcount);
    };
  }
}
function __webgl_acquireVertexArrayObjectExtension(ctx) {
  var ext = ctx.getExtension("OES_vertex_array_object");
  if (ext) {
    ctx["createVertexArray"] = function () {
      return ext["createVertexArrayOES"]();
    };
    ctx["deleteVertexArray"] = function (vao) {
      ext["deleteVertexArrayOES"](vao);
    };
    ctx["bindVertexArray"] = function (vao) {
      ext["bindVertexArrayOES"](vao);
    };
    ctx["isVertexArray"] = function (vao) {
      return ext["isVertexArrayOES"](vao);
    };
  }
}
function __webgl_acquireDrawBuffersExtension(ctx) {
  var ext = ctx.getExtension("WEBGL_draw_buffers");
  if (ext) {
    ctx["drawBuffers"] = function (n, bufs) {
      ext["drawBuffersWEBGL"](n, bufs);
    };
  }
}
var GL = {
  counter: 1,
  lastError: 0,
  buffers: [],
  mappedBuffers: {},
  programs: [],
  framebuffers: [],
  renderbuffers: [],
  textures: [],
  uniforms: [],
  shaders: [],
  vaos: [],
  contexts: {},
  currentContext: null,
  offscreenCanvases: {},
  timerQueriesEXT: [],
  queries: [],
  samplers: [],
  transformFeedbacks: [],
  syncs: [],
  programInfos: {},
  stringCache: {},
  stringiCache: {},
  unpackAlignment: 4,
  init: function () {
    var miniTempFloatBuffer = new Float32Array(GL.MINI_TEMP_BUFFER_SIZE);
    for (var i = 0; i < GL.MINI_TEMP_BUFFER_SIZE; i++) {
      GL.miniTempBufferFloatViews[i] = miniTempFloatBuffer.subarray(0, i + 1);
    }
    var miniTempIntBuffer = new Int32Array(GL.MINI_TEMP_BUFFER_SIZE);
    for (var i = 0; i < GL.MINI_TEMP_BUFFER_SIZE; i++) {
      GL.miniTempBufferIntViews[i] = miniTempIntBuffer.subarray(0, i + 1);
    }
  },
  recordError: function recordError(errorCode) {
    if (!GL.lastError) {
      GL.lastError = errorCode;
    }
  },
  getNewId: function (table) {
    var ret = GL.counter++;
    for (var i = table.length; i < ret; i++) {
      table[i] = null;
    }
    return ret;
  },
  MINI_TEMP_BUFFER_SIZE: 256,
  miniTempBufferFloatViews: [0],
  miniTempBufferIntViews: [0],
  getSource: function (shader, count, string, length) {
    var source = "";
    for (var i = 0; i < count; ++i) {
      var len = length ? HEAP32[(length + i * 4) >> 2] : -1;
      source += UTF8ToString(
        HEAP32[(string + i * 4) >> 2],
        len < 0 ? undefined : len
      );
    }
    return source;
  },
  createContext: function (canvas, webGLContextAttributes) {
    var ctx =
      webGLContextAttributes.majorVersion > 1
        ? canvas.getContext("webgl2", webGLContextAttributes)
        : canvas.getContext("webgl", webGLContextAttributes);
    if (!ctx) return 0;
    var handle = GL.registerContext(ctx, webGLContextAttributes);
    function disableHalfFloatExtensionIfBroken(ctx) {
      var t = ctx.createTexture();
      ctx.bindTexture(3553, t);
      for (var i = 0; i < 8 && ctx.getError(); ++i);
      var ext = ctx.getExtension("OES_texture_half_float");
      if (!ext) return;
      ctx.texImage2D(3553, 0, 6408, 1, 1, 0, 6408, 36193, new Uint16Array(4));
      var broken = ctx.getError();
      ctx.bindTexture(3553, null);
      ctx.deleteTexture(t);
      if (broken) {
        ctx.realGetSupportedExtensions = ctx.getSupportedExtensions;
        ctx.getSupportedExtensions = function () {
          return (this.realGetSupportedExtensions() || []).filter(function (
            ext
          ) {
            return ext.indexOf("texture_half_float") == -1;
          });
        };
      }
    }
    disableHalfFloatExtensionIfBroken(ctx);
    return handle;
  },
  registerContext: function (ctx, webGLContextAttributes) {
    var handle = _malloc(8);
    var context = {
      handle: handle,
      attributes: webGLContextAttributes,
      version: webGLContextAttributes.majorVersion,
      GLctx: ctx,
    };
    if (ctx.canvas) {         
          Object.defineProperty(ctx.canvas, 'GLctxObject', {  
                value: context,        
                writable: false, 
                enumerable: false             });       
              }
    GL.contexts[handle] = context;
    if (
      typeof webGLContextAttributes.enableExtensionsByDefault === "undefined" ||
      webGLContextAttributes.enableExtensionsByDefault
    ) {
      GL.initExtensions(context);
    }
    return handle;
  },
  makeContextCurrent: function (contextHandle) {
    GL.currentContext = GL.contexts[contextHandle];
    Module.ctx = GLctx = GL.currentContext && GL.currentContext.GLctx;
    return !(contextHandle && !GLctx);
  },
  getContext: function (contextHandle) {
    return GL.contexts[contextHandle];
  },
  deleteContext: function (contextHandle) {
    if (GL.currentContext === GL.contexts[contextHandle])
      GL.currentContext = null;
    if (typeof JSEvents === "object")
      JSEvents.removeAllHandlersOnTarget(
        GL.contexts[contextHandle].GLctx.canvas
      );
    if (GL.contexts[contextHandle] && GL.contexts[contextHandle].GLctx.canvas)
      GL.contexts[contextHandle].GLctx.canvas.GLctxObject = undefined;
    _free(GL.contexts[contextHandle]);
    GL.contexts[contextHandle] = null;
  },
  initExtensions: function (context) {
    if (!context) context = GL.currentContext;
    if (context.initExtensionsDone) return;
    context.initExtensionsDone = true;
    var GLctx = context.GLctx;
    if (context.version < 2) {
      __webgl_acquireInstancedArraysExtension(GLctx);
      __webgl_acquireVertexArrayObjectExtension(GLctx);
      __webgl_acquireDrawBuffersExtension(GLctx);
    }
    GLctx.disjointTimerQueryExt = GLctx.getExtension(
      "EXT_disjoint_timer_query"
    );
    var automaticallyEnabledExtensions = [
      "OES_texture_float",
      "OES_texture_half_float",
      "OES_standard_derivatives",
      "OES_vertex_array_object",
      "WEBGL_compressed_texture_s3tc",
      "WEBGL_depth_texture",
      "OES_element_index_uint",
      "EXT_texture_filter_anisotropic",
      "EXT_frag_depth",
      "WEBGL_draw_buffers",
      "ANGLE_instanced_arrays",
      "OES_texture_float_linear",
      "OES_texture_half_float_linear",
      "EXT_blend_minmax",
      "EXT_shader_texture_lod",
      "EXT_texture_norm16",
      "WEBGL_compressed_texture_pvrtc",
      "EXT_color_buffer_half_float",
      "WEBGL_color_buffer_float",
      "EXT_sRGB",
      "WEBGL_compressed_texture_etc1",
      "EXT_disjoint_timer_query",
      "WEBGL_compressed_texture_etc",
      "WEBGL_compressed_texture_astc",
      "EXT_color_buffer_float",
      "WEBGL_compressed_texture_s3tc_srgb",
      "EXT_disjoint_timer_query_webgl2",
      "WEBKIT_WEBGL_compressed_texture_pvrtc",
    ];
    var exts = GLctx.getSupportedExtensions() || [];
    exts.forEach(function (ext) {
      if (automaticallyEnabledExtensions.indexOf(ext) != -1) {
        GLctx.getExtension(ext);
      }
    });
  },
  populateUniformTable: function (program) {
    var p = GL.programs[program];
    var ptable = (GL.programInfos[program] = {
      uniforms: {},
      maxUniformLength: 0,
      maxAttributeLength: -1,
      maxUniformBlockNameLength: -1,
    });
    var utable = ptable.uniforms;
    var numUniforms = GLctx.getProgramParameter(p, 35718);
    for (var i = 0; i < numUniforms; ++i) {
      var u = GLctx.getActiveUniform(p, i);
      var name = u.name;
      ptable.maxUniformLength = Math.max(
        ptable.maxUniformLength,
        name.length + 1
      );
      if (name.slice(-1) == "]") {
        name = name.slice(0, name.lastIndexOf("["));
      }
      var loc = GLctx.getUniformLocation(p, name);
      if (loc) {
        var id = GL.getNewId(GL.uniforms);
        utable[name] = [u.size, id];
        GL.uniforms[id] = loc;
        for (var j = 1; j < u.size; ++j) {
          var n = name + "[" + j + "]";
          loc = GLctx.getUniformLocation(p, n);
          id = GL.getNewId(GL.uniforms);
          GL.uniforms[id] = loc;
        }
      }
    }
  },
};
function _emscripten_glActiveTexture(x0) {
  GLctx["activeTexture"](x0);
}
function _emscripten_glAttachShader(program, shader) {
  GLctx.attachShader(GL.programs[program], GL.shaders[shader]);
}
function _emscripten_glBeginQuery(target, id) {
  GLctx["beginQuery"](target, GL.queries[id]);
}
function _emscripten_glBeginQueryEXT(target, id) {
  GLctx.disjointTimerQueryExt["beginQueryEXT"](target, GL.timerQueriesEXT[id]);
}
function _emscripten_glBeginTransformFeedback(x0) {
  GLctx["beginTransformFeedback"](x0);
}
function _emscripten_glBindAttribLocation(program, index, name) {
  GLctx.bindAttribLocation(GL.programs[program], index, UTF8ToString(name));
}
function _emscripten_glBindBuffer(target, buffer) {
  if (target == 35051) {
    GLctx.currentPixelPackBufferBinding = buffer;
  } else if (target == 35052) {
    GLctx.currentPixelUnpackBufferBinding = buffer;
  }
  GLctx.bindBuffer(target, GL.buffers[buffer]);
}
function _emscripten_glBindBufferBase(target, index, buffer) {
  GLctx["bindBufferBase"](target, index, GL.buffers[buffer]);
}
function _emscripten_glBindBufferRange(target, index, buffer, offset, ptrsize) {
  GLctx["bindBufferRange"](target, index, GL.buffers[buffer], offset, ptrsize);
}
function _emscripten_glBindFramebuffer(target, framebuffer) {
  GLctx.bindFramebuffer(target, GL.framebuffers[framebuffer]);
}
function _emscripten_glBindRenderbuffer(target, renderbuffer) {
  GLctx.bindRenderbuffer(target, GL.renderbuffers[renderbuffer]);
}
function _emscripten_glBindSampler(unit, sampler) {
  GLctx["bindSampler"](unit, GL.samplers[sampler]);
}
function _emscripten_glBindTexture(target, texture) {
  GLctx.bindTexture(target, GL.textures[texture]);
}
function _emscripten_glBindTransformFeedback(target, id) {
  GLctx["bindTransformFeedback"](target, GL.transformFeedbacks[id]);
}
function _emscripten_glBindVertexArray(vao) {
  GLctx["bindVertexArray"](GL.vaos[vao]);
}
function _emscripten_glBindVertexArrayOES(vao) {
  GLctx["bindVertexArray"](GL.vaos[vao]);
}
function _emscripten_glBlendColor(x0, x1, x2, x3) {
  GLctx["blendColor"](x0, x1, x2, x3);
}
function _emscripten_glBlendEquation(x0) {
  GLctx["blendEquation"](x0);
}
function _emscripten_glBlendEquationSeparate(x0, x1) {
  GLctx["blendEquationSeparate"](x0, x1);
}
function _emscripten_glBlendFunc(x0, x1) {
  GLctx["blendFunc"](x0, x1);
}
function _emscripten_glBlendFuncSeparate(x0, x1, x2, x3) {
  GLctx["blendFuncSeparate"](x0, x1, x2, x3);
}
function _emscripten_glBlitFramebuffer(x0, x1, x2, x3, x4, x5, x6, x7, x8, x9) {
  GLctx["blitFramebuffer"](x0, x1, x2, x3, x4, x5, x6, x7, x8, x9);
}
function _emscripten_glBufferData(target, size, data, usage) {
  if (GL.currentContext.version >= 2) {
    if (data) {
      GLctx.bufferData(target, HEAPU8, usage, data, size);
    } else {
      GLctx.bufferData(target, size, usage);
    }
  } else {
    GLctx.bufferData(
      target,
      data ? HEAPU8.subarray(data, data + size) : size,
      usage
    );
  }
}
function _emscripten_glBufferSubData(target, offset, size, data) {
  if (GL.currentContext.version >= 2) {
    GLctx.bufferSubData(target, offset, HEAPU8, data, size);
    return;
  }
  GLctx.bufferSubData(target, offset, HEAPU8.subarray(data, data + size));
}
function _emscripten_glCheckFramebufferStatus(x0) {
  return GLctx["checkFramebufferStatus"](x0);
}
function _emscripten_glClear(x0) {
  GLctx["clear"](x0);
}
function _emscripten_glClearBufferfi(x0, x1, x2, x3) {
  GLctx["clearBufferfi"](x0, x1, x2, x3);
}
function _emscripten_glClearBufferfv(buffer, drawbuffer, value) {
  GLctx["clearBufferfv"](buffer, drawbuffer, HEAPF32, value >> 2);
}
function _emscripten_glClearBufferiv(buffer, drawbuffer, value) {
  GLctx["clearBufferiv"](buffer, drawbuffer, HEAP32, value >> 2);
}
function _emscripten_glClearBufferuiv(buffer, drawbuffer, value) {
  GLctx["clearBufferuiv"](buffer, drawbuffer, HEAPU32, value >> 2);
}
function _emscripten_glClearColor(x0, x1, x2, x3) {
  GLctx["clearColor"](x0, x1, x2, x3);
}
function _emscripten_glClearDepthf(x0) {
  GLctx["clearDepth"](x0);
}
function _emscripten_glClearStencil(x0) {
  GLctx["clearStencil"](x0);
}
function convertI32PairToI53(lo, hi) {
  return (lo >>> 0) + hi * 4294967296;
}
function _emscripten_glClientWaitSync(sync, flags, timeoutLo, timeoutHi) {
  return GLctx.clientWaitSync(
    GL.syncs[sync],
    flags,
    convertI32PairToI53(timeoutLo, timeoutHi)
  );
}
function _emscripten_glColorMask(red, green, blue, alpha) {
  GLctx.colorMask(!!red, !!green, !!blue, !!alpha);
}
function _emscripten_glCompileShader(shader) {
  GLctx.compileShader(GL.shaders[shader]);
}
function _emscripten_glCompressedTexImage2D(
  target,
  level,
  internalFormat,
  width,
  height,
  border,
  imageSize,
  data
) {
  if (GL.currentContext.version >= 2) {
    if (GLctx.currentPixelUnpackBufferBinding) {
      GLctx["compressedTexImage2D"](
        target,
        level,
        internalFormat,
        width,
        height,
        border,
        imageSize,
        data
      );
    } else {
      GLctx["compressedTexImage2D"](
        target,
        level,
        internalFormat,
        width,
        height,
        border,
        HEAPU8,
        data,
        imageSize
      );
    }
    return;
  }
  GLctx["compressedTexImage2D"](
    target,
    level,
    internalFormat,
    width,
    height,
    border,
    data ? HEAPU8.subarray(data, data + imageSize) : null
  );
}
function _emscripten_glCompressedTexImage3D(
  target,
  level,
  internalFormat,
  width,
  height,
  depth,
  border,
  imageSize,
  data
) {
  if (GLctx.currentPixelUnpackBufferBinding) {
    GLctx["compressedTexImage3D"](
      target,
      level,
      internalFormat,
      width,
      height,
      depth,
      border,
      imageSize,
      data
    );
  } else {
    GLctx["compressedTexImage3D"](
      target,
      level,
      internalFormat,
      width,
      height,
      depth,
      border,
      HEAPU8,
      data,
      imageSize
    );
  }
}
function _emscripten_glCompressedTexSubImage2D(
  target,
  level,
  xoffset,
  yoffset,
  width,
  height,
  format,
  imageSize,
  data
) {
  if (GL.currentContext.version >= 2) {
    if (GLctx.currentPixelUnpackBufferBinding) {
      GLctx["compressedTexSubImage2D"](
        target,
        level,
        xoffset,
        yoffset,
        width,
        height,
        format,
        imageSize,
        data
      );
    } else {
      GLctx["compressedTexSubImage2D"](
        target,
        level,
        xoffset,
        yoffset,
        width,
        height,
        format,
        HEAPU8,
        data,
        imageSize
      );
    }
    return;
  }
  GLctx["compressedTexSubImage2D"](
    target,
    level,
    xoffset,
    yoffset,
    width,
    height,
    format,
    data ? HEAPU8.subarray(data, data + imageSize) : null
  );
}
function _emscripten_glCompressedTexSubImage3D(
  target,
  level,
  xoffset,
  yoffset,
  zoffset,
  width,
  height,
  depth,
  format,
  imageSize,
  data
) {
  if (GLctx.currentPixelUnpackBufferBinding) {
    GLctx["compressedTexSubImage3D"](
      target,
      level,
      xoffset,
      yoffset,
      zoffset,
      width,
      height,
      depth,
      format,
      imageSize,
      data
    );
  } else {
    GLctx["compressedTexSubImage3D"](
      target,
      level,
      xoffset,
      yoffset,
      zoffset,
      width,
      height,
      depth,
      format,
      HEAPU8,
      data,
      imageSize
    );
  }
}
function _emscripten_glCopyBufferSubData(x0, x1, x2, x3, x4) {
  GLctx["copyBufferSubData"](x0, x1, x2, x3, x4);
}
function _emscripten_glCopyTexImage2D(x0, x1, x2, x3, x4, x5, x6, x7) {
  GLctx["copyTexImage2D"](x0, x1, x2, x3, x4, x5, x6, x7);
}
function _emscripten_glCopyTexSubImage2D(x0, x1, x2, x3, x4, x5, x6, x7) {
  GLctx["copyTexSubImage2D"](x0, x1, x2, x3, x4, x5, x6, x7);
}
function _emscripten_glCopyTexSubImage3D(x0, x1, x2, x3, x4, x5, x6, x7, x8) {
  GLctx["copyTexSubImage3D"](x0, x1, x2, x3, x4, x5, x6, x7, x8);
}
function _emscripten_glCreateProgram() {
  var id = GL.getNewId(GL.programs);
  var program = GLctx.createProgram();
  program.name = id;
  GL.programs[id] = program;
  return id;
}
function _emscripten_glCreateShader(shaderType) {
  var id = GL.getNewId(GL.shaders);
  GL.shaders[id] = GLctx.createShader(shaderType);
  return id;
}
function _emscripten_glCullFace(x0) {
  GLctx["cullFace"](x0);
}
function _emscripten_glDeleteBuffers(n, buffers) {
  for (var i = 0; i < n; i++) {
    var id = HEAP32[(buffers + i * 4) >> 2];
    var buffer = GL.buffers[id];
    if (!buffer) continue;
    GLctx.deleteBuffer(buffer);
    buffer.name = 0;
    GL.buffers[id] = null;
    if (id == GL.currArrayBuffer) GL.currArrayBuffer = 0;
    if (id == GL.currElementArrayBuffer) GL.currElementArrayBuffer = 0;
    if (id == GLctx.currentPixelPackBufferBinding)
      GLctx.currentPixelPackBufferBinding = 0;
    if (id == GLctx.currentPixelUnpackBufferBinding)
      GLctx.currentPixelUnpackBufferBinding = 0;
  }
}
function _emscripten_glDeleteFramebuffers(n, framebuffers) {
  for (var i = 0; i < n; ++i) {
    var id = HEAP32[(framebuffers + i * 4) >> 2];
    var framebuffer = GL.framebuffers[id];
    if (!framebuffer) continue;
    GLctx.deleteFramebuffer(framebuffer);
    framebuffer.name = 0;
    GL.framebuffers[id] = null;
  }
}
function _emscripten_glDeleteProgram(id) {
  if (!id) return;
  var program = GL.programs[id];
  if (!program) {
    GL.recordError(1281);
    return;
  }
  GLctx.deleteProgram(program);
  program.name = 0;
  GL.programs[id] = null;
  GL.programInfos[id] = null;
}
function _emscripten_glDeleteQueries(n, ids) {
  for (var i = 0; i < n; i++) {
    var id = HEAP32[(ids + i * 4) >> 2];
    var query = GL.queries[id];
    if (!query) continue;
    GLctx["deleteQuery"](query);
    GL.queries[id] = null;
  }
}
function _emscripten_glDeleteQueriesEXT(n, ids) {
  for (var i = 0; i < n; i++) {
    var id = HEAP32[(ids + i * 4) >> 2];
    var query = GL.timerQueriesEXT[id];
    if (!query) continue;
    GLctx.disjointTimerQueryExt["deleteQueryEXT"](query);
    GL.timerQueriesEXT[id] = null;
  }
}
function _emscripten_glDeleteRenderbuffers(n, renderbuffers) {
  for (var i = 0; i < n; i++) {
    var id = HEAP32[(renderbuffers + i * 4) >> 2];
    var renderbuffer = GL.renderbuffers[id];
    if (!renderbuffer) continue;
    GLctx.deleteRenderbuffer(renderbuffer);
    renderbuffer.name = 0;
    GL.renderbuffers[id] = null;
  }
}
function _emscripten_glDeleteSamplers(n, samplers) {
  for (var i = 0; i < n; i++) {
    var id = HEAP32[(samplers + i * 4) >> 2];
    var sampler = GL.samplers[id];
    if (!sampler) continue;
    GLctx["deleteSampler"](sampler);
    sampler.name = 0;
    GL.samplers[id] = null;
  }
}
function _emscripten_glDeleteShader(id) {
  if (!id) return;
  var shader = GL.shaders[id];
  if (!shader) {
    GL.recordError(1281);
    return;
  }
  GLctx.deleteShader(shader);
  GL.shaders[id] = null;
}
function _emscripten_glDeleteSync(id) {
  if (!id) return;
  var sync = GL.syncs[id];
  if (!sync) {
    GL.recordError(1281);
    return;
  }
  GLctx.deleteSync(sync);
  sync.name = 0;
  GL.syncs[id] = null;
}
function _emscripten_glDeleteTextures(n, textures) {
  for (var i = 0; i < n; i++) {
    var id = HEAP32[(textures + i * 4) >> 2];
    var texture = GL.textures[id];
    if (!texture) continue;
    GLctx.deleteTexture(texture);
    texture.name = 0;
    GL.textures[id] = null;
  }
}
function _emscripten_glDeleteTransformFeedbacks(n, ids) {
  for (var i = 0; i < n; i++) {
    var id = HEAP32[(ids + i * 4) >> 2];
    var transformFeedback = GL.transformFeedbacks[id];
    if (!transformFeedback) continue;
    GLctx["deleteTransformFeedback"](transformFeedback);
    transformFeedback.name = 0;
    GL.transformFeedbacks[id] = null;
  }
}
function _emscripten_glDeleteVertexArrays(n, vaos) {
  for (var i = 0; i < n; i++) {
    var id = HEAP32[(vaos + i * 4) >> 2];
    GLctx["deleteVertexArray"](GL.vaos[id]);
    GL.vaos[id] = null;
  }
}
function _emscripten_glDeleteVertexArraysOES(n, vaos) {
  for (var i = 0; i < n; i++) {
    var id = HEAP32[(vaos + i * 4) >> 2];
    GLctx["deleteVertexArray"](GL.vaos[id]);
    GL.vaos[id] = null;
  }
}
function _emscripten_glDepthFunc(x0) {
  GLctx["depthFunc"](x0);
}
function _emscripten_glDepthMask(flag) {
  GLctx.depthMask(!!flag);
}
function _emscripten_glDepthRangef(x0, x1) {
  GLctx["depthRange"](x0, x1);
}
function _emscripten_glDetachShader(program, shader) {
  GLctx.detachShader(GL.programs[program], GL.shaders[shader]);
}
function _emscripten_glDisable(x0) {
  GLctx["disable"](x0);
}
function _emscripten_glDisableVertexAttribArray(index) {
  GLctx.disableVertexAttribArray(index);
}
function _emscripten_glDrawArrays(mode, first, count) {
  GLctx.drawArrays(mode, first, count);
}
function _emscripten_glDrawArraysInstanced(mode, first, count, primcount) {
  GLctx["drawArraysInstanced"](mode, first, count, primcount);
}
function _emscripten_glDrawArraysInstancedANGLE(mode, first, count, primcount) {
  GLctx["drawArraysInstanced"](mode, first, count, primcount);
}
function _emscripten_glDrawArraysInstancedARB(mode, first, count, primcount) {
  GLctx["drawArraysInstanced"](mode, first, count, primcount);
}
function _emscripten_glDrawArraysInstancedEXT(mode, first, count, primcount) {
  GLctx["drawArraysInstanced"](mode, first, count, primcount);
}
function _emscripten_glDrawArraysInstancedNV(mode, first, count, primcount) {
  GLctx["drawArraysInstanced"](mode, first, count, primcount);
}
var __tempFixedLengthArray = [];
function _emscripten_glDrawBuffers(n, bufs) {
  var bufArray = __tempFixedLengthArray[n];
  for (var i = 0; i < n; i++) {
    bufArray[i] = HEAP32[(bufs + i * 4) >> 2];
  }
  GLctx["drawBuffers"](bufArray);
}
function _emscripten_glDrawBuffersEXT(n, bufs) {
  var bufArray = __tempFixedLengthArray[n];
  for (var i = 0; i < n; i++) {
    bufArray[i] = HEAP32[(bufs + i * 4) >> 2];
  }
  GLctx["drawBuffers"](bufArray);
}
function _emscripten_glDrawBuffersWEBGL(n, bufs) {
  var bufArray = __tempFixedLengthArray[n];
  for (var i = 0; i < n; i++) {
    bufArray[i] = HEAP32[(bufs + i * 4) >> 2];
  }
  GLctx["drawBuffers"](bufArray);
}
function _emscripten_glDrawElements(mode, count, type, indices) {
  GLctx.drawElements(mode, count, type, indices);
}
function _emscripten_glDrawElementsInstanced(
  mode,
  count,
  type,
  indices,
  primcount
) {
  GLctx["drawElementsInstanced"](mode, count, type, indices, primcount);
}
function _emscripten_glDrawElementsInstancedANGLE(
  mode,
  count,
  type,
  indices,
  primcount
) {
  GLctx["drawElementsInstanced"](mode, count, type, indices, primcount);
}
function _emscripten_glDrawElementsInstancedARB(
  mode,
  count,
  type,
  indices,
  primcount
) {
  GLctx["drawElementsInstanced"](mode, count, type, indices, primcount);
}
function _emscripten_glDrawElementsInstancedEXT(
  mode,
  count,
  type,
  indices,
  primcount
) {
  GLctx["drawElementsInstanced"](mode, count, type, indices, primcount);
}
function _emscripten_glDrawElementsInstancedNV(
  mode,
  count,
  type,
  indices,
  primcount
) {
  GLctx["drawElementsInstanced"](mode, count, type, indices, primcount);
}
function _glDrawElements(mode, count, type, indices) {
  GLctx.drawElements(mode, count, type, indices);
}
function _emscripten_glDrawRangeElements(
  mode,
  start,
  end,
  count,
  type,
  indices
) {
  _glDrawElements(mode, count, type, indices);
}
function _emscripten_glEnable(x0) {
  GLctx["enable"](x0);
}
function _emscripten_glEnableVertexAttribArray(index) {
  GLctx.enableVertexAttribArray(index);
}
function _emscripten_glEndQuery(x0) {
  GLctx["endQuery"](x0);
}
function _emscripten_glEndQueryEXT(target) {
  GLctx.disjointTimerQueryExt["endQueryEXT"](target);
}
function _emscripten_glEndTransformFeedback() {
  GLctx["endTransformFeedback"]();
}
function _emscripten_glFenceSync(condition, flags) {
  var sync = GLctx.fenceSync(condition, flags);
  if (sync) {
    var id = GL.getNewId(GL.syncs);
    sync.name = id;
    GL.syncs[id] = sync;
    return id;
  } else {
    return 0;
  }
}
function _emscripten_glFinish() {
  GLctx["finish"]();
}
function _emscripten_glFlush() {
  GLctx["flush"]();
}
function _emscripten_glFramebufferRenderbuffer(
  target,
  attachment,
  renderbuffertarget,
  renderbuffer
) {
  GLctx.framebufferRenderbuffer(
    target,
    attachment,
    renderbuffertarget,
    GL.renderbuffers[renderbuffer]
  );
}
function _emscripten_glFramebufferTexture2D(
  target,
  attachment,
  textarget,
  texture,
  level
) {
  GLctx.framebufferTexture2D(
    target,
    attachment,
    textarget,
    GL.textures[texture],
    level
  );
}
function _emscripten_glFramebufferTextureLayer(
  target,
  attachment,
  texture,
  level,
  layer
) {
  GLctx.framebufferTextureLayer(
    target,
    attachment,
    GL.textures[texture],
    level,
    layer
  );
}
function _emscripten_glFrontFace(x0) {
  GLctx["frontFace"](x0);
}
function __glGenObject(n, buffers, createFunction, objectTable) {
  for (var i = 0; i < n; i++) {
    var buffer = GLctx[createFunction]();
    var id = buffer && GL.getNewId(objectTable);
    if (buffer) {
      buffer.name = id;
      objectTable[id] = buffer;
    } else {
      GL.recordError(1282);
    }
    HEAP32[(buffers + i * 4) >> 2] = id;
  }
}
function _emscripten_glGenBuffers(n, buffers) {
  __glGenObject(n, buffers, "createBuffer", GL.buffers);
}
function _emscripten_glGenFramebuffers(n, ids) {
  __glGenObject(n, ids, "createFramebuffer", GL.framebuffers);
}
function _emscripten_glGenQueries(n, ids) {
  __glGenObject(n, ids, "createQuery", GL.queries);
}
function _emscripten_glGenQueriesEXT(n, ids) {
  for (var i = 0; i < n; i++) {
    var query = GLctx.disjointTimerQueryExt["createQueryEXT"]();
    if (!query) {
      GL.recordError(1282);
      while (i < n) HEAP32[(ids + i++ * 4) >> 2] = 0;
      return;
    }
    var id = GL.getNewId(GL.timerQueriesEXT);
    query.name = id;
    GL.timerQueriesEXT[id] = query;
    HEAP32[(ids + i * 4) >> 2] = id;
  }
}
function _emscripten_glGenRenderbuffers(n, renderbuffers) {
  __glGenObject(n, renderbuffers, "createRenderbuffer", GL.renderbuffers);
}
function _emscripten_glGenSamplers(n, samplers) {
  __glGenObject(n, samplers, "createSampler", GL.samplers);
}
function _emscripten_glGenTextures(n, textures) {
  __glGenObject(n, textures, "createTexture", GL.textures);
}
function _emscripten_glGenTransformFeedbacks(n, ids) {
  __glGenObject(n, ids, "createTransformFeedback", GL.transformFeedbacks);
}
function _emscripten_glGenVertexArrays(n, arrays) {
  __glGenObject(n, arrays, "createVertexArray", GL.vaos);
}
function _emscripten_glGenVertexArraysOES(n, arrays) {
  __glGenObject(n, arrays, "createVertexArray", GL.vaos);
}
function _emscripten_glGenerateMipmap(x0) {
  GLctx["generateMipmap"](x0);
}
function _emscripten_glGetActiveAttrib(
  program,
  index,
  bufSize,
  length,
  size,
  type,
  name
) {
  program = GL.programs[program];
  var info = GLctx.getActiveAttrib(program, index);
  if (!info) return;
  var numBytesWrittenExclNull =
    bufSize > 0 && name ? stringToUTF8(info.name, name, bufSize) : 0;
  if (length) HEAP32[length >> 2] = numBytesWrittenExclNull;
  if (size) HEAP32[size >> 2] = info.size;
  if (type) HEAP32[type >> 2] = info.type;
}
function _emscripten_glGetActiveUniform(
  program,
  index,
  bufSize,
  length,
  size,
  type,
  name
) {
  program = GL.programs[program];
  var info = GLctx.getActiveUniform(program, index);
  if (!info) return;
  var numBytesWrittenExclNull =
    bufSize > 0 && name ? stringToUTF8(info.name, name, bufSize) : 0;
  if (length) HEAP32[length >> 2] = numBytesWrittenExclNull;
  if (size) HEAP32[size >> 2] = info.size;
  if (type) HEAP32[type >> 2] = info.type;
}
function _emscripten_glGetActiveUniformBlockName(
  program,
  uniformBlockIndex,
  bufSize,
  length,
  uniformBlockName
) {
  program = GL.programs[program];
  var result = GLctx["getActiveUniformBlockName"](program, uniformBlockIndex);
  if (!result) return;
  if (uniformBlockName && bufSize > 0) {
    var numBytesWrittenExclNull = stringToUTF8(
      result,
      uniformBlockName,
      bufSize
    );
    if (length) HEAP32[length >> 2] = numBytesWrittenExclNull;
  } else {
    if (length) HEAP32[length >> 2] = 0;
  }
}
function _emscripten_glGetActiveUniformBlockiv(
  program,
  uniformBlockIndex,
  pname,
  params
) {
  if (!params) {
    GL.recordError(1281);
    return;
  }
  program = GL.programs[program];
  switch (pname) {
    case 35393:
      var name = GLctx["getActiveUniformBlockName"](program, uniformBlockIndex);
      HEAP32[params >> 2] = name.length + 1;
      return;
    default:
      var result = GLctx["getActiveUniformBlockParameter"](
        program,
        uniformBlockIndex,
        pname
      );
      if (!result) return;
      if (typeof result == "number") {
        HEAP32[params >> 2] = result;
      } else {
        for (var i = 0; i < result.length; i++) {
          HEAP32[(params + i * 4) >> 2] = result[i];
        }
      }
  }
}
function _emscripten_glGetActiveUniformsiv(
  program,
  uniformCount,
  uniformIndices,
  pname,
  params
) {
  if (!params) {
    GL.recordError(1281);
    return;
  }
  if (uniformCount > 0 && uniformIndices == 0) {
    GL.recordError(1281);
    return;
  }
  program = GL.programs[program];
  var ids = [];
  for (var i = 0; i < uniformCount; i++) {
    ids.push(HEAP32[(uniformIndices + i * 4) >> 2]);
  }
  var result = GLctx["getActiveUniforms"](program, ids, pname);
  if (!result) return;
  var len = result.length;
  for (var i = 0; i < len; i++) {
    HEAP32[(params + i * 4) >> 2] = result[i];
  }
}
function _emscripten_glGetAttachedShaders(program, maxCount, count, shaders) {
  var result = GLctx.getAttachedShaders(GL.programs[program]);
  var len = result.length;
  if (len > maxCount) {
    len = maxCount;
  }
  HEAP32[count >> 2] = len;
  for (var i = 0; i < len; ++i) {
    var id = GL.shaders.indexOf(result[i]);
    HEAP32[(shaders + i * 4) >> 2] = id;
  }
}
function _emscripten_glGetAttribLocation(program, name) {
  return GLctx.getAttribLocation(GL.programs[program], UTF8ToString(name));
}
function writeI53ToI64(ptr, num) {
  HEAPU32[ptr >> 2] = num;
  HEAPU32[(ptr + 4) >> 2] = (num - HEAPU32[ptr >> 2]) / 4294967296;
}
function emscriptenWebGLGet(name_, p, type) {
  if (!p) {
    GL.recordError(1281);
    return;
  }
  var ret = undefined;
  switch (name_) {
    case 36346:
      ret = 1;
      break;
    case 36344:
      if (type != 0 && type != 1) {
        GL.recordError(1280);
      }
      return;
    case 34814:
    case 36345:
      ret = 0;
      break;
    case 34466:
      var formats = GLctx.getParameter(34467);
      ret = formats ? formats.length : 0;
      break;
    case 33309:
      if (GL.currentContext.version < 2) {
        GL.recordError(1282);
        return;
      }
      var exts = GLctx.getSupportedExtensions() || [];
      ret = 2 * exts.length;
      break;
    case 33307:
    case 33308:
      if (GL.currentContext.version < 2) {
        GL.recordError(1280);
        return;
      }
      ret = name_ == 33307 ? 3 : 0;
      break;
  }
  if (ret === undefined) {
    var result = GLctx.getParameter(name_);
    switch (typeof result) {
      case "number":
        ret = result;
        break;
      case "boolean":
        ret = result ? 1 : 0;
        break;
      case "string":
        GL.recordError(1280);
        return;
      case "object":
        if (result === null) {
          switch (name_) {
            case 34964:
            case 35725:
            case 34965:
            case 36006:
            case 36007:
            case 32873:
            case 34229:
            case 35097:
            case 36389:
            case 34068: {
              ret = 0;
              break;
            }
            default: {
              GL.recordError(1280);
              return;
            }
          }
        } else if (
          result instanceof Float32Array ||
          result instanceof Uint32Array ||
          result instanceof Int32Array ||
          result instanceof Array
        ) {
          for (var i = 0; i < result.length; ++i) {
            switch (type) {
              case 0:
                HEAP32[(p + i * 4) >> 2] = result[i];
                break;
              case 2:
                HEAPF32[(p + i * 4) >> 2] = result[i];
                break;
              case 4:
                HEAP8[(p + i) >> 0] = result[i] ? 1 : 0;
                break;
            }
          }
          return;
        } else {
          try {
            ret = result.name | 0;
          } catch (e) {
            GL.recordError(1280);
            err(
              "GL_INVALID_ENUM in glGet" +
                type +
                "v: Unknown object returned from WebGL getParameter(" +
                name_ +
                ")! (error: " +
                e +
                ")"
            );
            return;
          }
        }
        break;
      default:
        GL.recordError(1280);
        err(
          "GL_INVALID_ENUM in glGet" +
            type +
            "v: Native code calling glGet" +
            type +
            "v(" +
            name_ +
            ") and it returns " +
            result +
            " of type " +
            typeof result +
            "!"
        );
        return;
    }
  }
  switch (type) {
    case 1:
      writeI53ToI64(p, ret);
      break;
    case 0:
      HEAP32[p >> 2] = ret;
      break;
    case 2:
      HEAPF32[p >> 2] = ret;
      break;
    case 4:
      HEAP8[p >> 0] = ret ? 1 : 0;
      break;
  }
}
function _emscripten_glGetBooleanv(name_, p) {
  emscriptenWebGLGet(name_, p, 4);
}
function _emscripten_glGetBufferParameteri64v(target, value, data) {
  if (!data) {
    GL.recordError(1281);
    return;
  }
  writeI53ToI64(data, GLctx.getBufferParameter(target, value));
}
function _emscripten_glGetBufferParameteriv(target, value, data) {
  if (!data) {
    GL.recordError(1281);
    return;
  }
  HEAP32[data >> 2] = GLctx.getBufferParameter(target, value);
}
function _emscripten_glGetError() {
  var error = GLctx.getError() || GL.lastError;
  GL.lastError = 0;
  return error;
}
function _emscripten_glGetFloatv(name_, p) {
  emscriptenWebGLGet(name_, p, 2);
}
function _emscripten_glGetFragDataLocation(program, name) {
  return GLctx["getFragDataLocation"](GL.programs[program], UTF8ToString(name));
}
function _emscripten_glGetFramebufferAttachmentParameteriv(
  target,
  attachment,
  pname,
  params
) {
  var result = GLctx.getFramebufferAttachmentParameter(
    target,
    attachment,
    pname
  );
  if (result instanceof WebGLRenderbuffer || result instanceof WebGLTexture) {
    result = result.name | 0;
  }
  HEAP32[params >> 2] = result;
}
function emscriptenWebGLGetIndexed(target, index, data, type) {
  if (!data) {
    GL.recordError(1281);
    return;
  }
  var result = GLctx["getIndexedParameter"](target, index);
  var ret;
  switch (typeof result) {
    case "boolean":
      ret = result ? 1 : 0;
      break;
    case "number":
      ret = result;
      break;
    case "object":
      if (result === null) {
        switch (target) {
          case 35983:
          case 35368:
            ret = 0;
            break;
          default: {
            GL.recordError(1280);
            return;
          }
        }
      } else if (result instanceof WebGLBuffer) {
        ret = result.name | 0;
      } else {
        GL.recordError(1280);
        return;
      }
      break;
    default:
      GL.recordError(1280);
      return;
  }
  switch (type) {
    case 1:
      writeI53ToI64(data, ret);
      break;
    case 0:
      HEAP32[data >> 2] = ret;
      break;
    case 2:
      HEAPF32[data >> 2] = ret;
      break;
    case 4:
      HEAP8[data >> 0] = ret ? 1 : 0;
      break;
    default:
      throw "internal emscriptenWebGLGetIndexed() error, bad type: " + type;
  }
}
function _emscripten_glGetInteger64i_v(target, index, data) {
  emscriptenWebGLGetIndexed(target, index, data, 1);
}
function _emscripten_glGetInteger64v(name_, p) {
  emscriptenWebGLGet(name_, p, 1);
}
function _emscripten_glGetIntegeri_v(target, index, data) {
  emscriptenWebGLGetIndexed(target, index, data, 0);
}
function _emscripten_glGetIntegerv(name_, p) {
  emscriptenWebGLGet(name_, p, 0);
}
function _emscripten_glGetInternalformativ(
  target,
  internalformat,
  pname,
  bufSize,
  params
) {
  if (bufSize < 0) {
    GL.recordError(1281);
    return;
  }
  if (!params) {
    GL.recordError(1281);
    return;
  }
  var ret = GLctx["getInternalformatParameter"](target, internalformat, pname);
  if (ret === null) return;
  for (var i = 0; i < ret.length && i < bufSize; ++i) {
    HEAP32[(params + i) >> 2] = ret[i];
  }
}
function _emscripten_glGetProgramBinary(
  program,
  bufSize,
  length,
  binaryFormat,
  binary
) {
  GL.recordError(1282);
}
function _emscripten_glGetProgramInfoLog(program, maxLength, length, infoLog) {
  var log = GLctx.getProgramInfoLog(GL.programs[program]);
  if (log === null) log = "(unknown error)";
  var numBytesWrittenExclNull =
    maxLength > 0 && infoLog ? stringToUTF8(log, infoLog, maxLength) : 0;
  if (length) HEAP32[length >> 2] = numBytesWrittenExclNull;
}
function _emscripten_glGetProgramiv(program, pname, p) {
  if (!p) {
    GL.recordError(1281);
    return;
  }
  if (program >= GL.counter) {
    GL.recordError(1281);
    return;
  }
  var ptable = GL.programInfos[program];
  if (!ptable) {
    GL.recordError(1282);
    return;
  }
  if (pname == 35716) {
    var log = GLctx.getProgramInfoLog(GL.programs[program]);
    if (log === null) log = "(unknown error)";
    HEAP32[p >> 2] = log.length + 1;
  } else if (pname == 35719) {
    HEAP32[p >> 2] = ptable.maxUniformLength;
  } else if (pname == 35722) {
    if (ptable.maxAttributeLength == -1) {
      program = GL.programs[program];
      var numAttribs = GLctx.getProgramParameter(program, 35721);
      ptable.maxAttributeLength = 0;
      for (var i = 0; i < numAttribs; ++i) {
        var activeAttrib = GLctx.getActiveAttrib(program, i);
        ptable.maxAttributeLength = Math.max(
          ptable.maxAttributeLength,
          activeAttrib.name.length + 1
        );
      }
    }
    HEAP32[p >> 2] = ptable.maxAttributeLength;
  } else if (pname == 35381) {
    if (ptable.maxUniformBlockNameLength == -1) {
      program = GL.programs[program];
      var numBlocks = GLctx.getProgramParameter(program, 35382);
      ptable.maxUniformBlockNameLength = 0;
      for (var i = 0; i < numBlocks; ++i) {
        var activeBlockName = GLctx.getActiveUniformBlockName(program, i);
        ptable.maxUniformBlockNameLength = Math.max(
          ptable.maxUniformBlockNameLength,
          activeBlockName.length + 1
        );
      }
    }
    HEAP32[p >> 2] = ptable.maxUniformBlockNameLength;
  } else {
    HEAP32[p >> 2] = GLctx.getProgramParameter(GL.programs[program], pname);
  }
}
function _emscripten_glGetQueryObjecti64vEXT(id, pname, params) {
  if (!params) {
    GL.recordError(1281);
    return;
  }
  var query = GL.timerQueriesEXT[id];
  var param = GLctx.disjointTimerQueryExt["getQueryObjectEXT"](query, pname);
  var ret;
  if (typeof param == "boolean") {
    ret = param ? 1 : 0;
  } else {
    ret = param;
  }
  writeI53ToI64(params, ret);
}
function _emscripten_glGetQueryObjectivEXT(id, pname, params) {
  if (!params) {
    GL.recordError(1281);
    return;
  }
  var query = GL.timerQueriesEXT[id];
  var param = GLctx.disjointTimerQueryExt["getQueryObjectEXT"](query, pname);
  var ret;
  if (typeof param == "boolean") {
    ret = param ? 1 : 0;
  } else {
    ret = param;
  }
  HEAP32[params >> 2] = ret;
}
function _emscripten_glGetQueryObjectui64vEXT(id, pname, params) {
  if (!params) {
    GL.recordError(1281);
    return;
  }
  var query = GL.timerQueriesEXT[id];
  var param = GLctx.disjointTimerQueryExt["getQueryObjectEXT"](query, pname);
  var ret;
  if (typeof param == "boolean") {
    ret = param ? 1 : 0;
  } else {
    ret = param;
  }
  writeI53ToI64(params, ret);
}
function _emscripten_glGetQueryObjectuiv(id, pname, params) {
  if (!params) {
    GL.recordError(1281);
    return;
  }
  var query = GL.queries[id];
  var param = GLctx["getQueryParameter"](query, pname);
  var ret;
  if (typeof param == "boolean") {
    ret = param ? 1 : 0;
  } else {
    ret = param;
  }
  HEAP32[params >> 2] = ret;
}
function _emscripten_glGetQueryObjectuivEXT(id, pname, params) {
  if (!params) {
    GL.recordError(1281);
    return;
  }
  var query = GL.timerQueriesEXT[id];
  var param = GLctx.disjointTimerQueryExt["getQueryObjectEXT"](query, pname);
  var ret;
  if (typeof param == "boolean") {
    ret = param ? 1 : 0;
  } else {
    ret = param;
  }
  HEAP32[params >> 2] = ret;
}
function _emscripten_glGetQueryiv(target, pname, params) {
  if (!params) {
    GL.recordError(1281);
    return;
  }
  HEAP32[params >> 2] = GLctx["getQuery"](target, pname);
}
function _emscripten_glGetQueryivEXT(target, pname, params) {
  if (!params) {
    GL.recordError(1281);
    return;
  }
  HEAP32[params >> 2] = GLctx.disjointTimerQueryExt["getQueryEXT"](
    target,
    pname
  );
}
function _emscripten_glGetRenderbufferParameteriv(target, pname, params) {
  if (!params) {
    GL.recordError(1281);
    return;
  }
  HEAP32[params >> 2] = GLctx.getRenderbufferParameter(target, pname);
}
function _emscripten_glGetSamplerParameterfv(sampler, pname, params) {
  if (!params) {
    GL.recordError(1281);
    return;
  }
  sampler = GL.samplers[sampler];
  HEAPF32[params >> 2] = GLctx["getSamplerParameter"](sampler, pname);
}
function _emscripten_glGetSamplerParameteriv(sampler, pname, params) {
  if (!params) {
    GL.recordError(1281);
    return;
  }
  sampler = GL.samplers[sampler];
  HEAP32[params >> 2] = GLctx["getSamplerParameter"](sampler, pname);
}
function _emscripten_glGetShaderInfoLog(shader, maxLength, length, infoLog) {
  var log = GLctx.getShaderInfoLog(GL.shaders[shader]);
  if (log === null) log = "(unknown error)";
  var numBytesWrittenExclNull =
    maxLength > 0 && infoLog ? stringToUTF8(log, infoLog, maxLength) : 0;
  if (length) HEAP32[length >> 2] = numBytesWrittenExclNull;
}
function _emscripten_glGetShaderPrecisionFormat(
  shaderType,
  precisionType,
  range,
  precision
) {
  var result = GLctx.getShaderPrecisionFormat(shaderType, precisionType);
  HEAP32[range >> 2] = result.rangeMin;
  HEAP32[(range + 4) >> 2] = result.rangeMax;
  HEAP32[precision >> 2] = result.precision;
}
function _emscripten_glGetShaderSource(shader, bufSize, length, source) {
  var result = GLctx.getShaderSource(GL.shaders[shader]);
  if (!result) return;
  var numBytesWrittenExclNull =
    bufSize > 0 && source ? stringToUTF8(result, source, bufSize) : 0;
  if (length) HEAP32[length >> 2] = numBytesWrittenExclNull;
}
function _emscripten_glGetShaderiv(shader, pname, p) {
  if (!p) {
    GL.recordError(1281);
    return;
  }
  if (pname == 35716) {
    var log = GLctx.getShaderInfoLog(GL.shaders[shader]);
    if (log === null) log = "(unknown error)";
    HEAP32[p >> 2] = log.length + 1;
  } else if (pname == 35720) {
    var source = GLctx.getShaderSource(GL.shaders[shader]);
    var sourceLength =
      source === null || source.length == 0 ? 0 : source.length + 1;
    HEAP32[p >> 2] = sourceLength;
  } else {
    HEAP32[p >> 2] = GLctx.getShaderParameter(GL.shaders[shader], pname);
  }
}
function stringToNewUTF8(jsString) {
  var length = lengthBytesUTF8(jsString) + 1;
  var cString = _malloc(length);
  stringToUTF8(jsString, cString, length);
  return cString;
}
function _emscripten_glGetString(name_) {
  if (GL.stringCache[name_]) return GL.stringCache[name_];
  var ret;
  switch (name_) {
    case 7939:
      var exts = GLctx.getSupportedExtensions() || [];
      exts = exts.concat(
        exts.map(function (e) {
          return "GL_" + e;
        })
      );
      ret = stringToNewUTF8(exts.join(" "));
      break;
    case 7936:
    case 7937:
    case 37445:
    case 37446:
      var s = GLctx.getParameter(name_);
      if (!s) {
        GL.recordError(1280);
      }
      ret = stringToNewUTF8(s);
      break;
    case 7938:
      var glVersion = GLctx.getParameter(7938);
      if (GL.currentContext.version >= 2)
        glVersion = "OpenGL ES 3.0 (" + glVersion + ")";
      else {
        glVersion = "OpenGL ES 2.0 (" + glVersion + ")";
      }
      ret = stringToNewUTF8(glVersion);
      break;
    case 35724:
      var glslVersion = GLctx.getParameter(35724);
      var ver_re = /^WebGL GLSL ES ([0-9]\.[0-9][0-9]?)(?:$| .*)/;
      var ver_num = glslVersion.match(ver_re);
      if (ver_num !== null) {
        if (ver_num[1].length == 3) ver_num[1] = ver_num[1] + "0";
        glslVersion =
          "OpenGL ES GLSL ES " + ver_num[1] + " (" + glslVersion + ")";
      }
      ret = stringToNewUTF8(glslVersion);
      break;
    default:
      GL.recordError(1280);
      return 0;
  }
  GL.stringCache[name_] = ret;
  return ret;
}
function _emscripten_glGetStringi(name, index) {
  if (GL.currentContext.version < 2) {
    GL.recordError(1282);
    return 0;
  }
  var stringiCache = GL.stringiCache[name];
  if (stringiCache) {
    if (index < 0 || index >= stringiCache.length) {
      GL.recordError(1281);
      return 0;
    }
    return stringiCache[index];
  }
  switch (name) {
    case 7939:
      var exts = GLctx.getSupportedExtensions() || [];
      exts = exts.concat(
        exts.map(function (e) {
          return "GL_" + e;
        })
      );
      exts = exts.map(function (e) {
        return stringToNewUTF8(e);
      });
      stringiCache = GL.stringiCache[name] = exts;
      if (index < 0 || index >= stringiCache.length) {
        GL.recordError(1281);
        return 0;
      }
      return stringiCache[index];
    default:
      GL.recordError(1280);
      return 0;
  }
}
function _emscripten_glGetSynciv(sync, pname, bufSize, length, values) {
  if (bufSize < 0) {
    GL.recordError(1281);
    return;
  }
  if (!values) {
    GL.recordError(1281);
    return;
  }
  var ret = GLctx.getSyncParameter(GL.syncs[sync], pname);
  HEAP32[length >> 2] = ret;
  if (ret !== null && length) HEAP32[length >> 2] = 1;
}
function _emscripten_glGetTexParameterfv(target, pname, params) {
  if (!params) {
    GL.recordError(1281);
    return;
  }
  HEAPF32[params >> 2] = GLctx.getTexParameter(target, pname);
}
function _emscripten_glGetTexParameteriv(target, pname, params) {
  if (!params) {
    GL.recordError(1281);
    return;
  }
  HEAP32[params >> 2] = GLctx.getTexParameter(target, pname);
}
function _emscripten_glGetTransformFeedbackVarying(
  program,
  index,
  bufSize,
  length,
  size,
  type,
  name
) {
  program = GL.programs[program];
  var info = GLctx["getTransformFeedbackVarying"](program, index);
  if (!info) return;
  if (name && bufSize > 0) {
    var numBytesWrittenExclNull = stringToUTF8(info.name, name, bufSize);
    if (length) HEAP32[length >> 2] = numBytesWrittenExclNull;
  } else {
    if (length) HEAP32[length >> 2] = 0;
  }
  if (size) HEAP32[size >> 2] = info.size;
  if (type) HEAP32[type >> 2] = info.type;
}
function _emscripten_glGetUniformBlockIndex(program, uniformBlockName) {
  return GLctx["getUniformBlockIndex"](
    GL.programs[program],
    UTF8ToString(uniformBlockName)
  );
}
function _emscripten_glGetUniformIndices(
  program,
  uniformCount,
  uniformNames,
  uniformIndices
) {
  if (!uniformIndices) {
    GL.recordError(1281);
    return;
  }
  if (uniformCount > 0 && (uniformNames == 0 || uniformIndices == 0)) {
    GL.recordError(1281);
    return;
  }
  program = GL.programs[program];
  var names = [];
  for (var i = 0; i < uniformCount; i++)
    names.push(UTF8ToString(HEAP32[(uniformNames + i * 4) >> 2]));
  var result = GLctx["getUniformIndices"](program, names);
  if (!result) return;
  var len = result.length;
  for (var i = 0; i < len; i++) {
    HEAP32[(uniformIndices + i * 4) >> 2] = result[i];
  }
}
function jstoi_q(str) {
  return parseInt(str, undefined);
}
function _emscripten_glGetUniformLocation(program, name) {
  name = UTF8ToString(name);
  var arrayIndex = 0;
  if (name[name.length - 1] == "]") {
    var leftBrace = name.lastIndexOf("[");
    arrayIndex =
      name[leftBrace + 1] != "]" ? jstoi_q(name.slice(leftBrace + 1)) : 0;
    name = name.slice(0, leftBrace);
  }
  var uniformInfo =
    GL.programInfos[program] && GL.programInfos[program].uniforms[name];
  if (uniformInfo && arrayIndex >= 0 && arrayIndex < uniformInfo[0]) {
    return uniformInfo[1] + arrayIndex;
  } else {
    return -1;
  }
}
function emscriptenWebGLGetUniform(program, location, params, type) {
  if (!params) {
    GL.recordError(1281);
    return;
  }
  var data = GLctx.getUniform(GL.programs[program], GL.uniforms[location]);
  if (typeof data == "number" || typeof data == "boolean") {
    switch (type) {
      case 0:
        HEAP32[params >> 2] = data;
        break;
      case 2:
        HEAPF32[params >> 2] = data;
        break;
      default:
        throw "internal emscriptenWebGLGetUniform() error, bad type: " + type;
    }
  } else {
    for (var i = 0; i < data.length; i++) {
      switch (type) {
        case 0:
          HEAP32[(params + i * 4) >> 2] = data[i];
          break;
        case 2:
          HEAPF32[(params + i * 4) >> 2] = data[i];
          break;
        default:
          throw "internal emscriptenWebGLGetUniform() error, bad type: " + type;
      }
    }
  }
}
function _emscripten_glGetUniformfv(program, location, params) {
  emscriptenWebGLGetUniform(program, location, params, 2);
}
function _emscripten_glGetUniformiv(program, location, params) {
  emscriptenWebGLGetUniform(program, location, params, 0);
}
function _emscripten_glGetUniformuiv(program, location, params) {
  emscriptenWebGLGetUniform(program, location, params, 0);
}
function emscriptenWebGLGetVertexAttrib(index, pname, params, type) {
  if (!params) {
    GL.recordError(1281);
    return;
  }
  var data = GLctx.getVertexAttrib(index, pname);
  if (pname == 34975) {
    HEAP32[params >> 2] = data["name"];
  } else if (typeof data == "number" || typeof data == "boolean") {
    switch (type) {
      case 0:
        HEAP32[params >> 2] = data;
        break;
      case 2:
        HEAPF32[params >> 2] = data;
        break;
      case 5:
        HEAP32[params >> 2] = Math.fround(data);
        break;
      default:
        throw (
          "internal emscriptenWebGLGetVertexAttrib() error, bad type: " + type
        );
    }
  } else {
    for (var i = 0; i < data.length; i++) {
      switch (type) {
        case 0:
          HEAP32[(params + i * 4) >> 2] = data[i];
          break;
        case 2:
          HEAPF32[(params + i * 4) >> 2] = data[i];
          break;
        case 5:
          HEAP32[(params + i * 4) >> 2] = Math.fround(data[i]);
          break;
        default:
          throw (
            "internal emscriptenWebGLGetVertexAttrib() error, bad type: " + type
          );
      }
    }
  }
}
function _emscripten_glGetVertexAttribIiv(index, pname, params) {
  emscriptenWebGLGetVertexAttrib(index, pname, params, 0);
}
function _emscripten_glGetVertexAttribIuiv(index, pname, params) {
  emscriptenWebGLGetVertexAttrib(index, pname, params, 0);
}
function _emscripten_glGetVertexAttribPointerv(index, pname, pointer) {
  if (!pointer) {
    GL.recordError(1281);
    return;
  }
  HEAP32[pointer >> 2] = GLctx.getVertexAttribOffset(index, pname);
}
function _emscripten_glGetVertexAttribfv(index, pname, params) {
  emscriptenWebGLGetVertexAttrib(index, pname, params, 2);
}
function _emscripten_glGetVertexAttribiv(index, pname, params) {
  emscriptenWebGLGetVertexAttrib(index, pname, params, 5);
}
function _emscripten_glHint(x0, x1) {
  GLctx["hint"](x0, x1);
}
function _emscripten_glInvalidateFramebuffer(
  target,
  numAttachments,
  attachments
) {
  var list = __tempFixedLengthArray[numAttachments];
  for (var i = 0; i < numAttachments; i++) {
    list[i] = HEAP32[(attachments + i * 4) >> 2];
  }
  GLctx["invalidateFramebuffer"](target, list);
}
function _emscripten_glInvalidateSubFramebuffer(
  target,
  numAttachments,
  attachments,
  x,
  y,
  width,
  height
) {
  var list = __tempFixedLengthArray[numAttachments];
  for (var i = 0; i < numAttachments; i++) {
    list[i] = HEAP32[(attachments + i * 4) >> 2];
  }
  GLctx["invalidateSubFramebuffer"](target, list, x, y, width, height);
}
function _emscripten_glIsBuffer(buffer) {
  var b = GL.buffers[buffer];
  if (!b) return 0;
  return GLctx.isBuffer(b);
}
function _emscripten_glIsEnabled(x0) {
  return GLctx["isEnabled"](x0);
}
function _emscripten_glIsFramebuffer(framebuffer) {
  var fb = GL.framebuffers[framebuffer];
  if (!fb) return 0;
  return GLctx.isFramebuffer(fb);
}
function _emscripten_glIsProgram(program) {
  program = GL.programs[program];
  if (!program) return 0;
  return GLctx.isProgram(program);
}
function _emscripten_glIsQuery(id) {
  var query = GL.queries[id];
  if (!query) return 0;
  return GLctx["isQuery"](query);
}
function _emscripten_glIsQueryEXT(id) {
  var query = GL.timerQueriesEXT[id];
  if (!query) return 0;
  return GLctx.disjointTimerQueryExt["isQueryEXT"](query);
}
function _emscripten_glIsRenderbuffer(renderbuffer) {
  var rb = GL.renderbuffers[renderbuffer];
  if (!rb) return 0;
  return GLctx.isRenderbuffer(rb);
}
function _emscripten_glIsSampler(id) {
  var sampler = GL.samplers[id];
  if (!sampler) return 0;
  return GLctx["isSampler"](sampler);
}
function _emscripten_glIsShader(shader) {
  var s = GL.shaders[shader];
  if (!s) return 0;
  return GLctx.isShader(s);
}
function _emscripten_glIsSync(sync) {
  return GLctx.isSync(GL.syncs[sync]);
}
function _emscripten_glIsTexture(id) {
  var texture = GL.textures[id];
  if (!texture) return 0;
  return GLctx.isTexture(texture);
}
function _emscripten_glIsTransformFeedback(id) {
  return GLctx["isTransformFeedback"](GL.transformFeedbacks[id]);
}
function _emscripten_glIsVertexArray(array) {
  var vao = GL.vaos[array];
  if (!vao) return 0;
  return GLctx["isVertexArray"](vao);
}
function _emscripten_glIsVertexArrayOES(array) {
  var vao = GL.vaos[array];
  if (!vao) return 0;
  return GLctx["isVertexArray"](vao);
}
function _emscripten_glLineWidth(x0) {
  GLctx["lineWidth"](x0);
}
function _emscripten_glLinkProgram(program) {
  GLctx.linkProgram(GL.programs[program]);
  GL.populateUniformTable(program);
}
function _emscripten_glPauseTransformFeedback() {
  GLctx["pauseTransformFeedback"]();
}
function _emscripten_glPixelStorei(pname, param) {
  if (pname == 3317) {
    GL.unpackAlignment = param;
  }
  GLctx.pixelStorei(pname, param);
}
function _emscripten_glPolygonOffset(x0, x1) {
  GLctx["polygonOffset"](x0, x1);
}
function _emscripten_glProgramBinary(program, binaryFormat, binary, length) {
  GL.recordError(1280);
}
function _emscripten_glProgramParameteri(program, pname, value) {
  GL.recordError(1280);
}
function _emscripten_glQueryCounterEXT(id, target) {
  GLctx.disjointTimerQueryExt["queryCounterEXT"](
    GL.timerQueriesEXT[id],
    target
  );
}
function _emscripten_glReadBuffer(x0) {
  GLctx["readBuffer"](x0);
}
function __computeUnpackAlignedImageSize(
  width,
  height,
  sizePerPixel,
  alignment
) {
  function roundedToNextMultipleOf(x, y) {
    return (x + y - 1) & -y;
  }
  var plainRowSize = width * sizePerPixel;
  var alignedRowSize = roundedToNextMultipleOf(plainRowSize, alignment);
  return height * alignedRowSize;
}
function __colorChannelsInGlTextureFormat(format) {
  var colorChannels = {
    5: 3,
    6: 4,
    8: 2,
    29502: 3,
    29504: 4,
    26917: 2,
    26918: 2,
    29846: 3,
    29847: 4,
  };
  return colorChannels[format - 6402] || 1;
}
function __heapObjectForWebGLType(type) {
  type -= 5120;
  if (type == 0) return HEAP8;
  if (type == 1) return HEAPU8;
  if (type == 2) return HEAP16;
  if (type == 4) return HEAP32;
  if (type == 6) return HEAPF32;
  if (
    type == 5 ||
    type == 28922 ||
    type == 28520 ||
    type == 30779 ||
    type == 30782
  )
    return HEAPU32;
  return HEAPU16;
}
function __heapAccessShiftForWebGLHeap(heap) {
  return 31 - Math.clz32(heap.BYTES_PER_ELEMENT);
}
function emscriptenWebGLGetTexPixelData(
  type,
  format,
  width,
  height,
  pixels,
  internalFormat
) {
  var heap = __heapObjectForWebGLType(type);
  var shift = __heapAccessShiftForWebGLHeap(heap);
  var byteSize = 1 << shift;
  var sizePerPixel = __colorChannelsInGlTextureFormat(format) * byteSize;
  var bytes = __computeUnpackAlignedImageSize(
    width,
    height,
    sizePerPixel,
    GL.unpackAlignment
  );
  return heap.subarray(pixels >> shift, (pixels + bytes) >> shift);
}
function _emscripten_glReadPixels(x, y, width, height, format, type, pixels) {
  if (GL.currentContext.version >= 2) {
    if (GLctx.currentPixelPackBufferBinding) {
      GLctx.readPixels(x, y, width, height, format, type, pixels);
    } else {
      var heap = __heapObjectForWebGLType(type);
      GLctx.readPixels(
        x,
        y,
        width,
        height,
        format,
        type,
        heap,
        pixels >> __heapAccessShiftForWebGLHeap(heap)
      );
    }
    return;
  }
  var pixelData = emscriptenWebGLGetTexPixelData(
    type,
    format,
    width,
    height,
    pixels,
    format
  );
  if (!pixelData) {
    GL.recordError(1280);
    return;
  }
  GLctx.readPixels(x, y, width, height, format, type, pixelData);
}
function _emscripten_glReleaseShaderCompiler() {}
function _emscripten_glRenderbufferStorage(x0, x1, x2, x3) {
  GLctx["renderbufferStorage"](x0, x1, x2, x3);
}
function _emscripten_glRenderbufferStorageMultisample(x0, x1, x2, x3, x4) {
  GLctx["renderbufferStorageMultisample"](x0, x1, x2, x3, x4);
}
function _emscripten_glResumeTransformFeedback() {
  GLctx["resumeTransformFeedback"]();
}
function _emscripten_glSampleCoverage(value, invert) {
  GLctx.sampleCoverage(value, !!invert);
}
function _emscripten_glSamplerParameterf(sampler, pname, param) {
  GLctx["samplerParameterf"](GL.samplers[sampler], pname, param);
}
function _emscripten_glSamplerParameterfv(sampler, pname, params) {
  var param = HEAPF32[params >> 2];
  GLctx["samplerParameterf"](GL.samplers[sampler], pname, param);
}
function _emscripten_glSamplerParameteri(sampler, pname, param) {
  GLctx["samplerParameteri"](GL.samplers[sampler], pname, param);
}
function _emscripten_glSamplerParameteriv(sampler, pname, params) {
  var param = HEAP32[params >> 2];
  GLctx["samplerParameteri"](GL.samplers[sampler], pname, param);
}
function _emscripten_glScissor(x0, x1, x2, x3) {
  GLctx["scissor"](x0, x1, x2, x3);
}
function _emscripten_glShaderBinary() {
  GL.recordError(1280);
}
function _emscripten_glShaderSource(shader, count, string, length) {
  var source = GL.getSource(shader, count, string, length);
  GLctx.shaderSource(GL.shaders[shader], source);
}
function _emscripten_glStencilFunc(x0, x1, x2) {
  GLctx["stencilFunc"](x0, x1, x2);
}
function _emscripten_glStencilFuncSeparate(x0, x1, x2, x3) {
  GLctx["stencilFuncSeparate"](x0, x1, x2, x3);
}
function _emscripten_glStencilMask(x0) {
  GLctx["stencilMask"](x0);
}
function _emscripten_glStencilMaskSeparate(x0, x1) {
  GLctx["stencilMaskSeparate"](x0, x1);
}
function _emscripten_glStencilOp(x0, x1, x2) {
  GLctx["stencilOp"](x0, x1, x2);
}
function _emscripten_glStencilOpSeparate(x0, x1, x2, x3) {
  GLctx["stencilOpSeparate"](x0, x1, x2, x3);
}
function _emscripten_glTexImage2D(
  target,
  level,
  internalFormat,
  width,
  height,
  border,
  format,
  type,
  pixels
) {
  if (GL.currentContext.version >= 2) {
    if (GLctx.currentPixelUnpackBufferBinding) {
      GLctx.texImage2D(
        target,
        level,
        internalFormat,
        width,
        height,
        border,
        format,
        type,
        pixels
      );
    } else if (pixels) {
      var heap = __heapObjectForWebGLType(type);
      GLctx.texImage2D(
        target,
        level,
        internalFormat,
        width,
        height,
        border,
        format,
        type,
        heap,
        pixels >> __heapAccessShiftForWebGLHeap(heap)
      );
    } else {
      GLctx.texImage2D(
        target,
        level,
        internalFormat,
        width,
        height,
        border,
        format,
        type,
        null
      );
    }
    return;
  }
  GLctx.texImage2D(
    target,
    level,
    internalFormat,
    width,
    height,
    border,
    format,
    type,
    pixels
      ? emscriptenWebGLGetTexPixelData(
          type,
          format,
          width,
          height,
          pixels,
          internalFormat
        )
      : null
  );
}
function _emscripten_glTexImage3D(
  target,
  level,
  internalFormat,
  width,
  height,
  depth,
  border,
  format,
  type,
  pixels
) {
  if (GLctx.currentPixelUnpackBufferBinding) {
    GLctx["texImage3D"](
      target,
      level,
      internalFormat,
      width,
      height,
      depth,
      border,
      format,
      type,
      pixels
    );
  } else if (pixels) {
    var heap = __heapObjectForWebGLType(type);
    GLctx["texImage3D"](
      target,
      level,
      internalFormat,
      width,
      height,
      depth,
      border,
      format,
      type,
      heap,
      pixels >> __heapAccessShiftForWebGLHeap(heap)
    );
  } else {
    GLctx["texImage3D"](
      target,
      level,
      internalFormat,
      width,
      height,
      depth,
      border,
      format,
      type,
      null
    );
  }
}
function _emscripten_glTexParameterf(x0, x1, x2) {
  GLctx["texParameterf"](x0, x1, x2);
}
function _emscripten_glTexParameterfv(target, pname, params) {
  var param = HEAPF32[params >> 2];
  GLctx.texParameterf(target, pname, param);
}
function _emscripten_glTexParameteri(x0, x1, x2) {
  GLctx["texParameteri"](x0, x1, x2);
}
function _emscripten_glTexParameteriv(target, pname, params) {
  var param = HEAP32[params >> 2];
  GLctx.texParameteri(target, pname, param);
}
function _emscripten_glTexStorage2D(x0, x1, x2, x3, x4) {
  GLctx["texStorage2D"](x0, x1, x2, x3, x4);
}
function _emscripten_glTexStorage3D(x0, x1, x2, x3, x4, x5) {
  GLctx["texStorage3D"](x0, x1, x2, x3, x4, x5);
}
function _emscripten_glTexSubImage2D(
  target,
  level,
  xoffset,
  yoffset,
  width,
  height,
  format,
  type,
  pixels
) {
  if (GL.currentContext.version >= 2) {
    if (GLctx.currentPixelUnpackBufferBinding) {
      GLctx.texSubImage2D(
        target,
        level,
        xoffset,
        yoffset,
        width,
        height,
        format,
        type,
        pixels
      );
    } else if (pixels) {
      var heap = __heapObjectForWebGLType(type);
      GLctx.texSubImage2D(
        target,
        level,
        xoffset,
        yoffset,
        width,
        height,
        format,
        type,
        heap,
        pixels >> __heapAccessShiftForWebGLHeap(heap)
      );
    } else {
      GLctx.texSubImage2D(
        target,
        level,
        xoffset,
        yoffset,
        width,
        height,
        format,
        type,
        null
      );
    }
    return;
  }
  var pixelData = null;
  if (pixels)
    pixelData = emscriptenWebGLGetTexPixelData(
      type,
      format,
      width,
      height,
      pixels,
      0
    );
  GLctx.texSubImage2D(
    target,
    level,
    xoffset,
    yoffset,
    width,
    height,
    format,
    type,
    pixelData
  );
}
function _emscripten_glTexSubImage3D(
  target,
  level,
  xoffset,
  yoffset,
  zoffset,
  width,
  height,
  depth,
  format,
  type,
  pixels
) {
  if (GLctx.currentPixelUnpackBufferBinding) {
    GLctx["texSubImage3D"](
      target,
      level,
      xoffset,
      yoffset,
      zoffset,
      width,
      height,
      depth,
      format,
      type,
      pixels
    );
  } else if (pixels) {
    var heap = __heapObjectForWebGLType(type);
    GLctx["texSubImage3D"](
      target,
      level,
      xoffset,
      yoffset,
      zoffset,
      width,
      height,
      depth,
      format,
      type,
      heap,
      pixels >> __heapAccessShiftForWebGLHeap(heap)
    );
  } else {
    GLctx["texSubImage3D"](
      target,
      level,
      xoffset,
      yoffset,
      zoffset,
      width,
      height,
      depth,
      format,
      type,
      null
    );
  }
}
function _emscripten_glTransformFeedbackVaryings(
  program,
  count,
  varyings,
  bufferMode
) {
  program = GL.programs[program];
  var vars = [];
  for (var i = 0; i < count; i++)
    vars.push(UTF8ToString(HEAP32[(varyings + i * 4) >> 2]));
  GLctx["transformFeedbackVaryings"](program, vars, bufferMode);
}
function _emscripten_glUniform1f(location, v0) {
  GLctx.uniform1f(GL.uniforms[location], v0);
}
function _emscripten_glUniform1fv(location, count, value) {
  if (GL.currentContext.version >= 2) {
    GLctx.uniform1fv(GL.uniforms[location], HEAPF32, value >> 2, count);
    return;
  }
  if (count <= GL.MINI_TEMP_BUFFER_SIZE) {
    var view = GL.miniTempBufferFloatViews[count - 1];
    for (var i = 0; i < count; ++i) {
      view[i] = HEAPF32[(value + 4 * i) >> 2];
    }
  } else {
    var view = HEAPF32.subarray(value >> 2, (value + count * 4) >> 2);
  }
  GLctx.uniform1fv(GL.uniforms[location], view);
}
function _emscripten_glUniform1i(location, v0) {
  GLctx.uniform1i(GL.uniforms[location], v0);
}
function _emscripten_glUniform1iv(location, count, value) {
  if (GL.currentContext.version >= 2) {
    GLctx.uniform1iv(GL.uniforms[location], HEAP32, value >> 2, count);
    return;
  }
  if (count <= GL.MINI_TEMP_BUFFER_SIZE) {
    var view = GL.miniTempBufferIntViews[count - 1];
    for (var i = 0; i < count; ++i) {
      view[i] = HEAP32[(value + 4 * i) >> 2];
    }
  } else {
    var view = HEAP32.subarray(value >> 2, (value + count * 4) >> 2);
  }
  GLctx.uniform1iv(GL.uniforms[location], view);
}
function _emscripten_glUniform1ui(location, v0) {
  GLctx.uniform1ui(GL.uniforms[location], v0);
}
function _emscripten_glUniform1uiv(location, count, value) {
  GLctx.uniform1uiv(GL.uniforms[location], HEAPU32, value >> 2, count);
}
function _emscripten_glUniform2f(location, v0, v1) {
  GLctx.uniform2f(GL.uniforms[location], v0, v1);
}
function _emscripten_glUniform2fv(location, count, value) {
  if (GL.currentContext.version >= 2) {
    GLctx.uniform2fv(GL.uniforms[location], HEAPF32, value >> 2, count * 2);
    return;
  }
  if (2 * count <= GL.MINI_TEMP_BUFFER_SIZE) {
    var view = GL.miniTempBufferFloatViews[2 * count - 1];
    for (var i = 0; i < 2 * count; i += 2) {
      view[i] = HEAPF32[(value + 4 * i) >> 2];
      view[i + 1] = HEAPF32[(value + (4 * i + 4)) >> 2];
    }
  } else {
    var view = HEAPF32.subarray(value >> 2, (value + count * 8) >> 2);
  }
  GLctx.uniform2fv(GL.uniforms[location], view);
}
function _emscripten_glUniform2i(location, v0, v1) {
  GLctx.uniform2i(GL.uniforms[location], v0, v1);
}
function _emscripten_glUniform2iv(location, count, value) {
  if (GL.currentContext.version >= 2) {
    GLctx.uniform2iv(GL.uniforms[location], HEAP32, value >> 2, count * 2);
    return;
  }
  if (2 * count <= GL.MINI_TEMP_BUFFER_SIZE) {
    var view = GL.miniTempBufferIntViews[2 * count - 1];
    for (var i = 0; i < 2 * count; i += 2) {
      view[i] = HEAP32[(value + 4 * i) >> 2];
      view[i + 1] = HEAP32[(value + (4 * i + 4)) >> 2];
    }
  } else {
    var view = HEAP32.subarray(value >> 2, (value + count * 8) >> 2);
  }
  GLctx.uniform2iv(GL.uniforms[location], view);
}
function _emscripten_glUniform2ui(location, v0, v1) {
  GLctx.uniform2ui(GL.uniforms[location], v0, v1);
}
function _emscripten_glUniform2uiv(location, count, value) {
  GLctx.uniform2uiv(GL.uniforms[location], HEAPU32, value >> 2, count * 2);
}
function _emscripten_glUniform3f(location, v0, v1, v2) {
  GLctx.uniform3f(GL.uniforms[location], v0, v1, v2);
}
function _emscripten_glUniform3fv(location, count, value) {
  if (GL.currentContext.version >= 2) {
    GLctx.uniform3fv(GL.uniforms[location], HEAPF32, value >> 2, count * 3);
    return;
  }
  if (3 * count <= GL.MINI_TEMP_BUFFER_SIZE) {
    var view = GL.miniTempBufferFloatViews[3 * count - 1];
    for (var i = 0; i < 3 * count; i += 3) {
      view[i] = HEAPF32[(value + 4 * i) >> 2];
      view[i + 1] = HEAPF32[(value + (4 * i + 4)) >> 2];
      view[i + 2] = HEAPF32[(value + (4 * i + 8)) >> 2];
    }
  } else {
    var view = HEAPF32.subarray(value >> 2, (value + count * 12) >> 2);
  }
  GLctx.uniform3fv(GL.uniforms[location], view);
}
function _emscripten_glUniform3i(location, v0, v1, v2) {
  GLctx.uniform3i(GL.uniforms[location], v0, v1, v2);
}
function _emscripten_glUniform3iv(location, count, value) {
  if (GL.currentContext.version >= 2) {
    GLctx.uniform3iv(GL.uniforms[location], HEAP32, value >> 2, count * 3);
    return;
  }
  if (3 * count <= GL.MINI_TEMP_BUFFER_SIZE) {
    var view = GL.miniTempBufferIntViews[3 * count - 1];
    for (var i = 0; i < 3 * count; i += 3) {
      view[i] = HEAP32[(value + 4 * i) >> 2];
      view[i + 1] = HEAP32[(value + (4 * i + 4)) >> 2];
      view[i + 2] = HEAP32[(value + (4 * i + 8)) >> 2];
    }
  } else {
    var view = HEAP32.subarray(value >> 2, (value + count * 12) >> 2);
  }
  GLctx.uniform3iv(GL.uniforms[location], view);
}
function _emscripten_glUniform3ui(location, v0, v1, v2) {
  GLctx.uniform3ui(GL.uniforms[location], v0, v1, v2);
}
function _emscripten_glUniform3uiv(location, count, value) {
  GLctx.uniform3uiv(GL.uniforms[location], HEAPU32, value >> 2, count * 3);
}
function _emscripten_glUniform4f(location, v0, v1, v2, v3) {
  GLctx.uniform4f(GL.uniforms[location], v0, v1, v2, v3);
}
function _emscripten_glUniform4fv(location, count, value) {
  if (GL.currentContext.version >= 2) {
    GLctx.uniform4fv(GL.uniforms[location], HEAPF32, value >> 2, count * 4);
    return;
  }
  if (4 * count <= GL.MINI_TEMP_BUFFER_SIZE) {
    var view = GL.miniTempBufferFloatViews[4 * count - 1];
    for (var i = 0; i < 4 * count; i += 4) {
      view[i] = HEAPF32[(value + 4 * i) >> 2];
      view[i + 1] = HEAPF32[(value + (4 * i + 4)) >> 2];
      view[i + 2] = HEAPF32[(value + (4 * i + 8)) >> 2];
      view[i + 3] = HEAPF32[(value + (4 * i + 12)) >> 2];
    }
  } else {
    var view = HEAPF32.subarray(value >> 2, (value + count * 16) >> 2);
  }
  GLctx.uniform4fv(GL.uniforms[location], view);
}
function _emscripten_glUniform4i(location, v0, v1, v2, v3) {
  GLctx.uniform4i(GL.uniforms[location], v0, v1, v2, v3);
}
function _emscripten_glUniform4iv(location, count, value) {
  if (GL.currentContext.version >= 2) {
    GLctx.uniform4iv(GL.uniforms[location], HEAP32, value >> 2, count * 4);
    return;
  }
  if (4 * count <= GL.MINI_TEMP_BUFFER_SIZE) {
    var view = GL.miniTempBufferIntViews[4 * count - 1];
    for (var i = 0; i < 4 * count; i += 4) {
      view[i] = HEAP32[(value + 4 * i) >> 2];
      view[i + 1] = HEAP32[(value + (4 * i + 4)) >> 2];
      view[i + 2] = HEAP32[(value + (4 * i + 8)) >> 2];
      view[i + 3] = HEAP32[(value + (4 * i + 12)) >> 2];
    }
  } else {
    var view = HEAP32.subarray(value >> 2, (value + count * 16) >> 2);
  }
  GLctx.uniform4iv(GL.uniforms[location], view);
}
function _emscripten_glUniform4ui(location, v0, v1, v2, v3) {
  GLctx.uniform4ui(GL.uniforms[location], v0, v1, v2, v3);
}
function _emscripten_glUniform4uiv(location, count, value) {
  GLctx.uniform4uiv(GL.uniforms[location], HEAPU32, value >> 2, count * 4);
}
function _emscripten_glUniformBlockBinding(
  program,
  uniformBlockIndex,
  uniformBlockBinding
) {
  program = GL.programs[program];
  GLctx["uniformBlockBinding"](program, uniformBlockIndex, uniformBlockBinding);
}
function _emscripten_glUniformMatrix2fv(location, count, transpose, value) {
  if (GL.currentContext.version >= 2) {
    GLctx.uniformMatrix2fv(
      GL.uniforms[location],
      !!transpose,
      HEAPF32,
      value >> 2,
      count * 4
    );
    return;
  }
  if (4 * count <= GL.MINI_TEMP_BUFFER_SIZE) {
    var view = GL.miniTempBufferFloatViews[4 * count - 1];
    for (var i = 0; i < 4 * count; i += 4) {
      view[i] = HEAPF32[(value + 4 * i) >> 2];
      view[i + 1] = HEAPF32[(value + (4 * i + 4)) >> 2];
      view[i + 2] = HEAPF32[(value + (4 * i + 8)) >> 2];
      view[i + 3] = HEAPF32[(value + (4 * i + 12)) >> 2];
    }
  } else {
    var view = HEAPF32.subarray(value >> 2, (value + count * 16) >> 2);
  }
  GLctx.uniformMatrix2fv(GL.uniforms[location], !!transpose, view);
}
function _emscripten_glUniformMatrix2x3fv(location, count, transpose, value) {
  GLctx.uniformMatrix2x3fv(
    GL.uniforms[location],
    !!transpose,
    HEAPF32,
    value >> 2,
    count * 6
  );
}
function _emscripten_glUniformMatrix2x4fv(location, count, transpose, value) {
  GLctx.uniformMatrix2x4fv(
    GL.uniforms[location],
    !!transpose,
    HEAPF32,
    value >> 2,
    count * 8
  );
}
function _emscripten_glUniformMatrix3fv(location, count, transpose, value) {
  if (GL.currentContext.version >= 2) {
    GLctx.uniformMatrix3fv(
      GL.uniforms[location],
      !!transpose,
      HEAPF32,
      value >> 2,
      count * 9
    );
    return;
  }
  if (9 * count <= GL.MINI_TEMP_BUFFER_SIZE) {
    var view = GL.miniTempBufferFloatViews[9 * count - 1];
    for (var i = 0; i < 9 * count; i += 9) {
      view[i] = HEAPF32[(value + 4 * i) >> 2];
      view[i + 1] = HEAPF32[(value + (4 * i + 4)) >> 2];
      view[i + 2] = HEAPF32[(value + (4 * i + 8)) >> 2];
      view[i + 3] = HEAPF32[(value + (4 * i + 12)) >> 2];
      view[i + 4] = HEAPF32[(value + (4 * i + 16)) >> 2];
      view[i + 5] = HEAPF32[(value + (4 * i + 20)) >> 2];
      view[i + 6] = HEAPF32[(value + (4 * i + 24)) >> 2];
      view[i + 7] = HEAPF32[(value + (4 * i + 28)) >> 2];
      view[i + 8] = HEAPF32[(value + (4 * i + 32)) >> 2];
    }
  } else {
    var view = HEAPF32.subarray(value >> 2, (value + count * 36) >> 2);
  }
  GLctx.uniformMatrix3fv(GL.uniforms[location], !!transpose, view);
}
function _emscripten_glUniformMatrix3x2fv(location, count, transpose, value) {
  GLctx.uniformMatrix3x2fv(
    GL.uniforms[location],
    !!transpose,
    HEAPF32,
    value >> 2,
    count * 6
  );
}
function _emscripten_glUniformMatrix3x4fv(location, count, transpose, value) {
  GLctx.uniformMatrix3x4fv(
    GL.uniforms[location],
    !!transpose,
    HEAPF32,
    value >> 2,
    count * 12
  );
}
function _emscripten_glUniformMatrix4fv(location, count, transpose, value) {
  if (GL.currentContext.version >= 2) {
    GLctx.uniformMatrix4fv(
      GL.uniforms[location],
      !!transpose,
      HEAPF32,
      value >> 2,
      count * 16
    );
    return;
  }
  if (16 * count <= GL.MINI_TEMP_BUFFER_SIZE) {
    var view = GL.miniTempBufferFloatViews[16 * count - 1];
    for (var i = 0; i < 16 * count; i += 16) {
      view[i] = HEAPF32[(value + 4 * i) >> 2];
      view[i + 1] = HEAPF32[(value + (4 * i + 4)) >> 2];
      view[i + 2] = HEAPF32[(value + (4 * i + 8)) >> 2];
      view[i + 3] = HEAPF32[(value + (4 * i + 12)) >> 2];
      view[i + 4] = HEAPF32[(value + (4 * i + 16)) >> 2];
      view[i + 5] = HEAPF32[(value + (4 * i + 20)) >> 2];
      view[i + 6] = HEAPF32[(value + (4 * i + 24)) >> 2];
      view[i + 7] = HEAPF32[(value + (4 * i + 28)) >> 2];
      view[i + 8] = HEAPF32[(value + (4 * i + 32)) >> 2];
      view[i + 9] = HEAPF32[(value + (4 * i + 36)) >> 2];
      view[i + 10] = HEAPF32[(value + (4 * i + 40)) >> 2];
      view[i + 11] = HEAPF32[(value + (4 * i + 44)) >> 2];
      view[i + 12] = HEAPF32[(value + (4 * i + 48)) >> 2];
      view[i + 13] = HEAPF32[(value + (4 * i + 52)) >> 2];
      view[i + 14] = HEAPF32[(value + (4 * i + 56)) >> 2];
      view[i + 15] = HEAPF32[(value + (4 * i + 60)) >> 2];
    }
  } else {
    var view = HEAPF32.subarray(value >> 2, (value + count * 64) >> 2);
  }
  GLctx.uniformMatrix4fv(GL.uniforms[location], !!transpose, view);
}
function _emscripten_glUniformMatrix4x2fv(location, count, transpose, value) {
  GLctx.uniformMatrix4x2fv(
    GL.uniforms[location],
    !!transpose,
    HEAPF32,
    value >> 2,
    count * 8
  );
}
function _emscripten_glUniformMatrix4x3fv(location, count, transpose, value) {
  GLctx.uniformMatrix4x3fv(
    GL.uniforms[location],
    !!transpose,
    HEAPF32,
    value >> 2,
    count * 12
  );
}
function _emscripten_glUseProgram(program) {
  GLctx.useProgram(GL.programs[program]);
}
function _emscripten_glValidateProgram(program) {
  GLctx.validateProgram(GL.programs[program]);
}
function _emscripten_glVertexAttrib1f(x0, x1) {
  GLctx["vertexAttrib1f"](x0, x1);
}
function _emscripten_glVertexAttrib1fv(index, v) {
  GLctx.vertexAttrib1f(index, HEAPF32[v >> 2]);
}
function _emscripten_glVertexAttrib2f(x0, x1, x2) {
  GLctx["vertexAttrib2f"](x0, x1, x2);
}
function _emscripten_glVertexAttrib2fv(index, v) {
  GLctx.vertexAttrib2f(index, HEAPF32[v >> 2], HEAPF32[(v + 4) >> 2]);
}
function _emscripten_glVertexAttrib3f(x0, x1, x2, x3) {
  GLctx["vertexAttrib3f"](x0, x1, x2, x3);
}
function _emscripten_glVertexAttrib3fv(index, v) {
  GLctx.vertexAttrib3f(
    index,
    HEAPF32[v >> 2],
    HEAPF32[(v + 4) >> 2],
    HEAPF32[(v + 8) >> 2]
  );
}
function _emscripten_glVertexAttrib4f(x0, x1, x2, x3, x4) {
  GLctx["vertexAttrib4f"](x0, x1, x2, x3, x4);
}
function _emscripten_glVertexAttrib4fv(index, v) {
  GLctx.vertexAttrib4f(
    index,
    HEAPF32[v >> 2],
    HEAPF32[(v + 4) >> 2],
    HEAPF32[(v + 8) >> 2],
    HEAPF32[(v + 12) >> 2]
  );
}
function _emscripten_glVertexAttribDivisor(index, divisor) {
  GLctx["vertexAttribDivisor"](index, divisor);
}
function _emscripten_glVertexAttribDivisorANGLE(index, divisor) {
  GLctx["vertexAttribDivisor"](index, divisor);
}
function _emscripten_glVertexAttribDivisorARB(index, divisor) {
  GLctx["vertexAttribDivisor"](index, divisor);
}
function _emscripten_glVertexAttribDivisorEXT(index, divisor) {
  GLctx["vertexAttribDivisor"](index, divisor);
}
function _emscripten_glVertexAttribDivisorNV(index, divisor) {
  GLctx["vertexAttribDivisor"](index, divisor);
}
function _emscripten_glVertexAttribI4i(x0, x1, x2, x3, x4) {
  GLctx["vertexAttribI4i"](x0, x1, x2, x3, x4);
}
function _emscripten_glVertexAttribI4iv(index, v) {
  GLctx.vertexAttribI4i(
    index,
    HEAP32[v >> 2],
    HEAP32[(v + 4) >> 2],
    HEAP32[(v + 8) >> 2],
    HEAP32[(v + 12) >> 2]
  );
}
function _emscripten_glVertexAttribI4ui(x0, x1, x2, x3, x4) {
  GLctx["vertexAttribI4ui"](x0, x1, x2, x3, x4);
}
function _emscripten_glVertexAttribI4uiv(index, v) {
  GLctx.vertexAttribI4ui(
    index,
    HEAPU32[v >> 2],
    HEAPU32[(v + 4) >> 2],
    HEAPU32[(v + 8) >> 2],
    HEAPU32[(v + 12) >> 2]
  );
}
function _emscripten_glVertexAttribIPointer(index, size, type, stride, ptr) {
  GLctx["vertexAttribIPointer"](index, size, type, stride, ptr);
}
function _emscripten_glVertexAttribPointer(
  index,
  size,
  type,
  normalized,
  stride,
  ptr
) {
  GLctx.vertexAttribPointer(index, size, type, !!normalized, stride, ptr);
}
function _emscripten_glViewport(x0, x1, x2, x3) {
  GLctx["viewport"](x0, x1, x2, x3);
}
function _emscripten_glWaitSync(sync, flags, timeoutLo, timeoutHi) {
  GLctx.waitSync(
    GL.syncs[sync],
    flags,
    convertI32PairToI53(timeoutLo, timeoutHi)
  );
}
function _emscripten_is_main_browser_thread() {
  return typeof importScripts === "undefined";
}
function __reallyNegative(x) {
  return x < 0 || (x === 0 && 1 / x === -Infinity);
}
function convertU32PairToI53(lo, hi) {
  return (lo >>> 0) + (hi >>> 0) * 4294967296;
}
function intArrayFromString(stringy, dontAddNull, length) {
  var len = length > 0 ? length : lengthBytesUTF8(stringy) + 1;
  var u8array = new Array(len);
  var numBytesWritten = stringToUTF8Array(stringy, u8array, 0, u8array.length);
  if (dontAddNull) u8array.length = numBytesWritten;
  return u8array;
}
function __formatString(format, varargs) {
  var textIndex = format;
  var argIndex = varargs;
  function prepVararg(ptr, type) {
    if (type === "double" || type === "i64") {
      if (ptr & 7) {
        ptr += 4;
      }
    } else {
    }
    return ptr;
  }
  function getNextArg(type) {
    var ret;
    argIndex = prepVararg(argIndex, type);
    if (type === "double") {
      ret = HEAPF64[argIndex >> 3];
      argIndex += 8;
    } else if (type == "i64") {
      ret = [HEAP32[argIndex >> 2], HEAP32[(argIndex + 4) >> 2]];
      argIndex += 8;
    } else {
      type = "i32";
      ret = HEAP32[argIndex >> 2];
      argIndex += 4;
    }
    return ret;
  }
  var ret = [];
  var curr, next, currArg;
  while (1) {
    var startTextIndex = textIndex;
    curr = HEAP8[textIndex >> 0];
    if (curr === 0) break;
    next = HEAP8[(textIndex + 1) >> 0];
    if (curr == 37) {
      var flagAlwaysSigned = false;
      var flagLeftAlign = false;
      var flagAlternative = false;
      var flagZeroPad = false;
      var flagPadSign = false;
      flagsLoop: while (1) {
        switch (next) {
          case 43:
            flagAlwaysSigned = true;
            break;
          case 45:
            flagLeftAlign = true;
            break;
          case 35:
            flagAlternative = true;
            break;
          case 48:
            if (flagZeroPad) {
              break flagsLoop;
            } else {
              flagZeroPad = true;
              break;
            }
          case 32:
            flagPadSign = true;
            break;
          default:
            break flagsLoop;
        }
        textIndex++;
        next = HEAP8[(textIndex + 1) >> 0];
      }
      var width = 0;
      if (next == 42) {
        width = getNextArg("i32");
        textIndex++;
        next = HEAP8[(textIndex + 1) >> 0];
      } else {
        while (next >= 48 && next <= 57) {
          width = width * 10 + (next - 48);
          textIndex++;
          next = HEAP8[(textIndex + 1) >> 0];
        }
      }
      var precisionSet = false,
        precision = -1;
      if (next == 46) {
        precision = 0;
        precisionSet = true;
        textIndex++;
        next = HEAP8[(textIndex + 1) >> 0];
        if (next == 42) {
          precision = getNextArg("i32");
          textIndex++;
        } else {
          while (1) {
            var precisionChr = HEAP8[(textIndex + 1) >> 0];
            if (precisionChr < 48 || precisionChr > 57) break;
            precision = precision * 10 + (precisionChr - 48);
            textIndex++;
          }
        }
        next = HEAP8[(textIndex + 1) >> 0];
      }
      if (precision < 0) {
        precision = 6;
        precisionSet = false;
      }
      var argSize;
      switch (String.fromCharCode(next)) {
        case "h":
          var nextNext = HEAP8[(textIndex + 2) >> 0];
          if (nextNext == 104) {
            textIndex++;
            argSize = 1;
          } else {
            argSize = 2;
          }
          break;
        case "l":
          var nextNext = HEAP8[(textIndex + 2) >> 0];
          if (nextNext == 108) {
            textIndex++;
            argSize = 8;
          } else {
            argSize = 4;
          }
          break;
        case "L":
        case "q":
        case "j":
          argSize = 8;
          break;
        case "z":
        case "t":
        case "I":
          argSize = 4;
          break;
        default:
          argSize = null;
      }
      if (argSize) textIndex++;
      next = HEAP8[(textIndex + 1) >> 0];
      switch (String.fromCharCode(next)) {
        case "d":
        case "i":
        case "u":
        case "o":
        case "x":
        case "X":
        case "p": {
          var signed = next == 100 || next == 105;
          argSize = argSize || 4;
          currArg = getNextArg("i" + argSize * 8);
          var argText;
          if (argSize == 8) {
            currArg =
              next == 117
                ? convertU32PairToI53(currArg[0], currArg[1])
                : convertI32PairToI53(currArg[0], currArg[1]);
          }
          if (argSize <= 4) {
            var limit = Math.pow(256, argSize) - 1;
            currArg = (signed ? reSign : unSign)(currArg & limit, argSize * 8);
          }
          var currAbsArg = Math.abs(currArg);
          var prefix = "";
          if (next == 100 || next == 105) {
            argText = reSign(currArg, 8 * argSize, 1).toString(10);
          } else if (next == 117) {
            argText = unSign(currArg, 8 * argSize, 1).toString(10);
            currArg = Math.abs(currArg);
          } else if (next == 111) {
            argText = (flagAlternative ? "0" : "") + currAbsArg.toString(8);
          } else if (next == 120 || next == 88) {
            prefix = flagAlternative && currArg != 0 ? "0x" : "";
            if (currArg < 0) {
              currArg = -currArg;
              argText = (currAbsArg - 1).toString(16);
              var buffer = [];
              for (var i = 0; i < argText.length; i++) {
                buffer.push((15 - parseInt(argText[i], 16)).toString(16));
              }
              argText = buffer.join("");
              while (argText.length < argSize * 2) argText = "f" + argText;
            } else {
              argText = currAbsArg.toString(16);
            }
            if (next == 88) {
              prefix = prefix.toUpperCase();
              argText = argText.toUpperCase();
            }
          } else if (next == 112) {
            if (currAbsArg === 0) {
              argText = "(nil)";
            } else {
              prefix = "0x";
              argText = currAbsArg.toString(16);
            }
          }
          if (precisionSet) {
            while (argText.length < precision) {
              argText = "0" + argText;
            }
          }
          if (currArg >= 0) {
            if (flagAlwaysSigned) {
              prefix = "+" + prefix;
            } else if (flagPadSign) {
              prefix = " " + prefix;
            }
          }
          if (argText.charAt(0) == "-") {
            prefix = "-" + prefix;
            argText = argText.substr(1);
          }
          while (prefix.length + argText.length < width) {
            if (flagLeftAlign) {
              argText += " ";
            } else {
              if (flagZeroPad) {
                argText = "0" + argText;
              } else {
                prefix = " " + prefix;
              }
            }
          }
          argText = prefix + argText;
          argText.split("").forEach(function (chr) {
            ret.push(chr.charCodeAt(0));
          });
          break;
        }
        case "f":
        case "F":
        case "e":
        case "E":
        case "g":
        case "G": {
          currArg = getNextArg("double");
          var argText;
          if (isNaN(currArg)) {
            argText = "nan";
            flagZeroPad = false;
          } else if (!isFinite(currArg)) {
            argText = (currArg < 0 ? "-" : "") + "inf";
            flagZeroPad = false;
          } else {
            var isGeneral = false;
            var effectivePrecision = Math.min(precision, 20);
            if (next == 103 || next == 71) {
              isGeneral = true;
              precision = precision || 1;
              var exponent = parseInt(
                currArg.toExponential(effectivePrecision).split("e")[1],
                10
              );
              if (precision > exponent && exponent >= -4) {
                next = (next == 103 ? "f" : "F").charCodeAt(0);
                precision -= exponent + 1;
              } else {
                next = (next == 103 ? "e" : "E").charCodeAt(0);
                precision--;
              }
              effectivePrecision = Math.min(precision, 20);
            }
            if (next == 101 || next == 69) {
              argText = currArg.toExponential(effectivePrecision);
              if (/[eE][-+]\d$/.test(argText)) {
                argText = argText.slice(0, -1) + "0" + argText.slice(-1);
              }
            } else if (next == 102 || next == 70) {
              argText = currArg.toFixed(effectivePrecision);
              if (currArg === 0 && __reallyNegative(currArg)) {
                argText = "-" + argText;
              }
            }
            var parts = argText.split("e");
            if (isGeneral && !flagAlternative) {
              while (
                parts[0].length > 1 &&
                parts[0].indexOf(".") != -1 &&
                (parts[0].slice(-1) == "0" || parts[0].slice(-1) == ".")
              ) {
                parts[0] = parts[0].slice(0, -1);
              }
            } else {
              if (flagAlternative && argText.indexOf(".") == -1)
                parts[0] += ".";
              while (precision > effectivePrecision++) parts[0] += "0";
            }
            argText = parts[0] + (parts.length > 1 ? "e" + parts[1] : "");
            if (next == 69) argText = argText.toUpperCase();
            if (currArg >= 0) {
              if (flagAlwaysSigned) {
                argText = "+" + argText;
              } else if (flagPadSign) {
                argText = " " + argText;
              }
            }
          }
          while (argText.length < width) {
            if (flagLeftAlign) {
              argText += " ";
            } else {
              if (flagZeroPad && (argText[0] == "-" || argText[0] == "+")) {
                argText = argText[0] + "0" + argText.slice(1);
              } else {
                argText = (flagZeroPad ? "0" : " ") + argText;
              }
            }
          }
          if (next < 97) argText = argText.toUpperCase();
          argText.split("").forEach(function (chr) {
            ret.push(chr.charCodeAt(0));
          });
          break;
        }
        case "s": {
          var arg = getNextArg("i8*");
          var argLength = arg ? _strlen(arg) : "(null)".length;
          if (precisionSet) argLength = Math.min(argLength, precision);
          if (!flagLeftAlign) {
            while (argLength < width--) {
              ret.push(32);
            }
          }
          if (arg) {
            for (var i = 0; i < argLength; i++) {
              ret.push(HEAPU8[arg++ >> 0]);
            }
          } else {
            ret = ret.concat(
              intArrayFromString("(null)".substr(0, argLength), true)
            );
          }
          if (flagLeftAlign) {
            while (argLength < width--) {
              ret.push(32);
            }
          }
          break;
        }
        case "c": {
          if (flagLeftAlign) ret.push(getNextArg("i8"));
          while (--width > 0) {
            ret.push(32);
          }
          if (!flagLeftAlign) ret.push(getNextArg("i8"));
          break;
        }
        case "n": {
          var ptr = getNextArg("i32*");
          HEAP32[ptr >> 2] = ret.length;
          break;
        }
        case "%": {
          ret.push(curr);
          break;
        }
        default: {
          for (var i = startTextIndex; i < textIndex + 2; i++) {
            ret.push(HEAP8[i >> 0]);
          }
        }
      }
      textIndex += 2;
    } else {
      ret.push(curr);
      textIndex += 1;
    }
  }
  return ret;
}
function __emscripten_traverse_stack(args) {
  if (!args || !args.callee || !args.callee.name) {
    return [null, "", ""];
  }
  var funstr = args.callee.toString();
  var funcname = args.callee.name;
  var str = "(";
  var first = true;
  for (var i in args) {
    var a = args[i];
    if (!first) {
      str += ", ";
    }
    first = false;
    if (typeof a === "number" || typeof a === "string") {
      str += a;
    } else {
      str += "(" + typeof a + ")";
    }
  }
  str += ")";
  var caller = args.callee.caller;
  args = caller ? caller.arguments : [];
  if (first) str = "";
  return [args, funcname, str];
}
function jsStackTrace() {
  var err = new Error();
  if (!err.stack) {
    try {
      throw new Error();
    } catch (e) {
      err = e;
    }
    if (!err.stack) {
      return "(no stack trace available)";
    }
  }
  return err.stack.toString();
}
function demangle(func) {
  return func;
}
function warnOnce(text) {
  if (!warnOnce.shown) warnOnce.shown = {};
  if (!warnOnce.shown[text]) {
    warnOnce.shown[text] = 1;
    err(text);
  }
}
function _emscripten_get_callstack_js(flags) {
  var callstack = jsStackTrace();
  var iThisFunc = callstack.lastIndexOf("_emscripten_log");
  var iThisFunc2 = callstack.lastIndexOf("_emscripten_get_callstack");
  var iNextLine = callstack.indexOf("\n", Math.max(iThisFunc, iThisFunc2)) + 1;
  callstack = callstack.slice(iNextLine);
  if (flags & 8 && typeof emscripten_source_map === "undefined") {
    warnOnce(
      'Source map information is not available, emscripten_log with EM_LOG_C_STACK will be ignored. Build with "--pre-js $EMSCRIPTEN/src/emscripten-source-map.min.js" linker flag to add source map loading to code.'
    );
    flags ^= 8;
    flags |= 16;
  }
  var stack_args = null;
  if (flags & 128) {
    stack_args = __emscripten_traverse_stack(arguments);
    while (stack_args[1].indexOf("_emscripten_") >= 0)
      stack_args = __emscripten_traverse_stack(stack_args[0]);
  }
  var lines = callstack.split("\n");
  callstack = "";
  var newFirefoxRe = new RegExp("\\s*(.*?)@(.*?):([0-9]+):([0-9]+)");
  var firefoxRe = new RegExp("\\s*(.*?)@(.*):(.*)(:(.*))?");
  var chromeRe = new RegExp("\\s*at (.*?) \\((.*):(.*):(.*)\\)");
  for (var l in lines) {
    var line = lines[l];
    var jsSymbolName = "";
    var file = "";
    var lineno = 0;
    var column = 0;
    var parts = chromeRe.exec(line);
    if (parts && parts.length == 5) {
      jsSymbolName = parts[1];
      file = parts[2];
      lineno = parts[3];
      column = parts[4];
    } else {
      parts = newFirefoxRe.exec(line);
      if (!parts) parts = firefoxRe.exec(line);
      if (parts && parts.length >= 4) {
        jsSymbolName = parts[1];
        file = parts[2];
        lineno = parts[3];
        column = parts[4] | 0;
      } else {
        callstack += line + "\n";
        continue;
      }
    }
    var cSymbolName = flags & 32 ? demangle(jsSymbolName) : jsSymbolName;
    if (!cSymbolName) {
      cSymbolName = jsSymbolName;
    }
    var haveSourceMap = false;
    if (flags & 8) {
      var orig = emscripten_source_map.originalPositionFor({
        line: lineno,
        column: column,
      });
      haveSourceMap = orig && orig.source;
      if (haveSourceMap) {
        if (flags & 64) {
          orig.source = orig.source.substring(
            orig.source.replace(/\\/g, "/").lastIndexOf("/") + 1
          );
        }
        callstack +=
          "    at " +
          cSymbolName +
          " (" +
          orig.source +
          ":" +
          orig.line +
          ":" +
          orig.column +
          ")\n";
      }
    }
    if (flags & 16 || !haveSourceMap) {
      if (flags & 64) {
        file = file.substring(file.replace(/\\/g, "/").lastIndexOf("/") + 1);
      }
      callstack +=
        (haveSourceMap ? "     = " + jsSymbolName : "    at " + cSymbolName) +
        " (" +
        file +
        ":" +
        lineno +
        ":" +
        column +
        ")\n";
    }
    if (flags & 128 && stack_args[0]) {
      if (stack_args[1] == jsSymbolName && stack_args[2].length > 0) {
        callstack = callstack.replace(/\s+$/, "");
        callstack += " with values: " + stack_args[1] + stack_args[2] + "\n";
      }
      stack_args = __emscripten_traverse_stack(stack_args[0]);
    }
  }
  callstack = callstack.replace(/\s+$/, "");
  return callstack;
}
function _emscripten_log_js(flags, str) {
  if (flags & 24) {
    str = str.replace(/\s+$/, "");
    str += (str.length > 0 ? "\n" : "") + _emscripten_get_callstack_js(flags);
  }
  if (flags & 1) {
    if (flags & 4) {
      console.error(str);
    } else if (flags & 2) {
      console.warn(str);
    } else {
      console.log(str);
    }
  } else if (flags & 6) {
    err(str);
  } else {
    out(str);
  }
}
function _emscripten_log(flags, varargs) {
  var format = HEAP32[varargs >> 2];
  varargs += 4;
  var str = "";
  if (format) {
    var result = __formatString(format, varargs);
    for (var i = 0; i < result.length; ++i) {
      str += String.fromCharCode(result[i]);
    }
  }
  _emscripten_log_js(flags, str);
}
function _emscripten_memcpy_big(dest, src, num) {
  HEAPU8.copyWithin(dest, src, src + num);
}
function _emscripten_performance_now() {
  return performance.now();
}
function _emscripten_request_animation_frame_loop(cb, userData) {
  function tick(timeStamp) {
    if (dynCall_idi(cb, timeStamp, userData)) {
      requestAnimationFrame(tick);
    }
  }
  return requestAnimationFrame(tick);
}
function abortOnCannotGrowMemory(requestedSize) {
  abort("OOM");
}
function _emscripten_resize_heap(requestedSize) {
  abortOnCannotGrowMemory(requestedSize);
}
var JSEvents = {
  keyEvent: 0,
  mouseEvent: 0,
  wheelEvent: 0,
  uiEvent: 0,
  focusEvent: 0,
  deviceOrientationEvent: 0,
  deviceMotionEvent: 0,
  fullscreenChangeEvent: 0,
  pointerlockChangeEvent: 0,
  visibilityChangeEvent: 0,
  touchEvent: 0,
  previousFullscreenElement: null,
  previousScreenX: null,
  previousScreenY: null,
  removeEventListenersRegistered: false,
  removeAllEventListeners: function () {
    for (var i = JSEvents.eventHandlers.length - 1; i >= 0; --i) {
      JSEvents._removeHandler(i);
    }
    JSEvents.eventHandlers = [];
    JSEvents.deferredCalls = [];
  },
  deferredCalls: [],
  deferCall: function (targetFunction, precedence, argsList) {
    function arraysHaveEqualContent(arrA, arrB) {
      if (arrA.length != arrB.length) return false;
      for (var i in arrA) {
        if (arrA[i] != arrB[i]) return false;
      }
      return true;
    }
    for (var i in JSEvents.deferredCalls) {
      var call = JSEvents.deferredCalls[i];
      if (
        call.targetFunction == targetFunction &&
        arraysHaveEqualContent(call.argsList, argsList)
      ) {
        return;
      }
    }
    JSEvents.deferredCalls.push({
      targetFunction: targetFunction,
      precedence: precedence,
      argsList: argsList,
    });
    JSEvents.deferredCalls.sort(function (x, y) {
      return x.precedence < y.precedence;
    });
  },
  removeDeferredCalls: function (targetFunction) {
    for (var i = 0; i < JSEvents.deferredCalls.length; ++i) {
      if (JSEvents.deferredCalls[i].targetFunction == targetFunction) {
        JSEvents.deferredCalls.splice(i, 1);
        --i;
      }
    }
  },
  canPerformEventHandlerRequests: function () {
    return (
      JSEvents.inEventHandler &&
      JSEvents.currentEventHandler.allowsDeferredCalls
    );
  },
  runDeferredCalls: function () {
    if (!JSEvents.canPerformEventHandlerRequests()) {
      return;
    }
    for (var i = 0; i < JSEvents.deferredCalls.length; ++i) {
      var call = JSEvents.deferredCalls[i];
      JSEvents.deferredCalls.splice(i, 1);
      --i;
      call.targetFunction.apply(null, call.argsList);
    }
  },
  inEventHandler: 0,
  currentEventHandler: null,
  eventHandlers: [],
  removeAllHandlersOnTarget: function (target, eventTypeString) {
    for (var i = 0; i < JSEvents.eventHandlers.length; ++i) {
      if (
        JSEvents.eventHandlers[i].target == target &&
        (!eventTypeString ||
          eventTypeString == JSEvents.eventHandlers[i].eventTypeString)
      ) {
        JSEvents._removeHandler(i--);
      }
    }
  },
  _removeHandler: function (i) {
    var h = JSEvents.eventHandlers[i];
    h.target.removeEventListener(
      h.eventTypeString,
      h.eventListenerFunc,
      h.useCapture
    );
    JSEvents.eventHandlers.splice(i, 1);
  },
  registerOrRemoveHandler: function (eventHandler) {
    var jsEventHandler = function jsEventHandler(event) {
      ++JSEvents.inEventHandler;
      JSEvents.currentEventHandler = eventHandler;
      JSEvents.runDeferredCalls();
      eventHandler.handlerFunc(event);
      JSEvents.runDeferredCalls();
      --JSEvents.inEventHandler;
    };
    if (eventHandler.callbackfunc) {
      eventHandler.eventListenerFunc = jsEventHandler;
      eventHandler.target.addEventListener(
        eventHandler.eventTypeString,
        jsEventHandler,
        eventHandler.useCapture
      );
      JSEvents.eventHandlers.push(eventHandler);
    } else {
      for (var i = 0; i < JSEvents.eventHandlers.length; ++i) {
        if (
          JSEvents.eventHandlers[i].target == eventHandler.target &&
          JSEvents.eventHandlers[i].eventTypeString ==
            eventHandler.eventTypeString
        ) {
          JSEvents._removeHandler(i--);
        }
      }
    }
  },
  getNodeNameForTarget: function (target) {
    if (!target) return "";
    if (target == window) return "#window";
    if (target == screen) return "#screen";
    return target && target.nodeName ? target.nodeName : "";
  },
  fullscreenEnabled: function () {
    return document.fullscreenEnabled || document.webkitFullscreenEnabled;
  },
};
function __maybeCStringToJsString(cString) {
  return cString === cString + 0 ? UTF8ToString(cString) : cString;
}
var __specialEventTargets = [0, document, window];
function __findEventTarget(target) {
  var domElement =
    __specialEventTargets[target] ||
    document.querySelector(__maybeCStringToJsString(target));
  return domElement;
}
function __findCanvasEventTarget(target) {
  return __findEventTarget(target);
}
function _emscripten_set_canvas_element_size(target, width, height) {
  var canvas = __findCanvasEventTarget(target);
  if (!canvas) return -4;
  canvas.width = width;
  canvas.height = height;
  return 0;
}
var Fetch = {
  xhrs: [],
  setu64: function (addr, val) {
    HEAPU32[addr >> 2] = val;
    HEAPU32[(addr + 4) >> 2] = (val / 4294967296) | 0;
  },
  staticInit: function () {},
};
function __emscripten_fetch_xhr(
  fetch,
  onsuccess,
  onerror,
  onprogress,
  onreadystatechange
) {
  var url = HEAPU32[(fetch + 8) >> 2];
  if (!url) {
    onerror(fetch, 0, "no url specified!");
    return;
  }
  // var url_ = 'https://stream.weixin.qq.com/wegameenginetest/outdoor/download_unzip/72/TinyRacing/' + UTF8ToString(url);
  var url_="http://10.86.98.70/wechat_tiny/tracing/"+ UTF8ToString(url);
  var fetch_attr = fetch + 112;
  var requestMethod = UTF8ToString(fetch_attr);
  if (!requestMethod) requestMethod = "GET";
  var userData = HEAPU32[(fetch_attr + 32) >> 2];
  var fetchAttributes = HEAPU32[(fetch_attr + 52) >> 2];
  var timeoutMsecs = HEAPU32[(fetch_attr + 56) >> 2];
  var withCredentials = !!HEAPU32[(fetch_attr + 60) >> 2];
  var destinationPath = HEAPU32[(fetch_attr + 64) >> 2];
  var userName = HEAPU32[(fetch_attr + 68) >> 2];
  var password = HEAPU32[(fetch_attr + 72) >> 2];
  var requestHeaders = HEAPU32[(fetch_attr + 76) >> 2];
  var overriddenMimeType = HEAPU32[(fetch_attr + 80) >> 2];
  var dataPtr = HEAPU32[(fetch_attr + 84) >> 2];
  var dataLength = HEAPU32[(fetch_attr + 88) >> 2];
  var fetchAttrLoadToMemory = !!(fetchAttributes & 1);
  var fetchAttrStreamData = !!(fetchAttributes & 2);
  var fetchAttrAppend = !!(fetchAttributes & 8);
  var fetchAttrReplace = !!(fetchAttributes & 16);
  var fetchAttrSynchronous = !!(fetchAttributes & 64);
  var fetchAttrWaitable = !!(fetchAttributes & 128);
  var userNameStr = userName ? UTF8ToString(userName) : undefined;
  var passwordStr = password ? UTF8ToString(password) : undefined;
  var overriddenMimeTypeStr = overriddenMimeType
    ? UTF8ToString(overriddenMimeType)
    : undefined;
  var xhr = new XMLHttpRequest();
  xhr.withCredentials = withCredentials;
  xhr.open(
    requestMethod,
    url_,
    !fetchAttrSynchronous,
    userNameStr,
    passwordStr
  );
  if (!fetchAttrSynchronous) xhr.timeout = timeoutMsecs;
  xhr.url_ = url_;
  xhr.responseType = "arraybuffer";
  if (overriddenMimeType) {
    xhr.overrideMimeType(overriddenMimeTypeStr);
  }
  if (requestHeaders) {
    for (;;) {
      var key = HEAPU32[requestHeaders >> 2];
      if (!key) break;
      var value = HEAPU32[(requestHeaders + 4) >> 2];
      if (!value) break;
      requestHeaders += 8;
      var keyStr = UTF8ToString(key);
      var valueStr = UTF8ToString(value);
      xhr.setRequestHeader(keyStr, valueStr);
    }
  }
  Fetch.xhrs.push(xhr);
  var id = Fetch.xhrs.length;
  HEAPU32[(fetch + 0) >> 2] = id;
  var data =
    dataPtr && dataLength ? HEAPU8.slice(dataPtr, dataPtr + dataLength) : null;
  xhr.onload = function (e) {
    var len = xhr.response ? xhr.response.byteLength : 0;
    var ptr = 0;
    var ptrLen = 0;
    if (fetchAttrLoadToMemory && !fetchAttrStreamData) {
      ptrLen = len;
      ptr = _malloc(ptrLen);
      HEAPU8.set(new Uint8Array(xhr.response), ptr);
    }
    HEAPU32[(fetch + 12) >> 2] = ptr;
    Fetch.setu64(fetch + 16, ptrLen);
    Fetch.setu64(fetch + 24, 0);
    if (len) {
      Fetch.setu64(fetch + 32, len);
    }
    HEAPU16[(fetch + 40) >> 1] = xhr.readyState;
    if (xhr.readyState === 4 && xhr.status === 0) {
      if (len > 0) xhr.status = 200;
      else xhr.status = 404;
    }
    HEAPU16[(fetch + 42) >> 1] = xhr.status;
    if (xhr.statusText) stringToUTF8(xhr.statusText, fetch + 44, 64);
    if (xhr.status >= 200 && xhr.status < 300) {
      if (onsuccess) onsuccess(fetch, xhr, e);
    } else {
      if (onerror) onerror(fetch, xhr, e);
    }
  };
  xhr.onerror = function (e) {
    var status = xhr.status;
    if (xhr.readyState === 4 && status === 0) status = 404;
    HEAPU32[(fetch + 12) >> 2] = 0;
    Fetch.setu64(fetch + 16, 0);
    Fetch.setu64(fetch + 24, 0);
    Fetch.setu64(fetch + 32, 0);
    HEAPU16[(fetch + 40) >> 1] = xhr.readyState;
    HEAPU16[(fetch + 42) >> 1] = status;
    if (onerror) onerror(fetch, xhr, e);
  };
  xhr.ontimeout = function (e) {
    if (onerror) onerror(fetch, xhr, e);
  };
  xhr.onprogress = function (e) {
    var ptrLen =
      fetchAttrLoadToMemory && fetchAttrStreamData && xhr.response
        ? xhr.response.byteLength
        : 0;
    var ptr = 0;
    if (fetchAttrLoadToMemory && fetchAttrStreamData) {
      ptr = _malloc(ptrLen);
      HEAPU8.set(new Uint8Array(xhr.response), ptr);
    }
    HEAPU32[(fetch + 12) >> 2] = ptr;
    Fetch.setu64(fetch + 16, ptrLen);
    Fetch.setu64(fetch + 24, e.loaded - ptrLen);
    Fetch.setu64(fetch + 32, e.total);
    HEAPU16[(fetch + 40) >> 1] = xhr.readyState;
    if (xhr.readyState >= 3 && xhr.status === 0 && e.loaded > 0)
      xhr.status = 200;
    HEAPU16[(fetch + 42) >> 1] = xhr.status;
    if (xhr.statusText) stringToUTF8(xhr.statusText, fetch + 44, 64);
    if (onprogress) onprogress(fetch, xhr, e);
  };
  xhr.onreadystatechange = function (e) {
    HEAPU16[(fetch + 40) >> 1] = xhr.readyState;
    if (xhr.readyState >= 2) {
      HEAPU16[(fetch + 42) >> 1] = xhr.status;
    }
    if (onreadystatechange) onreadystatechange(fetch, xhr, e);
  };
  try {
    xhr.send(data);
  } catch (e) {
    if (onerror) onerror(fetch, xhr, e);
  }
}
function _emscripten_start_fetch(
  fetch,
  successcb,
  errorcb,
  progresscb,
  readystatechangecb
) {
  if (typeof noExitRuntime !== "undefined") noExitRuntime = true;
  var fetch_attr = fetch + 112;
  var requestMethod = UTF8ToString(fetch_attr);
  var onsuccess = HEAPU32[(fetch_attr + 36) >> 2];
  var onerror = HEAPU32[(fetch_attr + 40) >> 2];
  var onprogress = HEAPU32[(fetch_attr + 44) >> 2];
  var onreadystatechange = HEAPU32[(fetch_attr + 48) >> 2];
  var fetchAttributes = HEAPU32[(fetch_attr + 52) >> 2];
  var fetchAttrLoadToMemory = !!(fetchAttributes & 1);
  var fetchAttrStreamData = !!(fetchAttributes & 2);
  var fetchAttrAppend = !!(fetchAttributes & 8);
  var fetchAttrReplace = !!(fetchAttributes & 16);
  var reportSuccess = function (fetch, xhr, e) {
    if (onsuccess) dynCall_vi(onsuccess, fetch);
    else if (successcb) successcb(fetch);
  };
  var reportProgress = function (fetch, xhr, e) {
    if (onprogress) dynCall_vi(onprogress, fetch);
    else if (progresscb) progresscb(fetch);
  };
  var reportError = function (fetch, xhr, e) {
    if (onerror) dynCall_vi(onerror, fetch);
    else if (errorcb) errorcb(fetch);
  };
  var reportReadyStateChange = function (fetch, xhr, e) {
    if (onreadystatechange) dynCall_vi(onreadystatechange, fetch);
    else if (readystatechangecb) readystatechangecb(fetch);
  };
  __emscripten_fetch_xhr(
    fetch,
    reportSuccess,
    reportError,
    reportProgress,
    reportReadyStateChange
  );
  return fetch;
}
function _emscripten_throw_string(str) {
  //throw UTF8ToString(str);
}
var __emscripten_webgl_power_preferences = [
  "default",
  "low-power",
  "high-performance",
];
function _emscripten_webgl_do_create_context(target, attributes) {
  var contextAttributes = {};
  var a = attributes >> 2;
  contextAttributes["alpha"] = !!HEAP32[a + (0 >> 2)];
  contextAttributes["depth"] = !!HEAP32[a + (4 >> 2)];
  contextAttributes["stencil"] = !!HEAP32[a + (8 >> 2)];
  contextAttributes["antialias"] = !!HEAP32[a + (12 >> 2)];
  contextAttributes["premultipliedAlpha"] = !!HEAP32[a + (16 >> 2)];
  contextAttributes["preserveDrawingBuffer"] = !!HEAP32[a + (20 >> 2)];
  var powerPreference = HEAP32[a + (24 >> 2)];
  contextAttributes["powerPreference"] =
    __emscripten_webgl_power_preferences[powerPreference];
  contextAttributes["failIfMajorPerformanceCaveat"] = !!HEAP32[a + (28 >> 2)];
  contextAttributes.majorVersion = HEAP32[a + (32 >> 2)];
  contextAttributes.minorVersion = HEAP32[a + (36 >> 2)];
  contextAttributes.enableExtensionsByDefault = HEAP32[a + (40 >> 2)];
  contextAttributes.explicitSwapControl = HEAP32[a + (44 >> 2)];
  contextAttributes.proxyContextToMainThread = HEAP32[a + (48 >> 2)];
  contextAttributes.renderViaOffscreenBackBuffer = HEAP32[a + (52 >> 2)];
  var canvas = __findCanvasEventTarget(target);
  if (!canvas) {
    return 0;
  }
  if (contextAttributes.explicitSwapControl) {
    return 0;
  }
  var contextHandle = GL.createContext(canvas, contextAttributes);
  return contextHandle;
}
function _emscripten_webgl_create_context(a0, a1) {
  return _emscripten_webgl_do_create_context(a0, a1);
}
function _emscripten_webgl_destroy_context_calling_thread(contextHandle) {
  if (GL.currentContext == contextHandle) GL.currentContext = 0;
  GL.deleteContext(contextHandle);
}
function _emscripten_webgl_destroy_context(a0) {
  return _emscripten_webgl_destroy_context_calling_thread(a0);
}
function _emscripten_webgl_enable_extension_calling_thread(
  contextHandle,
  extension
) {
  var context = GL.getContext(contextHandle);
  var extString = UTF8ToString(extension);
  if (extString.indexOf("GL_") == 0) extString = extString.substr(3);
  if (extString == "ANGLE_instanced_arrays")
    __webgl_acquireInstancedArraysExtension(GLctx);
  else if (extString == "OES_vertex_array_object")
    __webgl_acquireVertexArrayObjectExtension(GLctx);
  else if (extString == "WEBGL_draw_buffers")
    __webgl_acquireDrawBuffersExtension(GLctx);
  var ext = context.GLctx.getExtension(extString);
  return !!ext;
}
function _emscripten_webgl_enable_extension(a0, a1) {
  return _emscripten_webgl_enable_extension_calling_thread(a0, a1);
}
function _emscripten_webgl_get_context_attributes(c, a) {
  if (!a) return -5;
  c = GL.contexts[c];
  if (!c) return -3;
  var t = c.GLctx;
  if (!t) return -3;
  t = t.getContextAttributes();
  HEAP32[a >> 2] = t.alpha;
  HEAP32[(a + 4) >> 2] = t.depth;
  HEAP32[(a + 8) >> 2] = t.stencil;
  HEAP32[(a + 12) >> 2] = t.antialias;
  HEAP32[(a + 16) >> 2] = t.premultipliedAlpha;
  HEAP32[(a + 20) >> 2] = t.preserveDrawingBuffer;
  var power =
    t["powerPreference"] &&
    __emscripten_webgl_power_preferences.indexOf(t["powerPreference"]);
  HEAP32[(a + 24) >> 2] = power;
  HEAP32[(a + 28) >> 2] = t.failIfMajorPerformanceCaveat;
  HEAP32[(a + 32) >> 2] = c.version;
  HEAP32[(a + 36) >> 2] = 0;
  HEAP32[(a + 40) >> 2] = c.attributes.enableExtensionsByDefault;
  return 0;
}
function _emscripten_webgl_do_get_current_context() {
  return GL.currentContext ? GL.currentContext.handle : 0;
}
function _emscripten_webgl_get_current_context() {
  return _emscripten_webgl_do_get_current_context();
}
Module[
  "_emscripten_webgl_get_current_context"
] = _emscripten_webgl_get_current_context;
function _emscripten_webgl_init_context_attributes(attributes) {
  var a = attributes >> 2;
  for (var i = 0; i < 56 >> 2; ++i) {
    HEAP32[a + i] = 0;
  }
  HEAP32[a + (0 >> 2)] = HEAP32[a + (4 >> 2)] = HEAP32[a + (12 >> 2)] = HEAP32[
    a + (16 >> 2)
  ] = HEAP32[a + (32 >> 2)] = HEAP32[a + (40 >> 2)] = 1;
}
function _emscripten_webgl_make_context_current(contextHandle) {
  var success = GL.makeContextCurrent(contextHandle);
  return success ? 0 : -5;
}
Module[
  "_emscripten_webgl_make_context_current"
] = _emscripten_webgl_make_context_current;
var ENV = {};
function __getExecutableName() {
  return "./this.program";
}
function _emscripten_get_environ() {
  if (!_emscripten_get_environ.strings) {
    var env = {
      USER: "web_user",
      LOGNAME: "web_user",
      PATH: "/",
      PWD: "/",
      HOME: "/home/web_user",
      LANG:
        (
          (typeof navigator === "object" &&
            navigator.languages &&
            navigator.languages[0]) ||
          "C"
        ).replace("-", "_") + ".UTF-8",
      _: __getExecutableName(),
    };
    for (var x in ENV) {
      env[x] = ENV[x];
    }
    var strings = [];
    for (var x in env) {
      strings.push(x + "=" + env[x]);
    }
    _emscripten_get_environ.strings = strings;
  }
  return _emscripten_get_environ.strings;
}
function writeAsciiToMemory(str, buffer, dontAddNull) {
  for (var i = 0; i < str.length; ++i) {
    HEAP8[buffer++ >> 0] = str.charCodeAt(i);
  }
  if (!dontAddNull) HEAP8[buffer >> 0] = 0;
}
function _environ_get(__environ, environ_buf) {
  var strings = _emscripten_get_environ();
  var bufSize = 0;
  strings.forEach(function (string, i) {
    var ptr = environ_buf + bufSize;
    HEAP32[(__environ + i * 4) >> 2] = ptr;
    writeAsciiToMemory(string, ptr);
    bufSize += string.length + 1;
  });
  return 0;
}
function _environ_sizes_get(penviron_count, penviron_buf_size) {
  var strings = _emscripten_get_environ();
  HEAP32[penviron_count >> 2] = strings.length;
  var bufSize = 0;
  strings.forEach(function (string) {
    bufSize += string.length + 1;
  });
  HEAP32[penviron_buf_size >> 2] = bufSize;
  return 0;
}
function _exit(status) {
  throw "exit(" + status + ")";
}
function _fd_close(fd) {
  return 0;
}
function _fd_read(fd, iov, iovcnt, pnum) {
  var stream = SYSCALLS.getStreamFromFD(fd);
  var num = SYSCALLS.doReadv(stream, iov, iovcnt);
  HEAP32[pnum >> 2] = num;
  return 0;
}
function _fd_seek(fd, offset_low, offset_high, whence, newOffset) {}
function _fd_write(fd, iov, iovcnt, pnum) {
  var num = 0;
  for (var i = 0; i < iovcnt; i++) {
    var ptr = HEAP32[(iov + i * 8) >> 2];
    var len = HEAP32[(iov + (i * 8 + 4)) >> 2];
    for (var j = 0; j < len; j++) {
      SYSCALLS.printChar(fd, HEAPU8[ptr + j]);
    }
    num += len;
  }
  HEAP32[pnum >> 2] = num;
  return 0;
}
function _gettimeofday(ptr) {
  var now = Date.now();
  HEAP32[ptr >> 2] = (now / 1e3) | 0;
  HEAP32[(ptr + 4) >> 2] = ((now % 1e3) * 1e3) | 0;
  return 0;
}
function _glActiveTexture(x0) {
  GLctx["activeTexture"](x0);
}
function _glAttachShader(program, shader) {
  GLctx.attachShader(GL.programs[program], GL.shaders[shader]);
}
function _glBindBuffer(target, buffer) {
  if (target == 35051) {
    GLctx.currentPixelPackBufferBinding = buffer;
  } else if (target == 35052) {
    GLctx.currentPixelUnpackBufferBinding = buffer;
  }
  GLctx.bindBuffer(target, GL.buffers[buffer]);
}
function _glBindFramebuffer(target, framebuffer) {
  GLctx.bindFramebuffer(target, GL.framebuffers[framebuffer]);
}
function _glBindRenderbuffer(target, renderbuffer) {
  GLctx.bindRenderbuffer(target, GL.renderbuffers[renderbuffer]);
}
function _glBindTexture(target, texture) {
  GLctx.bindTexture(target, GL.textures[texture]);
}
function _glBlendColor(x0, x1, x2, x3) {
  GLctx["blendColor"](x0, x1, x2, x3);
}
function _glBlendEquationSeparate(x0, x1) {
  GLctx["blendEquationSeparate"](x0, x1);
}
function _glBlendFuncSeparate(x0, x1, x2, x3) {
  GLctx["blendFuncSeparate"](x0, x1, x2, x3);
}
function _glBufferData(target, size, data, usage) {
  if (GL.currentContext.version >= 2) {
    if (data) {
      GLctx.bufferData(target, HEAPU8, usage, data, size);
    } else {
      GLctx.bufferData(target, size, usage);
    }
  } else {
    GLctx.bufferData(
      target,
      data ? HEAPU8.subarray(data, data + size) : size,
      usage
    );
  }
}
function _glBufferSubData(target, offset, size, data) {
  if (GL.currentContext.version >= 2) {
    GLctx.bufferSubData(target, offset, HEAPU8, data, size);
    return;
  }
  GLctx.bufferSubData(target, offset, HEAPU8.subarray(data, data + size));
}
function _glCheckFramebufferStatus(x0) {
  return GLctx["checkFramebufferStatus"](x0);
}
function _glClear(x0) {
  GLctx["clear"](x0);
}
function _glClearColor(x0, x1, x2, x3) {
  GLctx["clearColor"](x0, x1, x2, x3);
}
function _glClearDepthf(x0) {
  GLctx["clearDepth"](x0);
}
function _glClearStencil(x0) {
  GLctx["clearStencil"](x0);
}
function _glColorMask(red, green, blue, alpha) {
  GLctx.colorMask(!!red, !!green, !!blue, !!alpha);
}
function _glCompileShader(shader) {
  GLctx.compileShader(GL.shaders[shader]);
}
function _glCompressedTexImage2D(
  target,
  level,
  internalFormat,
  width,
  height,
  border,
  imageSize,
  data
) {
  if (GL.currentContext.version >= 2) {
    if (GLctx.currentPixelUnpackBufferBinding) {
      GLctx["compressedTexImage2D"](
        target,
        level,
        internalFormat,
        width,
        height,
        border,
        imageSize,
        data
      );
    } else {
      GLctx["compressedTexImage2D"](
        target,
        level,
        internalFormat,
        width,
        height,
        border,
        HEAPU8,
        data,
        imageSize
      );
    }
    return;
  }
  GLctx["compressedTexImage2D"](
    target,
    level,
    internalFormat,
    width,
    height,
    border,
    data ? HEAPU8.subarray(data, data + imageSize) : null
  );
}
function _glCompressedTexSubImage2D(
  target,
  level,
  xoffset,
  yoffset,
  width,
  height,
  format,
  imageSize,
  data
) {
  if (GL.currentContext.version >= 2) {
    if (GLctx.currentPixelUnpackBufferBinding) {
      GLctx["compressedTexSubImage2D"](
        target,
        level,
        xoffset,
        yoffset,
        width,
        height,
        format,
        imageSize,
        data
      );
    } else {
      GLctx["compressedTexSubImage2D"](
        target,
        level,
        xoffset,
        yoffset,
        width,
        height,
        format,
        HEAPU8,
        data,
        imageSize
      );
    }
    return;
  }
  GLctx["compressedTexSubImage2D"](
    target,
    level,
    xoffset,
    yoffset,
    width,
    height,
    format,
    data ? HEAPU8.subarray(data, data + imageSize) : null
  );
}
function _glCreateProgram() {
  var id = GL.getNewId(GL.programs);
  var program = GLctx.createProgram();
  program.name = id;
  GL.programs[id] = program;
  return id;
}
function _glCreateShader(shaderType) {
  var id = GL.getNewId(GL.shaders);
  GL.shaders[id] = GLctx.createShader(shaderType);
  return id;
}
function _glCullFace(x0) {
  GLctx["cullFace"](x0);
}
function _glDeleteBuffers(n, buffers) {
  for (var i = 0; i < n; i++) {
    var id = HEAP32[(buffers + i * 4) >> 2];
    var buffer = GL.buffers[id];
    if (!buffer) continue;
    GLctx.deleteBuffer(buffer);
    buffer.name = 0;
    GL.buffers[id] = null;
    if (id == GL.currArrayBuffer) GL.currArrayBuffer = 0;
    if (id == GL.currElementArrayBuffer) GL.currElementArrayBuffer = 0;
    if (id == GLctx.currentPixelPackBufferBinding)
      GLctx.currentPixelPackBufferBinding = 0;
    if (id == GLctx.currentPixelUnpackBufferBinding)
      GLctx.currentPixelUnpackBufferBinding = 0;
  }
}
function _glDeleteFramebuffers(n, framebuffers) {
  for (var i = 0; i < n; ++i) {
    var id = HEAP32[(framebuffers + i * 4) >> 2];
    var framebuffer = GL.framebuffers[id];
    if (!framebuffer) continue;
    GLctx.deleteFramebuffer(framebuffer);
    framebuffer.name = 0;
    GL.framebuffers[id] = null;
  }
}
function _glDeleteProgram(id) {
  if (!id) return;
  var program = GL.programs[id];
  if (!program) {
    GL.recordError(1281);
    return;
  }
  GLctx.deleteProgram(program);
  program.name = 0;
  GL.programs[id] = null;
  GL.programInfos[id] = null;
}
function _glDeleteRenderbuffers(n, renderbuffers) {
  for (var i = 0; i < n; i++) {
    var id = HEAP32[(renderbuffers + i * 4) >> 2];
    var renderbuffer = GL.renderbuffers[id];
    if (!renderbuffer) continue;
    GLctx.deleteRenderbuffer(renderbuffer);
    renderbuffer.name = 0;
    GL.renderbuffers[id] = null;
  }
}
function _glDeleteShader(id) {
  if (!id) return;
  var shader = GL.shaders[id];
  if (!shader) {
    GL.recordError(1281);
    return;
  }
  GLctx.deleteShader(shader);
  GL.shaders[id] = null;
}
function _glDeleteTextures(n, textures) {
  for (var i = 0; i < n; i++) {
    var id = HEAP32[(textures + i * 4) >> 2];
    var texture = GL.textures[id];
    if (!texture) continue;
    GLctx.deleteTexture(texture);
    texture.name = 0;
    GL.textures[id] = null;
  }
}
function _glDepthFunc(x0) {
  GLctx["depthFunc"](x0);
}
function _glDepthMask(flag) {
  GLctx.depthMask(!!flag);
}
function _glDetachShader(program, shader) {
  GLctx.detachShader(GL.programs[program], GL.shaders[shader]);
}
function _glDisable(x0) {
  GLctx["disable"](x0);
}
function _glDisableVertexAttribArray(index) {
  GLctx.disableVertexAttribArray(index);
}
function _glDrawArrays(mode, first, count) {
  GLctx.drawArrays(mode, first, count);
}
function _glEnable(x0) {
  GLctx["enable"](x0);
}
function _glEnableVertexAttribArray(index) {
  GLctx.enableVertexAttribArray(index);
}
function _glFlush() {
  GLctx["flush"]();
}
function _glFramebufferRenderbuffer(
  target,
  attachment,
  renderbuffertarget,
  renderbuffer
) {
  GLctx.framebufferRenderbuffer(
    target,
    attachment,
    renderbuffertarget,
    GL.renderbuffers[renderbuffer]
  );
}
function _glFramebufferTexture2D(
  target,
  attachment,
  textarget,
  texture,
  level
) {
  GLctx.framebufferTexture2D(
    target,
    attachment,
    textarget,
    GL.textures[texture],
    level
  );
}
function _glFrontFace(x0) {
  GLctx["frontFace"](x0);
}
function _glGenBuffers(n, buffers) {
  __glGenObject(n, buffers, "createBuffer", GL.buffers);
}
function _glGenFramebuffers(n, ids) {
  __glGenObject(n, ids, "createFramebuffer", GL.framebuffers);
}
function _glGenRenderbuffers(n, renderbuffers) {
  __glGenObject(n, renderbuffers, "createRenderbuffer", GL.renderbuffers);
}
function _glGenTextures(n, textures) {
  __glGenObject(n, textures, "createTexture", GL.textures);
}
function _glGenerateMipmap(x0) {
  GLctx["generateMipmap"](x0);
}
function _glGetActiveAttrib(program, index, bufSize, length, size, type, name) {
  program = GL.programs[program];
  var info = GLctx.getActiveAttrib(program, index);
  if (!info) return;
  var numBytesWrittenExclNull =
    bufSize > 0 && name ? stringToUTF8(info.name, name, bufSize) : 0;
  if (length) HEAP32[length >> 2] = numBytesWrittenExclNull;
  if (size) HEAP32[size >> 2] = info.size;
  if (type) HEAP32[type >> 2] = info.type;
}
function _glGetActiveUniform(
  program,
  index,
  bufSize,
  length,
  size,
  type,
  name
) {
  program = GL.programs[program];
  var info = GLctx.getActiveUniform(program, index);
  if (!info) return;
  var numBytesWrittenExclNull =
    bufSize > 0 && name ? stringToUTF8(info.name, name, bufSize) : 0;
  if (length) HEAP32[length >> 2] = numBytesWrittenExclNull;
  if (size) HEAP32[size >> 2] = info.size;
  if (type) HEAP32[type >> 2] = info.type;
}
function _glGetAttribLocation(program, name) {
  return GLctx.getAttribLocation(GL.programs[program], UTF8ToString(name));
}
function _glGetError() {
  var error = GLctx.getError() || GL.lastError;
  GL.lastError = 0;
  return error;
}
function _glGetFloatv(name_, p) {
  emscriptenWebGLGet(name_, p, 2);
}
function _glGetIntegerv(name_, p) {
  emscriptenWebGLGet(name_, p, 0);
}
function _glGetProgramInfoLog(program, maxLength, length, infoLog) {
  var log = GLctx.getProgramInfoLog(GL.programs[program]);
  if (log === null) log = "(unknown error)";
  var numBytesWrittenExclNull =
    maxLength > 0 && infoLog ? stringToUTF8(log, infoLog, maxLength) : 0;
  if (length) HEAP32[length >> 2] = numBytesWrittenExclNull;
}
function _glGetProgramiv(program, pname, p) {
  if (!p) {
    GL.recordError(1281);
    return;
  }
  if (program >= GL.counter) {
    GL.recordError(1281);
    return;
  }
  var ptable = GL.programInfos[program];
  if (!ptable) {
    GL.recordError(1282);
    return;
  }
  if (pname == 35716) {
    var log = GLctx.getProgramInfoLog(GL.programs[program]);
    if (log === null) log = "(unknown error)";
    HEAP32[p >> 2] = log.length + 1;
  } else if (pname == 35719) {
    HEAP32[p >> 2] = ptable.maxUniformLength;
  } else if (pname == 35722) {
    if (ptable.maxAttributeLength == -1) {
      program = GL.programs[program];
      var numAttribs = GLctx.getProgramParameter(program, 35721);
      ptable.maxAttributeLength = 0;
      for (var i = 0; i < numAttribs; ++i) {
        var activeAttrib = GLctx.getActiveAttrib(program, i);
        ptable.maxAttributeLength = Math.max(
          ptable.maxAttributeLength,
          activeAttrib.name.length + 1
        );
      }
    }
    HEAP32[p >> 2] = ptable.maxAttributeLength;
  } else if (pname == 35381) {
    if (ptable.maxUniformBlockNameLength == -1) {
      program = GL.programs[program];
      var numBlocks = GLctx.getProgramParameter(program, 35382);
      ptable.maxUniformBlockNameLength = 0;
      for (var i = 0; i < numBlocks; ++i) {
        var activeBlockName = GLctx.getActiveUniformBlockName(program, i);
        ptable.maxUniformBlockNameLength = Math.max(
          ptable.maxUniformBlockNameLength,
          activeBlockName.length + 1
        );
      }
    }
    HEAP32[p >> 2] = ptable.maxUniformBlockNameLength;
  } else {
    HEAP32[p >> 2] = GLctx.getProgramParameter(GL.programs[program], pname);
  }
}
function _glGetShaderInfoLog(shader, maxLength, length, infoLog) {
  var log = GLctx.getShaderInfoLog(GL.shaders[shader]);
  if (log === null) log = "(unknown error)";
  var numBytesWrittenExclNull =
    maxLength > 0 && infoLog ? stringToUTF8(log, infoLog, maxLength) : 0;
  if (length) HEAP32[length >> 2] = numBytesWrittenExclNull;
}
function _glGetShaderiv(shader, pname, p) {
  if (!p) {
    GL.recordError(1281);
    return;
  }
  if (pname == 35716) {
    var log = GLctx.getShaderInfoLog(GL.shaders[shader]);
    if (log === null) log = "(unknown error)";
    HEAP32[p >> 2] = log.length + 1;
  } else if (pname == 35720) {
    var source = GLctx.getShaderSource(GL.shaders[shader]);
    var sourceLength =
      source === null || source.length == 0 ? 0 : source.length + 1;
    HEAP32[p >> 2] = sourceLength;
  } else {
    HEAP32[p >> 2] = GLctx.getShaderParameter(GL.shaders[shader], pname);
  }
}
function _glGetString(name_) {
  if (GL.stringCache[name_]) return GL.stringCache[name_];
  var ret;
  switch (name_) {
    case 7939:
      var exts = GLctx.getSupportedExtensions() || [];
      exts = exts.concat(
        exts.map(function (e) {
          return "GL_" + e;
        })
      );
      ret = stringToNewUTF8(exts.join(" "));
      break;
    case 7936:
    case 7937:
    case 37445:
    case 37446:
      var s = GLctx.getParameter(name_);
      if (!s) {
        GL.recordError(1280);
      }
      ret = stringToNewUTF8(s);
      break;
    case 7938:
      var glVersion = GLctx.getParameter(7938);
      if (GL.currentContext.version >= 2)
        glVersion = "OpenGL ES 3.0 (" + glVersion + ")";
      else {
        glVersion = "OpenGL ES 2.0 (" + glVersion + ")";
      }
      ret = stringToNewUTF8(glVersion);
      break;
    case 35724:
      var glslVersion = GLctx.getParameter(35724);
      var ver_re = /^WebGL GLSL ES ([0-9]\.[0-9][0-9]?)(?:$| .*)/;
      var ver_num = glslVersion.match(ver_re);
      if (ver_num !== null) {
        if (ver_num[1].length == 3) ver_num[1] = ver_num[1] + "0";
        glslVersion =
          "OpenGL ES GLSL ES " + ver_num[1] + " (" + glslVersion + ")";
      }
      ret = stringToNewUTF8(glslVersion);
      break;
    default:
      GL.recordError(1280);
      return 0;
  }
  GL.stringCache[name_] = ret;
  return ret;
}
function _glGetUniformLocation(program, name) {
  name = UTF8ToString(name);
  var arrayIndex = 0;
  if (name[name.length - 1] == "]") {
    var leftBrace = name.lastIndexOf("[");
    arrayIndex =
      name[leftBrace + 1] != "]" ? jstoi_q(name.slice(leftBrace + 1)) : 0;
    name = name.slice(0, leftBrace);
  }
  var uniformInfo =
    GL.programInfos[program] && GL.programInfos[program].uniforms[name];
  if (uniformInfo && arrayIndex >= 0 && arrayIndex < uniformInfo[0]) {
    return uniformInfo[1] + arrayIndex;
  } else {
    return -1;
  }
}
function _glLinkProgram(program) {
  GLctx.linkProgram(GL.programs[program]);
  GL.populateUniformTable(program);
}
function _glPixelStorei(pname, param) {
  if (pname == 3317) {
    GL.unpackAlignment = param;
  }
  GLctx.pixelStorei(pname, param);
}
function _glReadPixels(x, y, width, height, format, type, pixels) {
  if (GL.currentContext.version >= 2) {
    if (GLctx.currentPixelPackBufferBinding) {
      GLctx.readPixels(x, y, width, height, format, type, pixels);
    } else {
      var heap = __heapObjectForWebGLType(type);
      GLctx.readPixels(
        x,
        y,
        width,
        height,
        format,
        type,
        heap,
        pixels >> __heapAccessShiftForWebGLHeap(heap)
      );
    }
    return;
  }
  var pixelData = emscriptenWebGLGetTexPixelData(
    type,
    format,
    width,
    height,
    pixels,
    format
  );
  if (!pixelData) {
    GL.recordError(1280);
    return;
  }
  GLctx.readPixels(x, y, width, height, format, type, pixelData);
}
function _glRenderbufferStorage(x0, x1, x2, x3) {
  GLctx["renderbufferStorage"](x0, x1, x2, x3);
}
function _glScissor(x0, x1, x2, x3) {
  GLctx["scissor"](x0, x1, x2, x3);
}
function _glShaderSource(shader, count, string, length) {
  var source = GL.getSource(shader, count, string, length);
  GLctx.shaderSource(GL.shaders[shader], source);
}
function _glStencilFuncSeparate(x0, x1, x2, x3) {
  GLctx["stencilFuncSeparate"](x0, x1, x2, x3);
}
function _glStencilOpSeparate(x0, x1, x2, x3) {
  GLctx["stencilOpSeparate"](x0, x1, x2, x3);
}
function _glTexImage2D(
  target,
  level,
  internalFormat,
  width,
  height,
  border,
  format,
  type,
  pixels
) {
  if (GL.currentContext.version >= 2) {
    if (GLctx.currentPixelUnpackBufferBinding) {
      GLctx.texImage2D(
        target,
        level,
        internalFormat,
        width,
        height,
        border,
        format,
        type,
        pixels
      );
    } else if (pixels) {
      var heap = __heapObjectForWebGLType(type);
      GLctx.texImage2D(
        target,
        level,
        internalFormat,
        width,
        height,
        border,
        format,
        type,
        heap,
        pixels >> __heapAccessShiftForWebGLHeap(heap)
      );
    } else {
      GLctx.texImage2D(
        target,
        level,
        internalFormat,
        width,
        height,
        border,
        format,
        type,
        null
      );
    }
    return;
  }
  GLctx.texImage2D(
    target,
    level,
    internalFormat,
    width,
    height,
    border,
    format,
    type,
    pixels
      ? emscriptenWebGLGetTexPixelData(
          type,
          format,
          width,
          height,
          pixels,
          internalFormat
        )
      : null
  );
}
function _glTexParameterf(x0, x1, x2) {
  GLctx["texParameterf"](x0, x1, x2);
}
function _glTexParameterfv(target, pname, params) {
  var param = HEAPF32[params >> 2];
  GLctx.texParameterf(target, pname, param);
}
function _glTexParameteri(x0, x1, x2) {
  GLctx["texParameteri"](x0, x1, x2);
}
function _glTexSubImage2D(
  target,
  level,
  xoffset,
  yoffset,
  width,
  height,
  format,
  type,
  pixels
) {
  if (GL.currentContext.version >= 2) {
    if (GLctx.currentPixelUnpackBufferBinding) {
      GLctx.texSubImage2D(
        target,
        level,
        xoffset,
        yoffset,
        width,
        height,
        format,
        type,
        pixels
      );
    } else if (pixels) {
      var heap = __heapObjectForWebGLType(type);
      GLctx.texSubImage2D(
        target,
        level,
        xoffset,
        yoffset,
        width,
        height,
        format,
        type,
        heap,
        pixels >> __heapAccessShiftForWebGLHeap(heap)
      );
    } else {
      GLctx.texSubImage2D(
        target,
        level,
        xoffset,
        yoffset,
        width,
        height,
        format,
        type,
        null
      );
    }
    return;
  }
  var pixelData = null;
  if (pixels)
    pixelData = emscriptenWebGLGetTexPixelData(
      type,
      format,
      width,
      height,
      pixels,
      0
    );
  GLctx.texSubImage2D(
    target,
    level,
    xoffset,
    yoffset,
    width,
    height,
    format,
    type,
    pixelData
  );
}
function _glUniform1i(location, v0) {
  GLctx.uniform1i(GL.uniforms[location], v0);
}
function _glUniform1iv(location, count, value) {
  if (GL.currentContext.version >= 2) {
    GLctx.uniform1iv(GL.uniforms[location], HEAP32, value >> 2, count);
    return;
  }
  if (count <= GL.MINI_TEMP_BUFFER_SIZE) {
    var view = GL.miniTempBufferIntViews[count - 1];
    for (var i = 0; i < count; ++i) {
      view[i] = HEAP32[(value + 4 * i) >> 2];
    }
  } else {
    var view = HEAP32.subarray(value >> 2, (value + count * 4) >> 2);
  }
  GLctx.uniform1iv(GL.uniforms[location], view);
}
function _glUniform4f(location, v0, v1, v2, v3) {
  GLctx.uniform4f(GL.uniforms[location], v0, v1, v2, v3);
}
function _glUniform4fv(location, count, value) {
  if (GL.currentContext.version >= 2) {
    GLctx.uniform4fv(GL.uniforms[location], HEAPF32, value >> 2, count * 4);
    return;
  }
  if (4 * count <= GL.MINI_TEMP_BUFFER_SIZE) {
    var view = GL.miniTempBufferFloatViews[4 * count - 1];
    for (var i = 0; i < 4 * count; i += 4) {
      view[i] = HEAPF32[(value + 4 * i) >> 2];
      view[i + 1] = HEAPF32[(value + (4 * i + 4)) >> 2];
      view[i + 2] = HEAPF32[(value + (4 * i + 8)) >> 2];
      view[i + 3] = HEAPF32[(value + (4 * i + 12)) >> 2];
    }
  } else {
    var view = HEAPF32.subarray(value >> 2, (value + count * 16) >> 2);
  }
  GLctx.uniform4fv(GL.uniforms[location], view);
}
function _glUniformMatrix3fv(location, count, transpose, value) {
  if (GL.currentContext.version >= 2) {
    GLctx.uniformMatrix3fv(
      GL.uniforms[location],
      !!transpose,
      HEAPF32,
      value >> 2,
      count * 9
    );
    return;
  }
  if (9 * count <= GL.MINI_TEMP_BUFFER_SIZE) {
    var view = GL.miniTempBufferFloatViews[9 * count - 1];
    for (var i = 0; i < 9 * count; i += 9) {
      view[i] = HEAPF32[(value + 4 * i) >> 2];
      view[i + 1] = HEAPF32[(value + (4 * i + 4)) >> 2];
      view[i + 2] = HEAPF32[(value + (4 * i + 8)) >> 2];
      view[i + 3] = HEAPF32[(value + (4 * i + 12)) >> 2];
      view[i + 4] = HEAPF32[(value + (4 * i + 16)) >> 2];
      view[i + 5] = HEAPF32[(value + (4 * i + 20)) >> 2];
      view[i + 6] = HEAPF32[(value + (4 * i + 24)) >> 2];
      view[i + 7] = HEAPF32[(value + (4 * i + 28)) >> 2];
      view[i + 8] = HEAPF32[(value + (4 * i + 32)) >> 2];
    }
  } else {
    var view = HEAPF32.subarray(value >> 2, (value + count * 36) >> 2);
  }
  GLctx.uniformMatrix3fv(GL.uniforms[location], !!transpose, view);
}
function _glUniformMatrix4fv(location, count, transpose, value) {
  if (GL.currentContext.version >= 2) {
    GLctx.uniformMatrix4fv(
      GL.uniforms[location],
      !!transpose,
      HEAPF32,
      value >> 2,
      count * 16
    );
    return;
  }
  if (16 * count <= GL.MINI_TEMP_BUFFER_SIZE) {
    var view = GL.miniTempBufferFloatViews[16 * count - 1];
    for (var i = 0; i < 16 * count; i += 16) {
      view[i] = HEAPF32[(value + 4 * i) >> 2];
      view[i + 1] = HEAPF32[(value + (4 * i + 4)) >> 2];
      view[i + 2] = HEAPF32[(value + (4 * i + 8)) >> 2];
      view[i + 3] = HEAPF32[(value + (4 * i + 12)) >> 2];
      view[i + 4] = HEAPF32[(value + (4 * i + 16)) >> 2];
      view[i + 5] = HEAPF32[(value + (4 * i + 20)) >> 2];
      view[i + 6] = HEAPF32[(value + (4 * i + 24)) >> 2];
      view[i + 7] = HEAPF32[(value + (4 * i + 28)) >> 2];
      view[i + 8] = HEAPF32[(value + (4 * i + 32)) >> 2];
      view[i + 9] = HEAPF32[(value + (4 * i + 36)) >> 2];
      view[i + 10] = HEAPF32[(value + (4 * i + 40)) >> 2];
      view[i + 11] = HEAPF32[(value + (4 * i + 44)) >> 2];
      view[i + 12] = HEAPF32[(value + (4 * i + 48)) >> 2];
      view[i + 13] = HEAPF32[(value + (4 * i + 52)) >> 2];
      view[i + 14] = HEAPF32[(value + (4 * i + 56)) >> 2];
      view[i + 15] = HEAPF32[(value + (4 * i + 60)) >> 2];
    }
  } else {
    var view = HEAPF32.subarray(value >> 2, (value + count * 64) >> 2);
  }
  GLctx.uniformMatrix4fv(GL.uniforms[location], !!transpose, view);
}
function _glUseProgram(program) {
  GLctx.useProgram(GL.programs[program]);
}
function _glVertexAttribPointer(index, size, type, normalized, stride, ptr) {
  GLctx.vertexAttribPointer(index, size, type, !!normalized, stride, ptr);
}
function _glViewport(x0, x1, x2, x3) {
  GLctx["viewport"](x0, x1, x2, x3);
}
function _js_html_audioCheckLoad(audioClipIdx) {
  return 2;
}
function _js_html_audioFree(audioClipIdx) {
  return;
}
function _js_html_audioIsPlaying(audioSourceIdx) {
  if (!this.audioContext || audioSourceIdx < 0) return false;
  if (this.audioSources[audioSourceIdx] == null) return false;
  return this.audioSources[audioSourceIdx].isPlaying;
}
function _js_html_audioIsUnlocked() {
  return false;
}
function _js_html_audioPause() {
  if (this.audioContext && this.audioContext.suspend) {
    this.audioContext.suspend();
  }
}
function _js_html_audioPlay(
  audioClipIdx,
  audioSourceIdx,
  volume,
  pitch,
  pan,
  loop
) {
  if (!this.audioContext || audioClipIdx < 0 || audioSourceIdx < 0)
    return false;
  if (this.audioContext.state !== "running") return false;
  var srcBuffer = this.audioBuffers[audioClipIdx];
  if (!srcBuffer || typeof srcBuffer === "string") return false;
  var sourceNode = this.audioContext.createBufferSource();
  sourceNode.buffer = srcBuffer;
  sourceNode.playbackRate.value = pitch;
  var panNode = this.audioContext.createPanner();
  panNode.panningModel = "equalpower";
  sourceNode.panNode = panNode;
  var gainNode = this.audioContext.createGain();
  gainNode.buffer = srcBuffer;
  sourceNode.gainNode = gainNode;
  sourceNode.connect(gainNode);
  sourceNode.gainNode.connect(panNode);
  sourceNode.panNode.connect(this.audioContext.destination);
  ut._HTML.audio_setGain(sourceNode, volume);
  ut._HTML.audio_setPan(sourceNode, pan);
  sourceNode.loop = loop;
  if (this.audioSources[audioSourceIdx] != undefined)
    this.audioSources[audioSourceIdx].stop();
  this.audioSources[audioSourceIdx] = sourceNode;
  sourceNode.onended = function (event) {
    sourceNode.stop();
    sourceNode.isPlaying = false;
  };
  sourceNode.start();
  sourceNode.isPlaying = true;
  return true;
}
function _js_html_audioResume() {
  if (this.audioContext && this.audioContext.resume) {
    this.audioContext.resume();
  }
}
function _js_html_audioSetPan(audioSourceIdx, pan) {
  if (!this.audioContext || audioSourceIdx < 0) return false;
  var sourceNode = this.audioSources[audioSourceIdx];
  if (!sourceNode) return false;
  ut._HTML.audio_setPan(sourceNode, pan);
  return true;
}
function _js_html_audioSetPitch(audioSourceIdx, pitch) {
  if (!this.audioContext || audioSourceIdx < 0) return false;
  var sourceNode = this.audioSources[audioSourceIdx];
  if (!sourceNode) return false;
  sourceNode.playbackRate.value = pitch;
  return true;
}
function _js_html_audioSetVolume(audioSourceIdx, volume) {
  if (!this.audioContext || audioSourceIdx < 0) return false;
  var sourceNode = this.audioSources[audioSourceIdx];
  if (!sourceNode) return false;
  ut._HTML.audio_setGain(sourceNode, volume);
  return true;
}
function _js_html_audioStartLoadFile(audioClipName, audioClipIdx) {
  return -1;
}
function _js_html_audioStop(audioSourceIdx, dostop) {
  if (!this.audioContext || audioSourceIdx < 0) return;
  var sourceNode = this.audioSources[audioSourceIdx];
  if (!sourceNode) return;
  sourceNode.onended = null;
  this.audioSources[audioSourceIdx] = null;
  if (sourceNode.isPlaying && dostop) {
    sourceNode.stop();
    sourceNode.isPlaying = false;
  }
}
function _js_html_audioUnlock() {
  return;
}
function _js_html_checkLoadImage(idx) {
  var img = ut._HTML.images[idx];
  if (img.loaderror) {
    return 2;
  }
  if (img.image) {
    if (
      !img.image.complete ||
      !img.image.naturalWidth ||
      !img.image.naturalHeight
    )
      return 0;
  }
  if (img.mask) {
    if (!img.mask.complete || !img.mask.naturalWidth || !img.mask.naturalHeight)
      return 0;
  }
  return 1;
}
function _js_html_finishLoadImage(idx, wPtr, hPtr, alphaPtr) {
  var img = ut._HTML.images[idx];
  if (img.image && img.mask) {
    var width = img.image.naturalWidth;
    var height = img.image.naturalHeight;
    var maskwidth = img.mask.naturalWidth;
    var maskheight = img.mask.naturalHeight;
    var cvscolor = document.createElement("canvas");
    cvscolor.width = width;
    cvscolor.height = height;
    var cxcolor = cvscolor.getContext("2d");
    cxcolor.globalCompositeOperation = "copy";
    cxcolor.drawImage(img.image, 0, 0);
    var cvsalpha = document.createElement("canvas");
    cvsalpha.width = width;
    cvsalpha.height = height;
    var cxalpha = cvsalpha.getContext("2d");
    cxalpha.globalCompositeOperation = "copy";
    cxalpha.drawImage(img.mask, 0, 0, width, height);
    var colorBits = cxcolor.getImageData(0, 0, width, height);
    var alphaBits = cxalpha.getImageData(0, 0, width, height);
    var cdata = colorBits.data,
      adata = alphaBits.data;
    var sz = width * height;
    for (var i = 0; i < sz; i++) cdata[(i << 2) + 3] = adata[i << 2];
    cxcolor.putImageData(colorBits, 0, 0);
    img.image = cvscolor;
    img.image.naturalWidth = width;
    img.image.naturalHeight = height;
    img.hasAlpha = true;
  } else if (!img.image && img.mask) {
    var width = img.mask.naturalWidth;
    var height = img.mask.naturalHeight;
    var cvscolor = document.createElement("canvas");
    cvscolor.width = width;
    cvscolor.height = height;
    var cxcolor = cvscolor.getContext("2d");
    cxcolor.globalCompositeOperation = "copy";
    cxcolor.drawImage(img.mask, 0, 0);
    var colorBits = cxcolor.getImageData(0, 0, width, height);
    var cdata = colorBits.data;
    var sz = width * height;
    for (var i = 0; i < sz; i++) {
      cdata[(i << 2) + 1] = cdata[i << 2];
      cdata[(i << 2) + 2] = cdata[i << 2];
      cdata[(i << 2) + 3] = cdata[i << 2];
    }
    cxcolor.putImageData(colorBits, 0, 0);
    img.image = cvscolor;
    img.image.naturalWidth = width;
    img.image.naturalHeight = height;
    img.hasAlpha = true;
  }
  HEAP32[wPtr >> 2] = img.image.naturalWidth;
  HEAP32[hPtr >> 2] = img.image.naturalHeight;
  HEAP32[alphaPtr >> 2] = img.hasAlpha;
}
function _js_html_freeImage(idx) {
  ut._HTML.images[idx] = null;
}
function _js_html_getCanvasSize(wPtr, hPtr) {
  var html = ut._HTML;
  HEAP32[wPtr >> 2] = html.canvasElement.width | 0;
  HEAP32[hPtr >> 2] = html.canvasElement.height | 0;
}
function _js_html_getDPIScale() {
  return window.devicePixelRatio;
}
function _js_html_getFrameSize(wPtr, hPtr) {
  HEAP32[wPtr >> 2] = window.innerWidth | 0;
  HEAP32[hPtr >> 2] = window.innerHeight | 0;
}
function _js_html_getScreenSize(wPtr, hPtr) {
  HEAP32[wPtr >> 2] = screen.width | 0;
  HEAP32[hPtr >> 2] = screen.height | 0;
}
function _js_html_imageToMemory(idx, w, h, dest) {
  var cvs = ut._HTML.readyCanvasForReadback(idx, w, h);
  if (!cvs) return 0;
  var cx = cvs.getContext("2d");
  var imd = cx.getImageData(0, 0, w, h);
  HEAPU8.set(imd.data, dest);
  return 1;
}
function _js_html_init() {
  ut = ut || {};
  ut._HTML = ut._HTML || {};
  var html = ut._HTML;
  html.visible = true;
  html.focused = true;
}
function _js_html_initAudio() {
  ut = ut || {};
  ut._HTML = ut._HTML || {};
  ut._HTML.audio_setGain = function (sourceNode, volume) {
    sourceNode.gainNode.gain.value = volume;
  };
  ut._HTML.audio_setPan = function (sourceNode, pan) {
    sourceNode.panNode.setPosition(pan, 0, 1 - Math.abs(pan));
  };
  ut._HTML.unlock = function () {
    if (!self.audioContext || self.unlockState == 2) return;
    function unlocked() {
      self.unlockState = 2;
      delete self.unlockBuffer;
      document.removeEventListener("click", ut._HTML.unlock, true);
      document.removeEventListener("touchstart", ut._HTML.unlock, true);
      document.removeEventListener("touchend", ut._HTML.unlock, true);
      document.removeEventListener("keydown", ut._HTML.unlock, true);
      document.removeEventListener("keyup", ut._HTML.unlock, true);
    }
    if (self.audioContext.state === "running") {
      unlocked();
      return;
    }
    var now = performance.now();
    if (self.lastUnlockAttempted && now - self.lastUnlockAttempted < 500)
      return;
    self.lastUnlockAttempted = now;
    if (self.audioContext.resume) self.audioContext.resume();
    if (!self.unlockBuffer) {
      self.unlockBuffer = self.audioContext.createBuffer(1, 1, 22050);
    }
    var source = self.audioContext.createBufferSource();
    source.buffer = self.unlockBuffer;
    source.connect(self.audioContext.destination);
    if (typeof source.start === "undefined") {
      source.noteOn(0);
    } else {
      source.start(0);
    }
    if (self.audioContext.resume) self.audioContext.resume();
    source.onended = function () {
      source.disconnect(0);
      unlocked();
    };
  };
  if (!window.AudioContext && !window.webkitAudioContext) return false;
  var audioContext = new (window.AudioContext || window.webkitAudioContext)();
  if (!audioContext) return false;
  audioContext.listener.setPosition(0, 0, 0);
  this.audioContext = audioContext;
  this.audioBuffers = {};
  this.audioSources = {};
  this.unlockState = 0;
  var navigator =
    typeof window !== "undefined" && window.navigator ? window.navigator : null;
  var isMobile = /iPhone|iPad|iPod|Android|BlackBerry|BB10|Silk|Mobi/i.test(
    navigator && navigator.userAgent
  );
  var isTouch = !!(
    isMobile ||
    (navigator && navigator.maxTouchPoints > 0) ||
    (navigator && navigator.msMaxTouchPoints > 0)
  );
  if (this.audioContext.state !== "running" || isMobile || isTouch) {
    ut._HTML.unlock();
  } else {
    this.unlockState = 2;
  }
  document.addEventListener(
    "visibilitychange",
    function () {
      if (document.visibilityState === "visible" && audioContext.resume)
        audioContext.resume();
      else if (document.visibilityState !== "visible" && audioContext.suspend)
        audioContext.suspend();
    },
    true
  );
  return true;
}
function _js_html_initImageLoading() {
  ut = ut || {};
  ut._HTML = ut._HTML || {};
  ut._HTML.images = [null];
  ut._HTML.tintedSprites = [null];
  ut._HTML.tintedSpritesFreeList = [];
  ut._HTML.initImage = function (idx) {
    ut._HTML.images[idx] = {
      image: null,
      mask: null,
      loaderror: false,
      hasAlpha: true,
      glTexture: null,
      glDisableSmoothing: false,
    };
  };
  ut._HTML.ensureImageIsReadable = function (idx, w, h) {
    if (ut._HTML.canvasMode == "webgl2" || ut._HTML.canvasMode == "webgl") {
      var gl = ut._HTML.canvasContext;
      if (ut._HTML.images[idx].isrt) {
        if (!ut._HTML.images[idx].glTexture) return false;
        var pixels = new Uint8Array(w * h * 4);
        var fbo = gl.createFramebuffer();
        gl.bindFramebuffer(gl.FRAMEBUFFER, fbo);
        gl.framebufferTexture2D(
          gl.FRAMEBUFFER,
          gl.COLOR_ATTACHMENT0,
          gl.TEXTURE_2D,
          ut._HTML.images[idx].glTexture,
          0
        );
        gl.viewport(0, 0, w, h);
        if (
          gl.checkFramebufferStatus(gl.FRAMEBUFFER) == gl.FRAMEBUFFER_COMPLETE
        ) {
          gl.readPixels(0, 0, w, h, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
        } else {
          console.log("Warning, can not read back from WebGL framebuffer.");
          gl.bindFramebuffer(gl.FRAMEBUFFER, null);
          gl.deleteFramebuffer(fbo);
          return false;
        }
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        gl.deleteFramebuffer(fbo);
        var canvas = document.createElement("canvas");
        canvas.width = w;
        canvas.height = h;
        var cx = canvas.getContext("2d");
        var imd = cx.createImageData(w, h);
        imd.data.set(pixels);
        cx.putImageData(imd, 0, 0);
        ut._HTML.images[idx].image = canvas;
        return true;
      }
    }
    if (ut._HTML.images[idx].isrt)
      return (
        ut._HTML.images[idx].image &&
        ut._HTML.images[idx].width == w &&
        ut._HTML.images[idx].height == h
      );
    else
      return (
        ut._HTML.images[idx].image &&
        ut._HTML.images[idx].image.naturalWidth === w &&
        ut._HTML.images[idx].image.naturalHeight === h
      );
  };
  ut._HTML.readyCanvasForReadback = function (idx, w, h) {
    if (!ut._HTML.ensureImageIsReadable(idx, w, h)) return null;
    if (ut._HTML.images[idx].image instanceof HTMLCanvasElement) {
      return ut._HTML.images[idx].image;
    } else {
      var cvs = document.createElement("canvas");
      cvs.width = w;
      cvs.height = h;
      var cx = cvs.getContext("2d");
      var srcimg = ut._HTML.images[idx].image;
      cx.globalCompositeOperation = "copy";
      cx.drawImage(srcimg, 0, 0, w, h);
      return cvs;
    }
  };
  ut._HTML.loadWebPFallback = function (url, idx) {
    function decode_base64(base64) {
      var size = base64.length;
      while (base64.charCodeAt(size - 1) == 61) size--;
      var data = new Uint8Array((size * 3) >> 2);
      for (
        var c, cPrev = 0, s = 6, d = 0, b = 0;
        b < size;
        cPrev = c, s = (s + 2) & 7
      ) {
        c = base64.charCodeAt(b++);
        c =
          c >= 97
            ? c - 71
            : c >= 65
            ? c - 65
            : c >= 48
            ? c + 4
            : c == 47
            ? 63
            : 62;
        if (s < 6) data[d++] = (cPrev << (2 + s)) | (c >> (4 - s));
      }
      return data;
    }
    if (!url) return false;
    if (!(typeof WebPDecoder == "object")) return false;
    if (WebPDecoder.nativeSupport) return false;
    var webpCanvas;
    var webpPrefix = "data:image/webp;base64,";
    if (!url.lastIndexOf(webpPrefix, 0)) {
      webpCanvas = document.createElement("canvas");
      WebPDecoder.decode(
        decode_base64(url.substring(webpPrefix.length)),
        webpCanvas
      );
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
}
function _js_html_loadImage(colorName, maskName) {
  colorName = colorName ? UTF8ToString(colorName) : null;
  maskName = maskName ? UTF8ToString(maskName) : null;
  colorName += ".png";
  if (colorName == "::white1x1") {
    colorName =
      "data:image/gif;base64,R0lGODlhAQABAIAAAP7//wAAACH5BAAAAAAALAAAAAABAAEAAAICRAEAOw==";
  } else if (colorName && colorName.substring(0, 9) == "ut-asset:") {
    colorName = UT_ASSETS[colorName.substring(9)];
  }
  if (maskName && maskName.substring(0, 9) == "ut-asset:") {
    maskName = UT_ASSETS[maskName.substring(9)];
  }
  var idx;
  for (var i = 1; i <= ut._HTML.images.length; i++) {
    if (!ut._HTML.images[i]) {
      idx = i;
      break;
    }
  }
  ut._HTML.initImage(idx);
  if (ut._HTML.loadWebPFallback(colorName, idx)) return idx;
  if (colorName) {
    var imgColor = new Image();
    var isjpg = !!colorName.match(/\.jpe?g$/i);
    ut._HTML.images[idx].image = imgColor;
    ut._HTML.images[idx].hasAlpha = !isjpg;
    imgColor.onerror = function () {
      ut._HTML.images[idx].loaderror = true;
    };
    imgColor.src = colorName;
  }
  if (maskName) {
    var imgMask = new Image();
    ut._HTML.images[idx].mask = imgMask;
    ut._HTML.images[idx].hasAlpha = true;
    imgMask.onerror = function () {
      ut._HTML.images[idx].loaderror = true;
    };
    imgMask.src = maskName;
  }
  return idx;
}
function _js_html_setCanvasSize(width, height, fbwidth, fbheight) {
  if (!width > 0 || !height > 0) throw "Bad canvas size at init.";
  var canvas = ut._HTML.canvasElement;
  if (!canvas) {
    canvas = document.getElementById("UT_CANVAS");
  }
  if (!canvas) {
    canvas = document.createElement("canvas");
    canvas.setAttribute("id", "UT_CANVAS");
    canvas.setAttribute("tabindex", "1");
    canvas.style.touchAction = "none";
    if (document.body) {
      document.body.style.margin = "0px";
      document.body.style.border = "0";
      document.body.style.overflow = "hidden";
      document.body.style.display = "block";
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
  ut._HTML.canvasMode = "bgfx";
  if (!canvas.tiny_initialized) {
    canvas.addEventListener(
      "webglcontextlost",
      function (event) {
        event.preventDefault();
      },
      false
    );
    canvas.focus();
    canvas.tiny_initialized = true;
  }
  if (!window.tiny_initialized) {
    window.addEventListener("focus", function (event) {
      ut._HTML.focus = true;
    });
    window.addEventListener("blur", function (event) {
      ut._HTML.focus = false;
    });
    window.tiny_initialized = true;
  }
  return true;
}
function _js_html_validateWebGLContextFeatures(requireSrgb) {
  if (
    requireSrgb &&
    GL.currentContext.version == 1 &&
    !GLctx.getExtension("EXT_sRGB")
  ) {
    // fatal(
    //   "WebGL implementation in current browser does not support sRGB rendering (No EXT_sRGB or WebGL 2), but sRGB is required by this page!"
    // );
  }
}
function _js_inputGetCanvasLost() {
  var inp = ut._HTML.input;
  var canvas = ut._HTML.canvasElement;
  return canvas != inp.canvas;
}
function _js_inputGetFocusLost() {
  var inp = ut._HTML.input;
  if (inp.focusLost) {
    inp.focusLost = false;
    return true;
  }
  return false;
}
function _js_inputGetKeyStream(maxLen, destPtr) {
  var inp = ut._HTML.input;
  return inp.getStream(inp.keyStream, maxLen, destPtr);
}
function _js_inputGetMouseStream(maxLen, destPtr) {
  var inp = ut._HTML.input;
  return inp.getStream(inp.mouseStream, maxLen, destPtr);
}
function _js_inputGetTouchStream(maxLen, destPtr) {
  var inp = ut._HTML.input;
  return inp.getStream(inp.touchStream, maxLen, destPtr);
}
function _js_inputGetWheelStream(maxLen, destPtr) {
  var inp = ut._HTML.input;
  return inp.getStream(inp.wheelStream, maxLen, destPtr);
}
function _js_inputInit() {
  ut._HTML = ut._HTML || {};
  ut._HTML.input = {};
  var inp = ut._HTML.input;
  var canvas = ut._HTML.canvasElement;
  if (!canvas) return false;
  canvas.requestPointerLock =
    canvas.requestPointerLock || canvas.mozRequestPointerLock;
  // document.exitPointerLock =
  //   document.exitPointerLock || document.mozExitPointerLock;
  function getPixelRatio() {
    var rect = inp.canvas.getBoundingClientRect();
    return inp.canvas.width / rect.width;
  }
  inp.getStream = function (stream, maxLen, destPtr) {
    destPtr >>= 2;
    var l = stream.length;
    if (l > maxLen) l = maxLen;
    for (var i = 0; i < l; i++) HEAP32[destPtr + i] = stream[i];
    return l;
  };
  inp.updateCursor = function () {
    if (ut.inpActiveMouseMode == ut.inpSavedMouseMode) return;
    var canvas = ut._HTML.canvasElement;
    var hasPointerLock =
      document.pointerLockElement === canvas ||
      document.mozPointerLockElement === canvas;
    if (ut.inpSavedMouseMode == 0) {
      document.body.style.cursor = "auto";
      if (hasPointerLock) document.exitPointerLock();
      ut.inpActiveMouseMode = 0;
    } else if (ut.inpSavedMouseMode == 1) {
      document.body.style.cursor = "none";
      if (hasPointerLock) document.exitPointerLock();
      ut.inpActiveMouseMode = 1;
    } else {
      canvas.requestPointerLock();
    }
  };
  inp.mouseEventFn = function (ev) {
    if (ut.inpSavedMouseMode != ut.inpActiveMouseMode) return;
    var inp = ut._HTML.input;
    var eventType;
    var buttons = 0;
    if (ev.type == "mouseup") {
      eventType = 0;
      buttons = ev.button;
    } else if (ev.type == "mousedown") {
      eventType = 1;
      buttons = ev.button;
    } else if (ev.type == "mousemove") {
      eventType = 2;
    } else return;
    var pixelRatio = getPixelRatio();
    var x = Math.round(ev.clientX * pixelRatio) | 0;
    var y =
      Math.round((ev.target.clientHeight - 1 - ev.clientY) * pixelRatio) | 0;
    var dx = Math.round(ev.movementX * pixelRatio) | 0;
    var dy = Math.round(ev.movementY * pixelRatio) | 0;
    inp.mouseStream.push(eventType | 0);
    inp.mouseStream.push(buttons | 0);
    inp.mouseStream.push(x);
    inp.mouseStream.push(y);
    inp.mouseStream.push(dx);
    inp.mouseStream.push(dy);
    ev.preventDefault();
    ev.stopPropagation();
  };
  var normalizeWheelDelta = (function () {
    var distribution = [];
    var done = null;
    var scale = 1;
    return function (n) {
      if (n == 0) return n;
      if (done !== null) return n * done;
      var abs = Math.abs(n);
      outer: do {
        for (var i = 0; i < distribution.length; ++i) {
          if (abs <= distribution[i]) {
            distribution.splice(i, 0, abs);
            break outer;
          }
        }
        distribution.push(abs);
      } while (false);
      var factor = scale / distribution[Math.floor(distribution.length / 3)];
      if (distribution.length == 500) done = factor;
      return n * factor;
    };
  })();
  inp.wheelEventFn = function (ev) {
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
    inp.wheelStream.push(dx | 0);
    inp.wheelStream.push(dy | 0);
    ev.preventDefault();
    ev.stopPropagation();
  };
  inp.touchEventFn = function (ev) {
    var inp = ut._HTML.input;
    var eventType,
      x,
      y,
      touches = ev.changedTouches;
    if (ev.type == "touchstart") eventType = 1;
    else if (ev.type == "touchend") eventType = 0;
    else if (ev.type == "touchcancel") eventType = 3;
    else eventType = 2;
    var pixelRatio = getPixelRatio();
    for (var i = 0; i < touches.length; ++i) {
      var t = touches[i];
      x = Math.round(t.clientX * pixelRatio) | 0;
      y = Math.round((ev.target.clientHeight - 1 - t.clientY) * pixelRatio) | 0;
      inp.touchStream.push(eventType | 0);
      inp.touchStream.push(t.identifier | 0);
      inp.touchStream.push(x);
      inp.touchStream.push(y);
    }
    ev.preventDefault();
    ev.stopPropagation();
  };
  inp.keyEventFn = function (ev) {
    var eventType;
    if (ev.type == "keydown") eventType = 1;
    else if (ev.type == "keyup") eventType = 0;
    else return;
    inp.keyStream.push(eventType | 0);
    inp.keyStream.push(ev.keyCode | 0);
  };
  inp.clickEventFn = function () {
    this.focus();
    inp.updateCursor();
  };
  inp.focusoutEventFn = function () {
    var inp = ut._HTML.input;
    inp.focusLost = true;
    ut.inpActiveMouseMode = 0;
  };
  inp.cursorLockChangeFn = function () {
    var canvas = ut._HTML.canvasElement;
    if (
      document.pointerLockElement === canvas ||
      document.mozPointerLockElement === canvas
    ) {
      ut.inpActiveMouseMode = 2;
    } else {
      if (ut.inpActiveMouseMode === 2) ut.inpActiveMouseMode = 0;
    }
  };
  inp.mouseStream = [];
  inp.wheelStream = [];
  inp.keyStream = [];
  inp.touchStream = [];
  inp.canvas = canvas;
  inp.focusLost = false;
  ut.inpSavedMouseMode = ut.inpSavedMouseMode || 0;
  ut.inpActiveMouseMode = ut.inpActiveMouseMode || 0;
  var events = {};
  events["keydown"] = inp.keyEventFn;
  events["keyup"] = inp.keyEventFn;
  events["touchstart"] = events["touchend"] = events["touchmove"] = events[
    "touchcancel"
  ] = inp.touchEventFn;
  events["mousedown"] = events["mouseup"] = events["mousemove"] =
    inp.mouseEventFn;
  events["wheel"] = inp.wheelEventFn;
  events["focusout"] = inp.focusoutEventFn;
  events["click"] = inp.clickEventFn;
  for (var ev in events) canvas.addEventListener(ev, events[ev]);
  document.addEventListener("pointerlockchange", inp.cursorLockChangeFn);
  document.addEventListener("mozpointerlockchange", inp.cursorLockChangeFn);
  document.addEventListener("visibilitychange", inp.focusoutEventFn);
  return true;
}
function _js_inputResetStreams(maxLen, destPtr) {
  var inp = ut._HTML.input;
  inp.mouseStream.length = 0;
  inp.wheelStream.length = 0;
  inp.keyStream.length = 0;
  inp.touchStream.length = 0;
}
function _usleep(useconds) {
  var start = _emscripten_get_now();
  while (_emscripten_get_now() - start < useconds / 1e3) {}
}
function _nanosleep(rqtp, rmtp) {
  if (rqtp === 0) {
    ___setErrNo(28);
    return -1;
  }
  var seconds = HEAP32[rqtp >> 2];
  var nanoseconds = HEAP32[(rqtp + 4) >> 2];
  if (nanoseconds < 0 || nanoseconds > 999999999 || seconds < 0) {
    ___setErrNo(28);
    return -1;
  }
  if (rmtp !== 0) {
    HEAP32[rmtp >> 2] = 0;
    HEAP32[(rmtp + 4) >> 2] = 0;
  }
  return _usleep(seconds * 1e6 + nanoseconds / 1e3);
}
function _setTempRet0($i) {
  setTempRet0($i | 0);
}
function readAsmConstArgs(sigPtr, buf) {
  if (!readAsmConstArgs.array) {
    readAsmConstArgs.array = [];
  }
  var args = readAsmConstArgs.array;
  args.length = 0;
  var ch;
  while ((ch = HEAPU8[sigPtr++])) {
    if (ch === 100 || ch === 102) {
      buf = (buf + 7) & ~7;
      args.push(HEAPF64[buf >> 3]);
      buf += 8;
    } else {
      buf = (buf + 3) & ~3;
      args.push(HEAP32[buf >> 2]);
      buf += 4;
    }
  }
  return args;
}
var GLctx;
GL.init();
for (var i = 0; i < 32; i++) __tempFixedLengthArray.push(new Array(i));
Fetch.staticInit();
var ut;
var asmLibraryArg = {
  Eg: ___lock,
  oa: ___syscall221,
  Ag: ___syscall4,
  Fg: ___syscall5,
  Cg: ___syscall54,
  Q: ___unlock,
  vg: __emscripten_fetch_free,
  b: _abort,
  i: _clock,
  hc: _clock_getres,
  Yb: _clock_gettime,
  Ua: _emscripten_asm_const_iii,
  v: _emscripten_get_now,
  $f: _emscripten_glActiveTexture,
  Zf: _emscripten_glAttachShader,
  Rc: _emscripten_glBeginQuery,
  qg: _emscripten_glBeginQueryEXT,
  xc: _emscripten_glBeginTransformFeedback,
  Yf: _emscripten_glBindAttribLocation,
  Xf: _emscripten_glBindBuffer,
  uc: _emscripten_glBindBufferBase,
  vc: _emscripten_glBindBufferRange,
  Wf: _emscripten_glBindFramebuffer,
  Vf: _emscripten_glBindRenderbuffer,
  zb: _emscripten_glBindSampler,
  Uf: _emscripten_glBindTexture,
  qb: _emscripten_glBindTransformFeedback,
  Cc: _emscripten_glBindVertexArray,
  hg: _emscripten_glBindVertexArrayOES,
  Tf: _emscripten_glBlendColor,
  Sf: _emscripten_glBlendEquation,
  Rf: _emscripten_glBlendEquationSeparate,
  Qf: _emscripten_glBlendFunc,
  Pf: _emscripten_glBlendFuncSeparate,
  Fc: _emscripten_glBlitFramebuffer,
  Of: _emscripten_glBufferData,
  Nf: _emscripten_glBufferSubData,
  Mf: _emscripten_glCheckFramebufferStatus,
  Lf: _emscripten_glClear,
  Xb: _emscripten_glClearBufferfi,
  Zb: _emscripten_glClearBufferfv,
  $b: _emscripten_glClearBufferiv,
  _b: _emscripten_glClearBufferuiv,
  Kf: _emscripten_glClearColor,
  Jf: _emscripten_glClearDepthf,
  If: _emscripten_glClearStencil,
  Ib: _emscripten_glClientWaitSync,
  Hf: _emscripten_glColorMask,
  Gf: _emscripten_glCompileShader,
  Ef: _emscripten_glCompressedTexImage2D,
  Wc: _emscripten_glCompressedTexImage3D,
  Df: _emscripten_glCompressedTexSubImage2D,
  Vc: _emscripten_glCompressedTexSubImage3D,
  Vb: _emscripten_glCopyBufferSubData,
  Cf: _emscripten_glCopyTexImage2D,
  Bf: _emscripten_glCopyTexSubImage2D,
  Yc: _emscripten_glCopyTexSubImage3D,
  Af: _emscripten_glCreateProgram,
  zf: _emscripten_glCreateShader,
  yf: _emscripten_glCullFace,
  xf: _emscripten_glDeleteBuffers,
  wf: _emscripten_glDeleteFramebuffers,
  vf: _emscripten_glDeleteProgram,
  Tc: _emscripten_glDeleteQueries,
  sg: _emscripten_glDeleteQueriesEXT,
  sf: _emscripten_glDeleteRenderbuffers,
  Bb: _emscripten_glDeleteSamplers,
  rf: _emscripten_glDeleteShader,
  Jb: _emscripten_glDeleteSync,
  qf: _emscripten_glDeleteTextures,
  pb: _emscripten_glDeleteTransformFeedbacks,
  Bc: _emscripten_glDeleteVertexArrays,
  gg: _emscripten_glDeleteVertexArraysOES,
  pf: _emscripten_glDepthFunc,
  of: _emscripten_glDepthMask,
  nf: _emscripten_glDepthRangef,
  mf: _emscripten_glDetachShader,
  lf: _emscripten_glDisable,
  kf: _emscripten_glDisableVertexAttribArray,
  jf: _emscripten_glDrawArrays,
  Ob: _emscripten_glDrawArraysInstanced,
  cg: _emscripten_glDrawArraysInstancedANGLE,
  _a: _emscripten_glDrawArraysInstancedARB,
  dd: _emscripten_glDrawArraysInstancedEXT,
  $a: _emscripten_glDrawArraysInstancedNV,
  Nc: _emscripten_glDrawBuffers,
  bd: _emscripten_glDrawBuffersEXT,
  dg: _emscripten_glDrawBuffersWEBGL,
  gf: _emscripten_glDrawElements,
  Mb: _emscripten_glDrawElementsInstanced,
  bg: _emscripten_glDrawElementsInstancedANGLE,
  Ya: _emscripten_glDrawElementsInstancedARB,
  Za: _emscripten_glDrawElementsInstancedEXT,
  cd: _emscripten_glDrawElementsInstancedNV,
  $c: _emscripten_glDrawRangeElements,
  ff: _emscripten_glEnable,
  ef: _emscripten_glEnableVertexAttribArray,
  Qc: _emscripten_glEndQuery,
  pg: _emscripten_glEndQueryEXT,
  wc: _emscripten_glEndTransformFeedback,
  Lb: _emscripten_glFenceSync,
  df: _emscripten_glFinish,
  cf: _emscripten_glFlush,
  bf: _emscripten_glFramebufferRenderbuffer,
  af: _emscripten_glFramebufferTexture2D,
  Dc: _emscripten_glFramebufferTextureLayer,
  $e: _emscripten_glFrontFace,
  _e: _emscripten_glGenBuffers,
  Xe: _emscripten_glGenFramebuffers,
  Uc: _emscripten_glGenQueries,
  tg: _emscripten_glGenQueriesEXT,
  We: _emscripten_glGenRenderbuffers,
  Cb: _emscripten_glGenSamplers,
  Ve: _emscripten_glGenTextures,
  ob: _emscripten_glGenTransformFeedbacks,
  Ac: _emscripten_glGenVertexArrays,
  fg: _emscripten_glGenVertexArraysOES,
  Ze: _emscripten_glGenerateMipmap,
  Ue: _emscripten_glGetActiveAttrib,
  Te: _emscripten_glGetActiveUniform,
  Qb: _emscripten_glGetActiveUniformBlockName,
  Rb: _emscripten_glGetActiveUniformBlockiv,
  Tb: _emscripten_glGetActiveUniformsiv,
  Se: _emscripten_glGetAttachedShaders,
  Re: _emscripten_glGetAttribLocation,
  Qe: _emscripten_glGetBooleanv,
  Db: _emscripten_glGetBufferParameteri64v,
  Pe: _emscripten_glGetBufferParameteriv,
  Oe: _emscripten_glGetError,
  Me: _emscripten_glGetFloatv,
  jc: _emscripten_glGetFragDataLocation,
  Le: _emscripten_glGetFramebufferAttachmentParameteriv,
  Eb: _emscripten_glGetInteger64i_v,
  Gb: _emscripten_glGetInteger64v,
  yc: _emscripten_glGetIntegeri_v,
  Ke: _emscripten_glGetIntegerv,
  cb: _emscripten_glGetInternalformativ,
  kb: _emscripten_glGetProgramBinary,
  Ie: _emscripten_glGetProgramInfoLog,
  Je: _emscripten_glGetProgramiv,
  kg: _emscripten_glGetQueryObjecti64vEXT,
  mg: _emscripten_glGetQueryObjectivEXT,
  ig: _emscripten_glGetQueryObjectui64vEXT,
  Oc: _emscripten_glGetQueryObjectuiv,
  lg: _emscripten_glGetQueryObjectuivEXT,
  Pc: _emscripten_glGetQueryiv,
  ng: _emscripten_glGetQueryivEXT,
  He: _emscripten_glGetRenderbufferParameteriv,
  tb: _emscripten_glGetSamplerParameterfv,
  ub: _emscripten_glGetSamplerParameteriv,
  Fe: _emscripten_glGetShaderInfoLog,
  Ee: _emscripten_glGetShaderPrecisionFormat,
  De: _emscripten_glGetShaderSource,
  Ge: _emscripten_glGetShaderiv,
  Be: _emscripten_glGetString,
  Wb: _emscripten_glGetStringi,
  Fb: _emscripten_glGetSynciv,
  Ae: _emscripten_glGetTexParameterfv,
  ze: _emscripten_glGetTexParameteriv,
  sc: _emscripten_glGetTransformFeedbackVarying,
  Sb: _emscripten_glGetUniformBlockIndex,
  Ub: _emscripten_glGetUniformIndices,
  we: _emscripten_glGetUniformLocation,
  ye: _emscripten_glGetUniformfv,
  xe: _emscripten_glGetUniformiv,
  kc: _emscripten_glGetUniformuiv,
  qc: _emscripten_glGetVertexAttribIiv,
  pc: _emscripten_glGetVertexAttribIuiv,
  te: _emscripten_glGetVertexAttribPointerv,
  ve: _emscripten_glGetVertexAttribfv,
  ue: _emscripten_glGetVertexAttribiv,
  se: _emscripten_glHint,
  gb: _emscripten_glInvalidateFramebuffer,
  fb: _emscripten_glInvalidateSubFramebuffer,
  qe: _emscripten_glIsBuffer,
  pe: _emscripten_glIsEnabled,
  oe: _emscripten_glIsFramebuffer,
  ne: _emscripten_glIsProgram,
  Sc: _emscripten_glIsQuery,
  rg: _emscripten_glIsQueryEXT,
  me: _emscripten_glIsRenderbuffer,
  Ab: _emscripten_glIsSampler,
  le: _emscripten_glIsShader,
  Kb: _emscripten_glIsSync,
  ke: _emscripten_glIsTexture,
  nb: _emscripten_glIsTransformFeedback,
  zc: _emscripten_glIsVertexArray,
  eg: _emscripten_glIsVertexArrayOES,
  je: _emscripten_glLineWidth,
  ie: _emscripten_glLinkProgram,
  mb: _emscripten_glPauseTransformFeedback,
  he: _emscripten_glPixelStorei,
  fe: _emscripten_glPolygonOffset,
  jb: _emscripten_glProgramBinary,
  ib: _emscripten_glProgramParameteri,
  og: _emscripten_glQueryCounterEXT,
  ad: _emscripten_glReadBuffer,
  ee: _emscripten_glReadPixels,
  de: _emscripten_glReleaseShaderCompiler,
  ce: _emscripten_glRenderbufferStorage,
  Ec: _emscripten_glRenderbufferStorageMultisample,
  lb: _emscripten_glResumeTransformFeedback,
  be: _emscripten_glSampleCoverage,
  wb: _emscripten_glSamplerParameterf,
  vb: _emscripten_glSamplerParameterfv,
  yb: _emscripten_glSamplerParameteri,
  xb: _emscripten_glSamplerParameteriv,
  ae: _emscripten_glScissor,
  $d: _emscripten_glShaderBinary,
  _d: _emscripten_glShaderSource,
  Zd: _emscripten_glStencilFunc,
  Yd: _emscripten_glStencilFuncSeparate,
  Wd: _emscripten_glStencilMask,
  Vd: _emscripten_glStencilMaskSeparate,
  Ud: _emscripten_glStencilOp,
  Td: _emscripten_glStencilOpSeparate,
  Sd: _emscripten_glTexImage2D,
  _c: _emscripten_glTexImage3D,
  Rd: _emscripten_glTexParameterf,
  Qd: _emscripten_glTexParameterfv,
  Pd: _emscripten_glTexParameteri,
  Od: _emscripten_glTexParameteriv,
  eb: _emscripten_glTexStorage2D,
  db: _emscripten_glTexStorage3D,
  Nd: _emscripten_glTexSubImage2D,
  Zc: _emscripten_glTexSubImage3D,
  tc: _emscripten_glTransformFeedbackVaryings,
  Md: _emscripten_glUniform1f,
  Ld: _emscripten_glUniform1fv,
  Kd: _emscripten_glUniform1i,
  Jd: _emscripten_glUniform1iv,
  ic: _emscripten_glUniform1ui,
  dc: _emscripten_glUniform1uiv,
  Id: _emscripten_glUniform2f,
  Hd: _emscripten_glUniform2fv,
  Gd: _emscripten_glUniform2i,
  Fd: _emscripten_glUniform2iv,
  gc: _emscripten_glUniform2ui,
  cc: _emscripten_glUniform2uiv,
  Ed: _emscripten_glUniform3f,
  Dd: _emscripten_glUniform3fv,
  Bd: _emscripten_glUniform3i,
  Ad: _emscripten_glUniform3iv,
  fc: _emscripten_glUniform3ui,
  bc: _emscripten_glUniform3uiv,
  zd: _emscripten_glUniform4f,
  yd: _emscripten_glUniform4fv,
  xd: _emscripten_glUniform4i,
  wd: _emscripten_glUniform4iv,
  ec: _emscripten_glUniform4ui,
  ac: _emscripten_glUniform4uiv,
  Pb: _emscripten_glUniformBlockBinding,
  vd: _emscripten_glUniformMatrix2fv,
  Lc: _emscripten_glUniformMatrix2x3fv,
  Jc: _emscripten_glUniformMatrix2x4fv,
  ud: _emscripten_glUniformMatrix3fv,
  Kc: _emscripten_glUniformMatrix3x2fv,
  Hc: _emscripten_glUniformMatrix3x4fv,
  td: _emscripten_glUniformMatrix4fv,
  Ic: _emscripten_glUniformMatrix4x2fv,
  Gc: _emscripten_glUniformMatrix4x3fv,
  sd: _emscripten_glUseProgram,
  qd: _emscripten_glValidateProgram,
  pd: _emscripten_glVertexAttrib1f,
  od: _emscripten_glVertexAttrib1fv,
  nd: _emscripten_glVertexAttrib2f,
  md: _emscripten_glVertexAttrib2fv,
  ld: _emscripten_glVertexAttrib3f,
  kd: _emscripten_glVertexAttrib3fv,
  jd: _emscripten_glVertexAttrib4f,
  id: _emscripten_glVertexAttrib4fv,
  rb: _emscripten_glVertexAttribDivisor,
  ag: _emscripten_glVertexAttribDivisorANGLE,
  ab: _emscripten_glVertexAttribDivisorARB,
  ed: _emscripten_glVertexAttribDivisorEXT,
  bb: _emscripten_glVertexAttribDivisorNV,
  oc: _emscripten_glVertexAttribI4i,
  mc: _emscripten_glVertexAttribI4iv,
  nc: _emscripten_glVertexAttribI4ui,
  lc: _emscripten_glVertexAttribI4uiv,
  rc: _emscripten_glVertexAttribIPointer,
  hd: _emscripten_glVertexAttribPointer,
  fd: _emscripten_glViewport,
  Hb: _emscripten_glWaitSync,
  xg: _emscripten_is_main_browser_thread,
  N: _emscripten_log,
  Wa: _emscripten_memcpy_big,
  Ig: _emscripten_performance_now,
  Hg: _emscripten_request_animation_frame_loop,
  Xa: _emscripten_resize_heap,
  ia: _emscripten_set_canvas_element_size,
  wg: _emscripten_start_fetch,
  Gg: _emscripten_throw_string,
  ha: _emscripten_webgl_create_context,
  ga: _emscripten_webgl_destroy_context,
  g: _emscripten_webgl_enable_extension,
  I: _emscripten_webgl_get_context_attributes,
  J: _emscripten_webgl_get_current_context,
  Ta: _emscripten_webgl_init_context_attributes,
  j: _emscripten_webgl_make_context_current,
  yg: _environ_get,
  zg: _environ_sizes_get,
  ka: _exit,
  na: _fd_close,
  Bg: _fd_read,
  Va: _fd_seek,
  ma: _fd_write,
  Nb: _gettimeofday,
  Y: _glActiveTexture,
  ba: _glAttachShader,
  c: _glBindBuffer,
  d: _glBindFramebuffer,
  z: _glBindRenderbuffer,
  h: _glBindTexture,
  Sg: _glBlendColor,
  Tg: _glBlendEquationSeparate,
  Ug: _glBlendFuncSeparate,
  s: _glBufferData,
  r: _glBufferSubData,
  V: _glCheckFramebufferStatus,
  L: _glClear,
  M: _glClearColor,
  Pg: _glClearDepthf,
  Og: _glClearStencil,
  F: _glColorMask,
  Aa: _glCompileShader,
  fa: _glCompressedTexImage2D,
  G: _glCompressedTexSubImage2D,
  Pa: _glCreateProgram,
  Ca: _glCreateShader,
  sa: _glCullFace,
  B: _glDeleteBuffers,
  H: _glDeleteFramebuffers,
  aa: _glDeleteProgram,
  u: _glDeleteRenderbuffers,
  X: _glDeleteShader,
  D: _glDeleteTextures,
  w: _glDepthFunc,
  S: _glDepthMask,
  $: _glDetachShader,
  e: _glDisable,
  o: _glDisableVertexAttribArray,
  ra: _glDrawArrays,
  pa: _glDrawElements,
  f: _glEnable,
  K: _glEnableVertexAttribArray,
  Rg: _glFlush,
  x: _glFramebufferRenderbuffer,
  W: _glFramebufferTexture2D,
  U: _glFrontFace,
  t: _glGenBuffers,
  y: _glGenFramebuffers,
  Z: _glGenRenderbuffers,
  ca: _glGenTextures,
  Da: _glGenerateMipmap,
  La: _glGetActiveAttrib,
  Ka: _glGetActiveUniform,
  p: _glGetAttribLocation,
  a: _glGetError,
  Qa: _glGetFloatv,
  n: _glGetIntegerv,
  Ma: _glGetProgramInfoLog,
  q: _glGetProgramiv,
  ya: _glGetShaderInfoLog,
  za: _glGetShaderiv,
  m: _glGetString,
  Ja: _glGetUniformLocation,
  Oa: _glLinkProgram,
  A: _glPixelStorei,
  R: _glReadPixels,
  Ha: _glRenderbufferStorage,
  T: _glScissor,
  Ba: _glShaderSource,
  ua: _glStencilFuncSeparate,
  ta: _glStencilOpSeparate,
  da: _glTexImage2D,
  Fa: _glTexParameterf,
  Ga: _glTexParameterfv,
  l: _glTexParameteri,
  ea: _glTexSubImage2D,
  qa: _glUniform1i,
  Ng: _glUniform1iv,
  Mg: _glUniform4f,
  E: _glUniform4fv,
  Lg: _glUniformMatrix3fv,
  k: _glUniformMatrix4fv,
  C: _glUseProgram,
  _: _glVertexAttribPointer,
  va: _glViewport,
  ug: _js_html_audioCheckLoad,
  jg: _js_html_audioFree,
  Ye: _js_html_audioIsPlaying,
  wa: _js_html_audioIsUnlocked,
  Qg: _js_html_audioPause,
  _f: _js_html_audioPlay,
  Kg: _js_html_audioResume,
  tf: _js_html_audioSetPan,
  hf: _js_html_audioSetPitch,
  Ff: _js_html_audioSetVolume,
  Dg: _js_html_audioStartLoadFile,
  la: _js_html_audioStop,
  Vg: _js_html_audioUnlock,
  rd: _js_html_checkLoadImage,
  gd: _js_html_finishLoadImage,
  P: _js_html_freeImage,
  Ce: _js_html_getCanvasSize,
  Xd: _js_html_getDPIScale,
  re: _js_html_getFrameSize,
  ge: _js_html_getScreenSize,
  Mc: _js_html_imageToMemory,
  Jg: _js_html_init,
  Ea: _js_html_initAudio,
  Xc: _js_html_initImageLoading,
  Cd: _js_html_loadImage,
  Ne: _js_html_setCanvasSize,
  Wg: _js_html_validateWebGLContextFeatures,
  uf: _js_inputGetCanvasLost,
  sb: _js_inputGetFocusLost,
  Sa: _js_inputGetKeyStream,
  Ra: _js_inputGetMouseStream,
  Ia: _js_inputGetTouchStream,
  Na: _js_inputGetWheelStream,
  xa: _js_inputInit,
  O: _js_inputResetStreams,
  memory: wasmMemory,
  hb: _nanosleep,
  ja: _setTempRet0,
  table: wasmTable,
};
function run() {
  var ret = _main(0,0);
}
function initRuntime(asm) {
  asm["Xg"]();
}
var imports = { a: asmLibraryArg };
var _malloc,
  _free,
  _main,
  _BGFXCB_Init,
  _BGFXCB_Lock,
  _BGFXCB_UnlockAndClear,
  _init_html,
  _shutdown_html,
  _time_html,
  _RegisterSendMessage,
  _GetOrCreateSharedMemory,
  _unsafeutility_malloc,
  _unsafeutility_memcpy,
  _unsafeutility_assertheap,
  _unsafeutility_free,
  _unsafeutility_memset,
  _unsafeutility_memclear,
  _unsafeutility_memcpystride,
  _unsafeutility_memcmp,
  _unsafeutility_memcpyreplicate,
  _unsafeutility_memmove,
  _unsafeutility_freetemp,
  _unsafeutility_call_p,
  _unsafeutility_call_pi,
  _unsafeutility_get_last_free_ptr,
  _rafcallbackinit_html,
  _Decompress_LZ4,
  _GetStatus,
  _Close,
  _GetData,
  _RequestAsyncRead,
  _strlen,
  _BGFXCB_DeInit,
  _SendMessage,
  _Time_GetTicksMicrosecondsMonotonic,
  _CompressBound_LZ4,
  _Compress_LZ4,
  _GetErrorStatus,
  _htonl,
  _htons,
  _ntohs,
  __get_tzname,
  __get_daylight,
  __get_timezone,
  _memalign,
  _emscripten_builtin_free,
  _emscripten_builtin_memalign,
  stackSave,
  stackAlloc,
  stackRestore,
  __growWasmMemory,
  dynCall_viiiii,
  dynCall_vi,
  dynCall_viii,
  dynCall_viiiiii,
  dynCall_iii,
  dynCall_ii,
  dynCall_iiii,
  dynCall_iid,
  dynCall_iij,
  dynCall_iif,
  dynCall_vii,
  dynCall_iiiii,
  dynCall_viiiiiii,
  dynCall_iiiiiiiii,
  dynCall_viiii,
  dynCall_viiiiiiii,
  dynCall_iiif,
  dynCall_iiiiii,
  dynCall_viiiiiiiiii,
  dynCall_viiiiiiiii,
  dynCall_i,
  dynCall_vijii,
  dynCall_iijii,
  dynCall_v,
  dynCall_iiiiiii,
  dynCall_jiji,
  dynCall_iiiiji,
  dynCall_idi,
  dynCall_iidiiii,
  dynCall_vffff,
  dynCall_vf,
  dynCall_vff,
  dynCall_vfi,
  dynCall_viif,
  dynCall_vif,
  dynCall_viff,
  dynCall_vifff,
  dynCall_viffff,
  dynCall_viiiiiiiiiii,
  dynCall_viifi;
WebAssembly.instantiate('TinyRacing.wasm.txt', imports).then(function (output) {
  var asm = output.instance.exports;
  _malloc = asm["Yg"];
  _free = asm["Zg"];
  _main = asm["_g"];
  _BGFXCB_Init = asm["$g"];
  _BGFXCB_Lock = asm["ah"];
  _BGFXCB_UnlockAndClear = asm["bh"];
  _init_html = asm["ch"];
  _shutdown_html = asm["dh"];
  _time_html = asm["eh"];
  _RegisterSendMessage = asm["fh"];
  _GetOrCreateSharedMemory = asm["gh"];
  _unsafeutility_malloc = asm["hh"];
  _unsafeutility_memcpy = asm["ih"];
  _unsafeutility_assertheap = asm["jh"];
  _unsafeutility_free = asm["kh"];
  _unsafeutility_memset = asm["lh"];
  _unsafeutility_memclear = asm["mh"];
  _unsafeutility_memcpystride = asm["nh"];
  _unsafeutility_memcmp = asm["oh"];
  _unsafeutility_memcpyreplicate = asm["ph"];
  _unsafeutility_memmove = asm["qh"];
  _unsafeutility_freetemp = asm["rh"];
  _unsafeutility_call_p = asm["sh"];
  _unsafeutility_call_pi = asm["th"];
  _unsafeutility_get_last_free_ptr = asm["uh"];
  _rafcallbackinit_html = asm["vh"];
  _Decompress_LZ4 = asm["wh"];
  _GetStatus = asm["xh"];
  _Close = asm["yh"];
  _GetData = asm["zh"];
  _RequestAsyncRead = asm["Ah"];
  _strlen = asm["Bh"];
  _BGFXCB_DeInit = asm["Ch"];
  _SendMessage = asm["Dh"];
  _Time_GetTicksMicrosecondsMonotonic = asm["Eh"];
  _CompressBound_LZ4 = asm["Fh"];
  _Compress_LZ4 = asm["Gh"];
  _GetErrorStatus = asm["Hh"];
  _htonl = asm["Ih"];
  _htons = asm["Jh"];
  _ntohs = asm["Kh"];
  __get_tzname = asm["Lh"];
  __get_daylight = asm["Mh"];
  __get_timezone = asm["Nh"];
  _memalign = asm["Oh"];
  _emscripten_builtin_free = asm["Ph"];
  _emscripten_builtin_memalign = asm["Qh"];
  stackSave = asm["Rh"];
  stackAlloc = asm["Sh"];
  stackRestore = asm["Th"];
  __growWasmMemory = asm["Uh"];
  dynCall_viiiii = asm["Vh"];
  dynCall_viii = asm["Xh"];
  dynCall_viiiiii = asm["Yh"];
  dynCall_iii = asm["Zh"];
  dynCall_ii = asm["_h"];
  dynCall_iiii = asm["$h"];
  dynCall_iid = asm["ai"];
  dynCall_iij = asm["bi"];
  dynCall_iif = asm["ci"];
  dynCall_vii = asm["di"];
  dynCall_iiiii = asm["ei"];
  dynCall_viiiiiii = asm["fi"];
  dynCall_iiiiiiiii = asm["gi"];
  dynCall_viiii = asm["hi"];
  dynCall_viiiiiiii = asm["ii"];
  dynCall_iiif = asm["ji"];
  dynCall_iiiiii = asm["ki"];
  dynCall_viiiiiiiiii = asm["li"];
  dynCall_viiiiiiiii = asm["mi"];
  dynCall_i = asm["ni"];
  dynCall_vijii = asm["oi"];
  dynCall_iijii = asm["pi"];
  dynCall_v = asm["qi"];
  dynCall_iiiiiii = asm["ri"];
  dynCall_jiji = asm["si"];
  dynCall_iiiiji = asm["ti"];
  dynCall_idi = asm["ui"];
  dynCall_iidiiii = asm["vi"];
  dynCall_vffff = asm["wi"];
  dynCall_vf = asm["xi"];
  dynCall_vff = asm["yi"];
  dynCall_vfi = asm["zi"];
  dynCall_viif = asm["Ai"];
  dynCall_vif = asm["Bi"];
  dynCall_viff = asm["Ci"];
  dynCall_vifff = asm["Di"];
  dynCall_viffff = asm["Ei"];
  dynCall_viiiiiiiiiii = asm["Fi"];
  dynCall_viifi = asm["Gi"];
  initRuntime(asm);
  ready();
});
