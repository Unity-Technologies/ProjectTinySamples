/* This file contains "externs" declarations for Google Closure compiler (JS code minifier) tool, which is used to minify output code when targeting asm.js/wasm builds.
Each external JavaScript variable identifier referenced from compiled code should appear here. "External" here means JS code/variables that are not defined in .c/cpp files,
--js-library files, nor --pre-js/--post-js files; but symbols whose definitions appear as part of being concatenated from external JS/HTML code after bee/Emscripten build
has finished.

If you need to add a symbol here, please document here clearly why the symbol needs to be declared extern, because using externs limits the amount of code size minification
that Closure can perform, and needing to use externs can suggest we have structured our build flow somehow poorly.
*/

// UT_ASSETS variable is declared by generated code from Unity editor, and it contains the list of assets compiled to the application.
// This variable should go away altogether and be removed from this list, when we have developed a more full-fledged asset system for Dots.
/**
 * @suppress {duplicate, undefinedVars}
 */
var UT_ASSETS;

// fatal is a function defined in tiny_shell.html, that file is outside Closure's minification boundary, so references there need to
// be declared as externs.
/**
 * @suppress {duplicate, undefinedVars}
 */
var fatal = function(msg) {};