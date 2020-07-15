#include <Unity/Runtime.h>

#include <emscripten.h>
#include <emscripten/html5.h>
#include <stdio.h>

#include "il2cpp-config.h"
#include "gc/GarbageCollector.h"

extern "C" {
    void js_html_init();
    bool js_html_setCanvasSize(int width, int height, bool webgl);
    void js_html_debugReadback(int w, int h, void *pixels);
}

DOTS_EXPORT(bool)
init_html()
{
    js_html_init();
    return true;
}

DOTS_EXPORT(void)
shutdown_html(int exitCode)
{
}

DOTS_EXPORT(double)
time_html()
{
    // TODO: If we want to target such old mobile browsers that they do not have performance.now() API,
    //       we should change the following call to emscripten_get_now() instead of emscripten_performance_now().
    //       They are the same otherwise, except that emscripten_performance_now() is much smaller code-size wise,
    //       since it does not emulate performance.now() via Date.now() (which emscripten_get_now() does)
    return emscripten_performance_now()*0.001;
}
