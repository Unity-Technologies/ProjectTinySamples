/** @suppress{duplicate} This symbol is intended to be present multiple times in the source, the second definition overwrites the first to override default behavior. Closure deletes the first instance. */
function ready() {
	try {
		if (typeof ENVIRONMENT_IS_PTHREAD === 'undefined' || !ENVIRONMENT_IS_PTHREAD) run();
	} catch(e) {
		// Suppress the JS throw message that corresponds to Dots unwinding the call stack to run the application. 
		if (e !== 'unwind') throw e;
	}
}
