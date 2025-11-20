/**
 * WebGL JavaScript Plugin for Unity <-> React/JavaScript communication
 *
 * This file is compiled into the Unity WebGL build and provides JavaScript
 * functions that can be called from C# using [DllImport("__Internal")].
 *
 * NOTE: This file must be in a folder with ".jslib" extension to be recognized by Unity.
 */

mergeInto(LibraryManager.library, {
  /**
   * Send a message to the React/JavaScript layer.
   * Called from Unity via WebGLBridge.SendMessageToWeb()
   *
   * @param {string} eventType - The type of event (e.g., "GameReady", "PlayerSpawned")
   * @param {string} data - JSON string containing event data
   */
  SendMessageToWeb: function(eventType, data) {
    var eventTypeStr = UTF8ToString(eventType);
    var dataStr = UTF8ToString(data);

    console.log('[Unity -> Web] Event:', eventTypeStr, 'Data:', dataStr);

    try {
      // Parse the data string to JSON
      var parsedData = dataStr ? JSON.parse(dataStr) : {};

      // Dispatch custom event that React can listen to
      var event = new CustomEvent('UnityMessage', {
        detail: {
          type: eventTypeStr,
          data: parsedData,
          timestamp: new Date().toISOString()
        }
      });

      window.dispatchEvent(event);

      // Also try to call react-unity-webgl's event system if available
      if (window.unityEventListeners && window.unityEventListeners[eventTypeStr]) {
        window.unityEventListeners[eventTypeStr].forEach(function(callback) {
          callback(parsedData);
        });
      }
    } catch (e) {
      console.error('[Unity -> Web] Error processing message:', e);
    }
  },

  /**
   * Send an error message to JavaScript.
   * Called from Unity via WebGLBridge.SendErrorToWeb()
   *
   * @param {string} errorMessage - The error message
   */
  SendErrorToWeb: function(errorMessage) {
    var errorStr = UTF8ToString(errorMessage);
    console.error('[Unity Error]', errorStr);

    // Dispatch error event
    var event = new CustomEvent('UnityError', {
      detail: {
        message: errorStr,
        timestamp: new Date().toISOString()
      }
    });

    window.dispatchEvent(event);
  },

  /**
   * Send a Float32Array to JavaScript.
   * More efficient than base64 encoding for large float arrays.
   *
   * @param {string} eventType - The type of event
   * @param {float[]} data - Float array pointer from Unity
   * @param {number} length - Length of the array
   */
  SendFloat32Array: function(eventType, data, length) {
    var eventTypeStr = UTF8ToString(eventType);

    try {
      // Create a Float32Array view of Unity's memory
      var floatArray = new Float32Array(HEAPF32.buffer, data, length);

      // Copy the data (important! Unity's memory can be reallocated)
      var arrayCopy = new Float32Array(floatArray);

      console.log('[Unity -> Web] Float32Array:', eventTypeStr, 'Length:', length);

      // Dispatch event with typed array
      var event = new CustomEvent('UnityArrayData', {
        detail: {
          type: eventTypeStr,
          dataType: 'Float32Array',
          data: arrayCopy,
          length: length,
          timestamp: new Date().toISOString()
        }
      });

      window.dispatchEvent(event);

      // Also make available via custom listeners
      if (window.unityEventListeners && window.unityEventListeners[eventTypeStr]) {
        window.unityEventListeners[eventTypeStr].forEach(function(callback) {
          callback(arrayCopy);
        });
      }
    } catch (e) {
      console.error('[Unity -> Web] Error sending Float32Array:', e);
    }
  },

  /**
   * Send an Int32Array to JavaScript.
   *
   * @param {string} eventType - The type of event
   * @param {int[]} data - Int array pointer from Unity
   * @param {number} length - Length of the array
   */
  SendInt32Array: function(eventType, data, length) {
    var eventTypeStr = UTF8ToString(eventType);

    try {
      // Create an Int32Array view of Unity's memory
      var intArray = new Int32Array(HEAP32.buffer, data, length);

      // Copy the data
      var arrayCopy = new Int32Array(intArray);

      console.log('[Unity -> Web] Int32Array:', eventTypeStr, 'Length:', length);

      // Dispatch event
      var event = new CustomEvent('UnityArrayData', {
        detail: {
          type: eventTypeStr,
          dataType: 'Int32Array',
          data: arrayCopy,
          length: length,
          timestamp: new Date().toISOString()
        }
      });

      window.dispatchEvent(event);

      // Custom listeners
      if (window.unityEventListeners && window.unityEventListeners[eventTypeStr]) {
        window.unityEventListeners[eventTypeStr].forEach(function(callback) {
          callback(arrayCopy);
        });
      }
    } catch (e) {
      console.error('[Unity -> Web] Error sending Int32Array:', e);
    }
  },

  /**
   * Send a Uint8Array (byte array) to JavaScript.
   *
   * @param {string} eventType - The type of event
   * @param {byte[]} data - Byte array pointer from Unity
   * @param {number} length - Length of the array
   */
  SendUint8Array: function(eventType, data, length) {
    var eventTypeStr = UTF8ToString(eventType);

    try {
      // Create a Uint8Array view of Unity's memory
      var byteArray = new Uint8Array(HEAPU8.buffer, data, length);

      // Copy the data
      var arrayCopy = new Uint8Array(byteArray);

      console.log('[Unity -> Web] Uint8Array:', eventTypeStr, 'Length:', length);

      // Dispatch event
      var event = new CustomEvent('UnityArrayData', {
        detail: {
          type: eventTypeStr,
          dataType: 'Uint8Array',
          data: arrayCopy,
          length: length,
          timestamp: new Date().toISOString()
        }
      });

      window.dispatchEvent(event);

      // Custom listeners
      if (window.unityEventListeners && window.unityEventListeners[eventTypeStr]) {
        window.unityEventListeners[eventTypeStr].forEach(function(callback) {
          callback(arrayCopy);
        });
      }
    } catch (e) {
      console.error('[Unity -> Web] Error sending Uint8Array:', e);
    }
  },

  /**
   * Send a Float64Array (double array) to JavaScript.
   *
   * @param {string} eventType - The type of event
   * @param {double[]} data - Double array pointer from Unity
   * @param {number} length - Length of the array
   */
  SendFloat64Array: function(eventType, data, length) {
    var eventTypeStr = UTF8ToString(eventType);

    try {
      // Create a Float64Array view of Unity's memory
      var doubleArray = new Float64Array(HEAPF64.buffer, data, length);

      // Copy the data
      var arrayCopy = new Float64Array(doubleArray);

      console.log('[Unity -> Web] Float64Array:', eventTypeStr, 'Length:', length);

      // Dispatch event
      var event = new CustomEvent('UnityArrayData', {
        detail: {
          type: eventTypeStr,
          dataType: 'Float64Array',
          data: arrayCopy,
          length: length,
          timestamp: new Date().toISOString()
        }
      });

      window.dispatchEvent(event);

      // Custom listeners
      if (window.unityEventListeners && window.unityEventListeners[eventTypeStr]) {
        window.unityEventListeners[eventTypeStr].forEach(function(callback) {
          callback(arrayCopy);
        });
      }
    } catch (e) {
      console.error('[Unity -> Web] Error sending Float64Array:', e);
    }
  },

  /**
   * Get the current window location hostname.
   * Returns the hostname (e.g., "localhost" or "bugwars.kbve.com")
   *
   * @returns {string} The current hostname
   */
  GetHostname: function() {
    var hostname = window.location.hostname;
    var bufferSize = lengthBytesUTF8(hostname) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(hostname, buffer, bufferSize);
    return buffer;
  },

  /**
   * Get the WebSocket URL for the current environment.
   * Automatically determines if running on localhost or production.
   *
   * @returns {string} The WebSocket URL (e.g., "ws://localhost:4321/ws" or "wss://bugwars.kbve.com/ws")
   */
  GetWebSocketUrl: function() {
    var hostname = window.location.hostname;
    var protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    var port = '';

    // For localhost, use port 4321 (Axum development server)
    if (hostname === 'localhost' || hostname === '127.0.0.1') {
      port = ':4321';
    }

    var wsUrl = protocol + '//' + hostname + port + '/ws';

    console.log('[WebGLBridge] WebSocket URL:', wsUrl);

    var bufferSize = lengthBytesUTF8(wsUrl) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(wsUrl, buffer, bufferSize);
    return buffer;
  },

  /**
   * Check if running in development mode (localhost).
   *
   * @returns {number} 1 if localhost, 0 otherwise
   */
  IsLocalhost: function() {
    var hostname = window.location.hostname;
    return (hostname === 'localhost' || hostname === '127.0.0.1') ? 1 : 0;
  }
});

