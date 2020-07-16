mergeInto(LibraryManager.library, {
    
    js_html_playerconnectionPlatformInit : function() {
        ut = ut || {};
        ut._HTML = ut._HTML || {};

        ut._HTML.pc_isConnected = function() {
            return self.pcStateConnected || false;
        };
        
        ut._HTML.pc_disconnect = function() {
            if (!self.ws)
                return;
            self.ws.onopen = null;
            self.ws.onmessage = null;
            self.ws.onclose = null;
            self.ws.close();
            console.log("WebGL Player Connection disconnected");
            delete self.ws;

            self.pcStateConnected = false;
            self.pcStateConnecting = false;
            self.pcBufferPosW = 0;
            self.pcBufferPosR = 0;
        };
        
        this.pcBufferSize = 65536;   // max websocket buffer size
		this.pcBufferPosW = 0;
        this.pcBufferPosR = 0;
        this.pcStateConnected = false;
        this.pcStateConnecting = false;

        // sizeLow32, sizeHigh32, alignment, allocator (4 = persistent)
        this.pcBuffer = _unsafeutility_malloc(this.pcBufferSize, 0, 0, 4);
    },

    js_html_playerconnectionPlatformShutdown : function() {
        ut._HTML.pc_disconnect();
        _unsafeutility_free(this.pcBuffer, 4);
    },

    js_html_playerconnectionConnect : function(address) {
        var stringAddress = UTF8ToString(address);
        this.ws = new WebSocket(stringAddress, "binary");
        this.ws.binaryType = "arraybuffer";
        
        this.pcStateConnecting = true;

        this.ws.onopen = function() {
            console.log("WebGL Player Connection opened");
            self.pcStateConnected = true;
            self.pcStateConnecting = false;
        };
        
        this.ws.onmessage = function(e) {
            var data8 = new Uint8Array(e.data);

            // Allocate larger buffer for larger data
            if (self.pcBufferSize < self.pcBufferPosW + data8.length) {
                var oldBuffer = self.pcBuffer;
                while (self.pcBufferSize < self.pcBufferPosW + data8.length)
                    self.pcBufferSize *= 2;
                
                this.pcBuffer = _unsafeutility_malloc(this.pcBufferSize, 0, 0, 4);
                HEAP8.set(HEAP8.subarray(oldBuffer, oldBuffer + self.pcBufferPosW), self.pcBuffer);                
                _unsafeutility_free(oldBuffer, 4);
            }

            HEAP8.set(data8, self.pcBuffer + self.pcBufferPosW);
            self.pcBufferPosW += data8.length;
        };
        
        this.ws.onclose = function() {
            console.log("WebGL Player Connection closed");
            self.ws.onopen = null;
            self.ws.onmessage = null;
            self.ws.onclose = null;
            delete this.ws;
            self.pcStateConnected = false;
            self.pcStateConnecting = false;
        };
    },

    js_html_playerconnectionDisconnect : function() {
        ut._HTML.pc_disconnect();
    },

    js_html_playerconnectionSend : function(data, size) {
        if (this.pcStateConnecting)
            return 0;

        // readyState 1 is OPEN i.e. ready
        if (this.ws && this.ws.readyState == 1)
            this.ws.send(HEAPU8.subarray(data, data + size));

        // Error if:
        // - not initialized
        // - not connected
        // - connected but send caused buffer overflow resulting in WebSocket auto-disconnect
        if (!ut._HTML.pc_isConnected())
            return 0xffffffff;

        // If successful, exactly this size was added to WebSocket internal buffering
        return size;
    },

	js_html_playerconnectionReceive : function(outBuffer, reqBytes) {
        if (this.pcStateConnecting)
            return 0;
        if (!ut._HTML.pc_isConnected())
            return 0xffffffff;

        // This should happen on the last read to indicate we are done grabbing data from web sockets
        if (this.pcBufferPosR == this.pcBufferPosW) {
            this.pcBufferPosR = 0;
            this.pcBufferPosW = 0;
            return 0;
        }

        var outBytes = reqBytes;
        var dataAvail = this.pcBufferPosW - this.pcBufferPosR;
        if (dataAvail < outBytes)
            outBytes = dataAvail;

        HEAP8.set(HEAP8.subarray(this.pcBuffer + this.pcBufferPosR, this.pcBuffer + this.pcBufferPosR + outBytes), outBuffer);
        
        this.pcBufferPosR += outBytes;

		return outBytes;
    },
    
    js_html_playerconnectionLostConnection : function() {
        return (!this.pcStateConnecting && this.ws && this.ws.readyState == 1 && !ut._HTML.pc_isConnected()) ? 1 : 0;
    },

    js_html_playerconnectionIsConnecting : function() {
        return this.pcStateConnecting ? 1 : 0;
    }
});
