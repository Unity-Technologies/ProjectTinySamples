mergeInto(LibraryManager.library, {

    js_html_initAudio : function() {
        
        ut = ut || {};
        ut._HTML = ut._HTML || {};

        ut._HTML.audio_setGain = function(sourceNode, volume) {
            sourceNode.gainNode.gain.value = volume;
        };
        
        ut._HTML.audio_setPan = function(sourceNode, pan) {
            sourceNode.panNode.setPosition(pan, 0, 1 - Math.abs(pan));
        };

        ut._HTML.unlock = function() {
        // call this method on touch start to create and play a buffer, then check
        // if the audio actually played to determine if audio has now been
        // unlocked on iOS, Android, etc.
            if (!self.audioContext || self.unlockState == 2/*unlocked*/)
                return;

            function unlocked() {
                // update the unlocked state and prevent this check from happening
                // again
                self.unlockState = 2/*unlocked*/;
                delete self.unlockBuffer;
                //console.log("[Audio] unlocked");

                // remove the touch start listener
                document.removeEventListener('click', ut._HTML.unlock, true);
                document.removeEventListener('touchstart', ut._HTML.unlock, true);
                document.removeEventListener('touchend', ut._HTML.unlock, true);
                document.removeEventListener('keydown', ut._HTML.unlock, true);
                document.removeEventListener('keyup', ut._HTML.unlock, true);
            }

            // If AudioContext is already enabled, no need to unlock again
            if (self.audioContext.state === 'running') {
                unlocked();
                return;
            }

            // Limit unlock attempts to two times per second (arbitrary, to avoid a flood
            // of hundreds of unlocks per second)
            var now = performance.now();
            if (self.lastUnlockAttempted && now - self.lastUnlockAttempted < 500)
                return;
            self.lastUnlockAttempted = now;

            // fix Android can not play in suspend state
            if (self.audioContext.resume) self.audioContext.resume();

            // create an empty buffer for unlocking
            if (!self.unlockBuffer) {
                self.unlockBuffer = self.audioContext.createBuffer(1, 1, 22050);
            }

            // and a source for the empty buffer
            var source = self.audioContext.createBufferSource();
            source.buffer = self.unlockBuffer;
            source.connect(self.audioContext.destination);

            // play the empty buffer
            if (typeof source.start === 'undefined') {
                source.noteOn(0);
            } else {
                source.start(0);
            }

            // calling resume() on a stack initiated by user gesture is what
            // actually unlocks the audio on Android Chrome >= 55
            if (self.audioContext.resume) self.audioContext.resume();

            // setup a timeout to check that we are unlocked on the next event
            // loop
            source.onended = function () {
                source.disconnect(0);
                unlocked();
            };
        };

        // audio initialization
        if (!window.AudioContext && !window.webkitAudioContext)
            return false;

        var audioContext =
            new (window.AudioContext || window.webkitAudioContext)();
        if (!audioContext)
            return false;
        audioContext.listener.setPosition(0, 0, 0);

        this.audioContext = audioContext;
        this.audioBuffers = {};
        this.audioSources = {};

        // try to unlock audio
        this.unlockState = 0/*locked*/;
        var navigator = (typeof window !== 'undefined' && window.navigator)
            ? window.navigator
            : null;
        var isMobile = /iPhone|iPad|iPod|Android|BlackBerry|BB10|Silk|Mobi/i.test(
            navigator && navigator.userAgent);
        var isTouch = !!(isMobile ||
            (navigator && navigator.maxTouchPoints > 0) ||
            (navigator && navigator.msMaxTouchPoints > 0));
        if (this.audioContext.state !== 'running' || isMobile || isTouch) {
            ut._HTML.unlock();
        } else {
            this.unlockState = 2/*unlocked*/;
        }

        document.addEventListener('visibilitychange', function() {
            if ((document.visibilityState === 'visible') && audioContext.resume)
                audioContext.resume();
            else if ((document.visibilityState !== 'visible') && audioContext.suspend)
                audioContext.suspend();
        }, true);

        //console.log("[Audio] initialized " + (["locked", "unlocking", "unlocked"][this.unlockState]));
        return true;
    },

    js_html_audioIsUnlocked : function() {
        return false;
        // return this.unlockState == 2/*unlocked*/;
    },

    // unlock audio for browsers
    js_html_audioUnlock : function () {
        return;

    //     var self = this;
    //     if (self.unlockState >= 1/*unlocking or unlocked*/ || !self.audioContext ||
    //         typeof self.audioContext.resume !== 'function')
    //         return;

    //     // setup a touch start listener to attempt an unlock in
    //     document.addEventListener('click', ut._HTML.unlock, true);
    //     document.addEventListener('touchstart', ut._HTML.unlock, true);
    //     document.addEventListener('touchend', ut._HTML.unlock, true);
    //     document.addEventListener('keydown', ut._HTML.unlock, true);
    //     document.addEventListener('keyup', ut._HTML.unlock, true);
    //     // Record that we are now in the unlocking attempt stage so that the above event listeners
    //     // will not be attempted to be registered again.
    //     self.unlockState = 1/*unlocking*/;
    },

    // pause audio context
    js_html_audioPause : function () {
        if (this.audioContext && this.audioContext.suspend) {
            this.audioContext.suspend();
        }
    },

    // resume audio context
    js_html_audioResume : function () {
        if (this.audioContext && this.audioContext.resume) {
            this.audioContext.resume();
        }
    },

    // load audio clip
    js_html_audioStartLoadFile : function (audioClipName, audioClipIdx) 
    {
        return -1;

        // if (!this.audioContext || audioClipIdx < 0)
        //     return -1;

        // audioClipName = UTF8ToString(audioClipName);

        // var url = audioClipName;
        // if (url.substring(0, 9) === "ut-asset:")
        //     url = UT_ASSETS[url.substring(9)];

        // var self = this;
        // var request = new XMLHttpRequest();

        // self.audioBuffers[audioClipIdx] = 'loading';
        // request.open('GET', url, true);
        // request.responseType = 'arraybuffer';
        // request.onload =
        //     function () {
        //         self.audioContext.decodeAudioData(request.response, function (buffer) {
        //             self.audioBuffers[audioClipIdx] = buffer;
        //         });
        //     };
        // request.onerror =
        //     function () {
        //         self.audioBuffers[audioClipIdx] = 'error';
        //     };
        // try {
        //     request.send();
        //     //Module._AudioService_AudioClip_OnLoading(entity,audioClipIdx);
        // } catch (e) {
        //     // LG Nexus 5 + Android OS 4.4.0 + Google Chrome 30.0.1599.105 browser
        //     // odd behavior: If loading from base64-encoded data URI and the
        //     // format is unsupported, request.send() will immediately throw and
        //     // not raise the failure at .onerror() handler. Therefore catch
        //     // failures also eagerly from .send() above.
        //     self.audioBuffers[audioClipIdx] = 'error';
        // }

        // return audioClipIdx;
    },

    /*public enum LoadResult
    {
        stillWorking = 0,
        success = 1,
        failed = 2
    };
    */
    js_html_audioCheckLoad : function (audioClipIdx) {
        return 2;

        // var WORKING_ON_IT = 0;
        // var SUCCESS = 1;
        // var FAILED = 2;

        // if (!this.audioContext || audioClipIdx < 0)
        //     return FAILED;
        // if (this.audioBuffers[audioClipIdx] == null)
        //     return FAILED;
        // if (this.audioBuffers[audioClipIdx] === 'loading')
        //     return WORKING_ON_IT; 
        // if (this.audioBuffers[audioClipIdx] === 'error')
        //     return FAILED;
        // return SUCCESS;
    },

    js_html_audioFree : function (audioClipIdx) {
        return;
        // var audioBuffer = this.audioBuffers[audioClipIdx];
        // if (!audioBuffer)
        //     return;

        // for (var i = 0; i < this.audioSources.length; ++i) {
        //     var sourceNode = this.audioSources[i];
        //     if (sourceNode && sourceNode.buffer === audioBuffer)
        //         sourceNode.stop();
        // }

        // this.audioBuffers[audioClipIdx] = null;
    },

    // create audio source node
    js_html_audioPlay : function (audioClipIdx, audioSourceIdx, volume, pitch, pan, loop) 
    {
        if (!this.audioContext || audioClipIdx < 0 || audioSourceIdx < 0)
            return false;

        if (this.audioContext.state !== 'running')
            return false;

        // require audio buffer to be loaded
        var srcBuffer = this.audioBuffers[audioClipIdx];
        if (!srcBuffer || typeof srcBuffer === 'string')
            return false;

        // create audio source node
        var sourceNode = this.audioContext.createBufferSource();
        sourceNode.buffer = srcBuffer;
        sourceNode.playbackRate.value = pitch;

        var panNode = this.audioContext.createPanner();
        panNode.panningModel = 'equalpower';
        sourceNode.panNode = panNode;

        var gainNode = this.audioContext.createGain();
        gainNode.buffer = srcBuffer;
        sourceNode.gainNode = gainNode;

        sourceNode.connect(gainNode);
        sourceNode.gainNode.connect(panNode);
        sourceNode.panNode.connect(this.audioContext.destination);

        ut._HTML.audio_setGain(sourceNode, volume);
        ut._HTML.audio_setPan(sourceNode, pan);

        // loop value
        sourceNode.loop = loop;

        if (this.audioSources[audioSourceIdx] != undefined)
            // stop audio source node if it is already playing
            this.audioSources[audioSourceIdx].stop();
            
        // store audio source node
        this.audioSources[audioSourceIdx] = sourceNode;
        
        // on ended event
        sourceNode.onended = function (event) {
            sourceNode.stop();
            sourceNode.isPlaying = false;
        };

        // play audio source
        sourceNode.start();
        sourceNode.isPlaying = true;
        //console.log("[Audio] playing " + audioSourceIdx);
        return true;
    },

    // remove audio source node, optionally stop it 
    js_html_audioStop : function (audioSourceIdx, dostop) {
        if (!this.audioContext || audioSourceIdx < 0)
            return;

        // retrieve audio source node
        var sourceNode = this.audioSources[audioSourceIdx];
        if (!sourceNode)
            return;

        // forget audio source node
        sourceNode.onended = null;
        this.audioSources[audioSourceIdx] = null;

        // stop audio source
        if (sourceNode.isPlaying && dostop) {
            sourceNode.stop();
            sourceNode.isPlaying = false;
            //console.log("[Audio] stopping " + audioSourceIdx);
        }
    },

    js_html_audioSetVolume : function (audioSourceIdx, volume) {
        if (!this.audioContext || audioSourceIdx < 0)
            return false;

        // retrieve audio source node
        var sourceNode = this.audioSources[audioSourceIdx];
        if (!sourceNode)
            return false;

        ut._HTML.audio_setGain(sourceNode, volume);
        return true;
    },
    
    js_html_audioSetPan : function (audioSourceIdx, pan) {
        if (!this.audioContext || audioSourceIdx < 0)
            return false;

        // retrieve audio source node
        var sourceNode = this.audioSources[audioSourceIdx];
        if (!sourceNode)
            return false;

        ut._HTML.audio_setPan(sourceNode, pan);
        return true;
    },

    js_html_audioSetPitch : function (audioSourceIdx, pitch) {
        if (!this.audioContext || audioSourceIdx < 0)
            return false;

        // retrieve audio source node
        var sourceNode = this.audioSources[audioSourceIdx];
        if (!sourceNode)
            return false;

        sourceNode.playbackRate.value = pitch;
        return true;
    },

    js_html_audioIsPlaying : function (audioSourceIdx) {
        if (!this.audioContext || audioSourceIdx < 0)
            return false;

        if (this.audioSources[audioSourceIdx] == null)
            return false;

        return this.audioSources[audioSourceIdx].isPlaying;
    }
});