/**
 * Initialize Unity event listener system on page load.
 * This allows JavaScript to register listeners for Unity events.
 */
(function() {
  if (typeof window !== 'undefined') {
    // Create event listener registry
    window.unityEventListeners = {};

    /**
     * Register a listener for Unity events.
     *
     * @param {string} eventType - The event type to listen for
     * @param {function} callback - Callback function to handle the event
     * @returns {function} Unsubscribe function
     */
    window.addUnityEventListener = function(eventType, callback) {
      if (!window.unityEventListeners[eventType]) {
        window.unityEventListeners[eventType] = [];
      }

      window.unityEventListeners[eventType].push(callback);

      // Return unsubscribe function
      return function() {
        var index = window.unityEventListeners[eventType].indexOf(callback);
        if (index > -1) {
          window.unityEventListeners[eventType].splice(index, 1);
        }
      };
    };

    /**
     * Remove a listener for Unity events.
     *
     * @param {string} eventType - The event type
     * @param {function} callback - The callback to remove
     */
    window.removeUnityEventListener = function(eventType, callback) {
      if (window.unityEventListeners[eventType]) {
        var index = window.unityEventListeners[eventType].indexOf(callback);
        if (index > -1) {
          window.unityEventListeners[eventType].splice(index, 1);
        }
      }
    };

    console.log('[WebGLBridge] Unity event system initialized');
  }
})();
