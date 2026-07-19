(function() {
  var isIOS = (/iphone|ipad|ipod/i).test(window.navigator.userAgent.toLowerCase());
  if (isIOS) {
    console = {};
    console.log = function(log) {
      var iframe = document.createElement('iframe');
      iframe.setAttribute('src', 'ios-log: ' + log);
      document.documentElement.appendChild(iframe);
      iframe.parentNode.removeChild(iframe);
      iframe = null;
    };
    console.debug = console.info = console.warn = console.error = console.log;
  }
}());

(function() {
  // Establish the root mraidbridge object.
  var mraidbridge = window.mraidbridge = {};

  // Listeners for bridge events.
  var listeners = {};

  // Queue to track pending calls to the native SDK.
  var nativeCallQueue = [];

  // Whether a native call is currently in progress.
  var nativeCallInFlight = false;

  //////////////////////////////////////////////////////////////////////////////////////////////////

  mraidbridge.fireReadyEvent = function() {
    mraidbridge.fireEvent('ready');
  };

  mraidbridge.fireChangeEvent = function(properties) {
    mraidbridge.fireEvent('change', properties);
  };

  mraidbridge.fireErrorEvent = function(message, action) {
    mraidbridge.fireEvent('error', message, action);
  };

  mraidbridge.fireEvent = function(type) {
    var ls = listeners[type];
    if (ls) {
      var args = Array.prototype.slice.call(arguments);
      args.shift();
      var l = ls.length;
      for (var i = 0; i < l; i++) {
        ls[i].apply(null, args);
      }
    }
  };

  mraidbridge.nativeCallComplete = function(command) {
    if (nativeCallQueue.length === 0) {
      nativeCallInFlight = false;
      return;
    }

    var nextCall = nativeCallQueue.pop();
    window.location = nextCall;
  };

  mraidbridge.executeNativeCall = function(command) {
    var call = 'mraid://' + command;

    var key, value;
    var isFirstArgument = true;

    for (var i = 1; i < arguments.length; i += 2) {
      key = arguments[i];
      value = arguments[i + 1];

      if (value === null) continue;

      if (isFirstArgument) {
        call += '?';
        isFirstArgument = false;
      } else {
        call += '&';
      }

       console.log(value);
      call += encodeURIComponent(key) + '=' + encodeURIComponent(value);
    }

    if (nativeCallInFlight) {
      nativeCallQueue.push(call);
    } else {
      nativeCallInFlight = true;
      window.location = call;
    }
  };

  //////////////////////////////////////////////////////////////////////////////////////////////////

  mraidbridge.addEventListener = function(event, listener) {
    var eventListeners;
    listeners[event] = listeners[event] || [];
    eventListeners = listeners[event];

    for (var l in eventListeners) {
      // Listener already registered, so no need to add it.
      if (listener === l) return;
    }

    eventListeners.push(listener);
  };

  mraidbridge.removeEventListener = function(event, listener) {
    if (listeners.hasOwnProperty(event)) {
      var eventListeners = listeners[event];
      if (eventListeners) {
        var idx = eventListeners.indexOf(listener);
        if (idx !== -1) {
          eventListeners.splice(idx, 1);
        }
      }
    }
  };
}());

(function() {
  var mraid = window.mraid = {};
  var bridge = window.mraidbridge;

  var LogLevelEnum = {
  		"DEBUG"   : 0,
  		"INFO"    : 1,
  		"WARNING" : 2,
  		"ERROR"   : 3,
  		"NONE"    : 4
  	};

  	var logLevel = LogLevelEnum.DEBUG;
  	var log = {};

  	log.d = function(msg) {
  		if (logLevel <= LogLevelEnum.DEBUG) {
  			console.log("(D-mraid.js) " + msg);
  		}
  	};

  	log.i = function(msg) {
  		if (logLevel <= LogLevelEnum.INFO) {
  			console.log("(I-mraid.js) " + msg);
  		}
  	};

  	log.w = function(msg) {
  		if (logLevel <= LogLevelEnum.WARNING) {
  			console.log("(W-mraid.js) " + msg);
  		}
  	};

  	log.e = function(msg) {
  		if (logLevel <= LogLevelEnum.ERROR) {
  			console.log("(E-mraid.js) " + msg);
  		}
  	};

  // Constants. ////////////////////////////////////////////////////////////////////////////////////

  var currentPosition = {
    		"x" : 0,
    		"y" : 0,
    		"width" : 0,
    		"height" : 0
   };

   var defaultPosition = {
    		"x" : 0,
    		"y" : 0,
    		"width" : 0,
    		"height" : 0
   };

   var maxSize = {
    		"width" : 0,
    		"height" : 0
   };

   var screenSize = {
    		"width" : 0,
    		"height" : 0
   };

  var isExpandPropertiesSet = false;

  var VERSION = mraid.VERSION = '2.0';

  var STATES = mraid.STATES = {
    LOADING: 'loading',     // Initial state.
    DEFAULT: 'default',
    EXPANDED: 'expanded',
    RESIZED : "resized",
    HIDDEN: 'hidden'
  };

  var RESIZE_PROPERTIES_CUSTOM_CLOSE_POSITION = mraid.RESIZE_PROPERTIES_CUSTOM_CLOSE_POSITION = {
  	"TOP_LEFT" : "top-left",
  	"TOP_CENTER" : "top-center",
  	"TOP_RIGHT" : "top-right",
  	"CENTER" : "center",
  	"BOTTOM_LEFT" : "bottom-left",
  	"BOTTOM_CENTER" : "bottom-center",
  	"BOTTOM_RIGHT" : "bottom-right"
  };

  var EVENTS = mraid.EVENTS = {
    ERROR: 'error',
    INFO: 'info',
    READY: 'ready',
    SIZECHANGE : "sizeChange",
    STATECHANGE: 'stateChange',
    VIEWABLECHANGE: 'viewableChange'
  };

  var PLACEMENT_TYPES = mraid.PLACEMENT_TYPES = {
    UNKNOWN: 'unknown',
    INLINE: 'inline',
    INTERSTITIAL: 'interstitial'
  };

  // External MRAID state: may be directly or indirectly modified by the ad JS. ////////////////////

  // Properties which define the behavior of an expandable ad.
  var expandProperties = {
    width: -1,
    height: -1,
    useCustomClose: false,
    isModal: true,
    lockOrientation: false
  };

  var hasSetCustomSize = false;

  var hasSetCustomClose = false;

  var listeners = {};

  // Internal MRAID state. Modified by the native SDK. /////////////////////////////////////////////

  var state = STATES.LOADING;

  var isViewable = false;

  var screenSize = { width: -1, height: -1 };

  var placementType = PLACEMENT_TYPES.UNKNOWN;

  var isResizeReady = false;

  var resizeProperties = {
  		"width" : 0,
  		"height" : 0,
  		"customClosePosition" : RESIZE_PROPERTIES_CUSTOM_CLOSE_POSITION.TOP_RIGHT,
  		"offsetX" : 0,
  		"offsetY" : 0,
  		"allowOffscreen" : true
  };

  var supports = {
    sms: false,
    tel: false,
    calendar: false,
    storePicture: false,
    inlineVideo: false
  };

  //////////////////////////////////////////////////////////////////////////////////////////////////

  var EventListeners = function(event) {
    this.event = event;
    this.count = 0;
    var listeners = {};

    this.add = function(func) {
      var id = String(func);
      if (!listeners[id]) {
        listeners[id] = func;
        this.count++;
      }
    };

    this.remove = function(func) {
      var id = String(func);
      if (listeners[id]) {
        listeners[id] = null;
        delete listeners[id];
        this.count--;
        return true;
      } else {
        return false;
      }
    };

    this.removeAll = function() {
      for (var id in listeners) {
        if (listeners.hasOwnProperty(id)) this.remove(listeners[id]);
      }
    };

    this.broadcast = function(args) {
      for (var id in listeners) {
        if (listeners.hasOwnProperty(id)) listeners[id].apply({}, args);
      }
    };

    this.toString = function() {
      var out = [event, ':'];
      for (var id in listeners) {
        if (listeners.hasOwnProperty(id)) out.push('|', id, '|');
      }
      return out.join('');
    };
  };

  var broadcastEvent = function() {
    var args = new Array(arguments.length);
    var l = arguments.length;
    for (var i = 0; i < l; i++) args[i] = arguments[i];
    var event = args.shift();
    if (listeners[event]) listeners[event].broadcast(args);
  };

  var contains = function(value, array) {
    for (var i in array) {
      if (array[i] === value) return true;
    }
    return false;
  };

  var clone = function(obj) {
    if (obj === null) return null;
    var f = function() {};
    f.prototype = obj;
    return new f();
  };

  var stringify = function(obj) {
    if (typeof obj === 'object') {
      var out = [];
      if (obj.push) {
        // Array.
        for (var p in obj) out.push(obj[p]);
        return '[' + out.join(',') + ']';
      } else {
        // Other object.
        for (var p in obj) out.push("'" + p + "': " + obj[p]);
        return '{' + out.join(',') + '}';
      }
    } else return String(obj);
  };

  var trim = function(str) {
    return str.replace(/^\s+|\s+$/g, '');
  };

  // Functions that will be invoked by the native SDK whenever a "change" event occurs.
  var changeHandlers = {
    state: function(val) {
      if (state === STATES.LOADING) {
        broadcastEvent(EVENTS.INFO, 'Native SDK initialized.');
      }
      state = val;
      broadcastEvent(EVENTS.INFO, 'Set state to ' + stringify(val));
      broadcastEvent(EVENTS.STATECHANGE, state);
    },

    viewable: function(val) {
      isViewable = val;
      broadcastEvent(EVENTS.INFO, 'Set isViewable to ' + stringify(val));
      broadcastEvent(EVENTS.VIEWABLECHANGE, isViewable);
    },

    placementType: function(val) {
      broadcastEvent(EVENTS.INFO, 'Set placementType to ' + stringify(val));
      placementType = val;
    },

    screenSize: function(val) {
      broadcastEvent(EVENTS.INFO, 'Set screenSize to ' + stringify(val));
      for (var key in val) {
        if (val.hasOwnProperty(key)) screenSize[key] = val[key];
      }

      if (!hasSetCustomSize) {
        expandProperties['width'] = screenSize['width'];
        expandProperties['height'] = screenSize['height'];
      }
    },

    expandProperties: function(val) {
      broadcastEvent(EVENTS.INFO, 'Merging expandProperties with ' + stringify(val));
      for (var key in val) {
        if (val.hasOwnProperty(key)) expandProperties[key] = val[key];
      }
    },

    supports: function(val) {
      broadcastEvent(EVENTS.INFO, 'Set supports to ' + stringify(val));
        supports = val;
    },
  };

  var validate = function(obj, validators, action, merge) {
    if (!merge) {
      // Check to see if any required properties are missing.
      if (obj === null) {
        broadcastEvent(EVENTS.ERROR, 'Required object not provided.', action);
        return false;
      } else {
        for (var i in validators) {
          if (validators.hasOwnProperty(i) && obj[i] === undefined) {
            broadcastEvent(EVENTS.ERROR, 'Object is missing required property: ' + i + '.', action);
            return false;
          }
        }
      }
    }

    for (var prop in obj) {
      var validator = validators[prop];
      var value = obj[prop];
      if (validator && !validator(value)) {
        // Failed validation.
        broadcastEvent(EVENTS.ERROR, 'Value of property ' + prop + ' is invalid.',
          action);
        return false;
      }
    }
    return true;
  };

  var expandPropertyValidators = {
    width: function(v) { return !isNaN(v) && v >= 0; },
    height: function(v) { return !isNaN(v) && v >= 0; },
    useCustomClose: function(v) { return (typeof v === 'boolean'); },
    lockOrientation: function(v) { return (typeof v === 'boolean'); }
  };

  //////////////////////////////////////////////////////////////////////////////////////////////////

  bridge.addEventListener('change', function(properties) {
    for (var p in properties) {
      if (properties.hasOwnProperty(p)) {
        var handler = changeHandlers[p];
        handler(properties[p]);
      }
    }
  });

  bridge.addEventListener('error', function(message, action) {
    broadcastEvent(EVENTS.ERROR, message, action);
  });

  bridge.addEventListener('ready', function() {
    broadcastEvent(EVENTS.READY);
  });

  //////////////////////////////////////////////////////////////////////////////////////////////////

  mraid.addEventListener = function(event, listener) {
    if (!event || !listener) {
      broadcastEvent(EVENTS.ERROR, 'Both event and listener are required.', 'addEventListener');
    } else if (!contains(event, EVENTS)) {
      broadcastEvent(EVENTS.ERROR, 'Unknown MRAID event: ' + event, 'addEventListener');
    } else {
      if (!listeners[event]) listeners[event] = new EventListeners(event);
      listeners[event].add(listener);
    }
  };

  mraid.close = function() {
    if (state === STATES.HIDDEN) {
      broadcastEvent(EVENTS.ERROR, 'Ad cannot be closed when it is already hidden.',
        'close');
    } else bridge.executeNativeCall('close');
  };

  mraid.expand = function(URL) {
    if (this.getState() !== STATES.DEFAULT) {
      broadcastEvent(EVENTS.ERROR, 'Ad can only be expanded from the default state.', 'expand');
    } else {
      var args = ['expand'];

      if (this.getHasSetCustomClose()) {
        args = args.concat(['shouldUseCustomClose', expandProperties.useCustomClose ? 'true' : 'false']);
      }

      if (this.getHasSetCustomSize()) {
        if (expandProperties.width >= 0 && expandProperties.height >= 0) {
          args = args.concat(['w', expandProperties.width, 'h', expandProperties.height]);
        }
      }

      if (typeof expandProperties.lockOrientation !== 'undefined') {
        args = args.concat(['lockOrientation', expandProperties.lockOrientation]);
      }

      if (URL) {
        args = args.concat(['url', URL]);
      }

      bridge.executeNativeCall.apply(this, args);
    }
  };

  mraid.getHasSetCustomClose = function() {
      return hasSetCustomClose;
  };

  mraid.getHasSetCustomSize = function() {
      return hasSetCustomSize;
  };

  mraid.getExpandProperties = function() {
    var properties = {
      width: expandProperties.width,
      height: expandProperties.height,
      useCustomClose: expandProperties.useCustomClose,
      isModal: expandProperties.isModal
    };
    return properties;
  };

  mraid.getPlacementType = function() {
    return placementType;
  };

  mraid.getState = function() {
    return state;
  };

  mraid.getVersion = function() {
    return mraid.VERSION;
  };

  mraid.isViewable = function() {
    return isViewable;
  };

  mraid.open = function(URL) {
    if (!URL) broadcastEvent(EVENTS.ERROR, 'URL is required.', 'open');
    else bridge.executeNativeCall('open', 'url', URL);
  };

  mraid.removeEventListener = function(event, listener) {
    if (!event) broadcastEvent(EVENTS.ERROR, 'Event is required.', 'removeEventListener');
    else {
      if (listener && (!listeners[event] || !listeners[event].remove(listener))) {
        broadcastEvent(EVENTS.ERROR, 'Listener not currently registered for event.',
          'removeEventListener');
        return;
      } else if (listeners[event]) listeners[event].removeAll();

      if (listeners[event] && listeners[event].count === 0) {
        listeners[event] = null;
        delete listeners[event];
      }
    }
  };

  mraid.setExpandProperties = function(properties) {
    if (validate(properties, expandPropertyValidators, 'setExpandProperties', true)) {
      if (properties.hasOwnProperty('width') || properties.hasOwnProperty('height')) {
        hasSetCustomSize = true;
      }

      if (properties.hasOwnProperty('useCustomClose')) hasSetCustomClose = true;

      var desiredProperties = ['width', 'height', 'useCustomClose', 'lockOrientation'];
      var length = desiredProperties.length;
      for (var i = 0; i < length; i++) {
        var propname = desiredProperties[i];
        if (properties.hasOwnProperty(propname)) expandProperties[propname] = properties[propname];
      }
    }
  };

  mraid.useCustomClose = function(shouldUseCustomClose) {
    expandProperties.useCustomClose = shouldUseCustomClose;
    hasSetCustomClose = true;
    bridge.executeNativeCall('usecustomclose', 'shouldUseCustomClose', shouldUseCustomClose);
  };

  // MRAID 2.0 APIs ////////////////////////////////////////////////////////////////////////////////

  mraid.createCalendarEvent = function(parameters) {
//    CalendarEventParser.initialize(parameters);
//    if (CalendarEventParser.parse()) {
//      log.d("CalendarEventParser.arguments: " + CalendarEventParser.arguments);
//      bridge.executeNativeCall.apply(this, CalendarEventParser.arguments);
//    } else {
//      broadcastEvent(EVENTS.ERROR, CalendarEventParser.errors[0], 'createCalendarEvent');
//    }
    callNative("createCalendarEvent?url="	+ encodeURIComponent(parameters));
  };

  mraid.supports = function(feature) {
    return supports[feature];
  };

  mraid.playVideo = function(uri) {
    if (!mraid.isViewable()) {
      broadcastEvent(EVENTS.ERROR, 'playVideo cannot be called until the ad is viewable', 'playVideo');
      return;
    }

    if (!uri) {
      broadcastEvent(EVENTS.ERROR, 'playVideo must be called with a valid URI', 'playVideo');
    } else {
      bridge.executeNativeCall.apply(this, ['playVideo', 'uri', uri]);
    }
  };

  mraid.storePicture = function(uri) {
    if (!mraid.isViewable()) {
      broadcastEvent(EVENTS.ERROR, 'storePicture cannot be called until the ad is viewable', 'storePicture');
      return;
    }

    if (!uri) {
      broadcastEvent(EVENTS.ERROR, 'storePicture must be called with a valid URI', 'storePicture');
    } else {
      bridge.executeNativeCall.apply(this, ['storePicture', 'uri', uri]);
    }
  };

  mraid.setResizeProperties = function(properties) {
  		log.i("mraid.setResizeProperties");

  		isResizeReady = false;

  		// resizeProperties contains 6 read-write properties:
  		// width, height, offsetX, offsetY, customClosePosition, allowOffscreen

  		// The properties object passed into this function must contain width, height, offsetX, offsetY.
  		// The remaining two properties are optional.
  		var requiredProps = [ "width", "height", "offsetX", "offsetY" ];
  		for (var i = 0; i < requiredProps.length; i++) {
  			var propname = requiredProps[i];
  			log.i("propname: " + propname);
  			if (!properties.hasOwnProperty(propname)) {
  				mraidbridge.fireErrorEvent(
  						"required property " + propname + " is missing",
  						"mraid.setResizeProperties");
  				return;
  			}
  		}

  		if (!validateResizeProperties(properties, "setResizeProperties")) {
  			mraidbridge.fireErrorEvent("failed validation", "mraid.setResizeProperties");
  			return;
  		}

        var adjustments = { "x": 0, "y": 0 };

  		var allowOffscreen = properties.hasOwnProperty("allowOffscreen") ? properties.allowOffscreen : resizeProperties.allowOffscreen;
          if (!allowOffscreen) {
              if (properties.width > maxSize.width || properties.height > maxSize.height) {
                  mraidbridge.fireErrorEvent("resize width or height is greater than the maxSize width or height", "mraid.setResizeProperties");
                  return;
              }
              adjustments = fitResizeViewOnScreen(properties);
          } else if (!isCloseRegionOnScreen(properties)) {
              mraidbridge.fireErrorEvent("close event region will not appear entirely onscreen", "mraid.setResizeProperties");
              return;
          }

  		var rwProps = [ "width", "height", "offsetX", "offsetY", "customClosePosition", "allowOffscreen" ];
  		for (var i = 0; i < rwProps.length; i++) {
  			var propname = rwProps[i];
  			if (properties.hasOwnProperty(propname)) {
  				resizeProperties[propname] = properties[propname];
  			}
  		}

  		var params =
  			"width=" + resizeProperties.width +
  			"&height=" + resizeProperties.height +
  	        "&offsetX=" + (resizeProperties.offsetX + adjustments.x) +
  	        "&offsetY=" + (resizeProperties.offsetY + adjustments.y) +
  			"&customClosePosition=" + resizeProperties.customClosePosition +
  			"&allowOffscreen=" + resizeProperties.allowOffscreen;

  		callNative("setResizeProperties?" + params);

  		isResizeReady = true;
  	};

  	function callNative(command) {
        var iframe = document.createElement("IFRAME");
        iframe.setAttribute("src", "mraid://" + command);
        document.documentElement.appendChild(iframe);
        iframe.parentNode.removeChild(iframe);
        iframe = null;
    };

  	// The action parameter is a string which is the name of the setter function
    // which called this function
    // (in other words, setExpandPropeties, setOrientationProperties, or
    // setResizeProperties).
    // It serves both as the key to get the the appropriate set of validating
    // functions from the allValidators object
    // as well as the action parameter of any error event that may be thrown.
   function validateResizeProperties(properties, action) {
        var retval = true;
        var validators = allValidators[action];
        for (var prop in properties) {
            var validator = validators[prop];
            var value = properties[prop];
            if (validator && !validator(value)) {
                mraidbridge.fireErrorEvent("Value of property " + prop + " (" + value	+ ") is invalid", "mraid." + action);
                retval = false;
            }
        }
        return retval;
   };

    var allValidators = {
        "setExpandProperties" : {
            // In MRAID 2.0, the only property in expandProperties we actually care about is useCustomClose.
            // Still, we'll do a basic sanity check on the width and height properties, too.
            "width" : function(width) {
                return !isNaN(width);
            },
            "height" : function(height) {
                return !isNaN(height);
            },
            "useCustomClose" : function(useCustomClose) {
                return (typeof useCustomClose === "boolean");
            }
        },
        "setOrientationProperties" : {
            "allowOrientationChange" : function(allowOrientationChange) {
                return (typeof allowOrientationChange === "boolean");
            },
            "forceOrientation" : function(forceOrientation) {
                var validValues = [ "portrait", "landscape", "none" ];
                return (typeof forceOrientation === "string" && validValues.indexOf(forceOrientation) !== -1);
            }
        },
        "setResizeProperties" : {
            "width" : function(width) {
                return !isNaN(width) && 50 <= width;
            },
            "height" : function(height) {
                return !isNaN(height) && 50 <= height;
            },
            "offsetX" : function(offsetX) {
                return !isNaN(offsetX);
            },
            "offsetY" : function(offsetY) {
                return !isNaN(offsetY);
            },
            "customClosePosition" : function(customClosePosition) {
                var validPositions = [ "top-left", "top-center", "top-right",
                                       "center",
                                       "bottom-left", "bottom-center",	"bottom-right" ];
                return (typeof customClosePosition === "string" && validPositions.indexOf(customClosePosition) !== -1);
            },
            "allowOffscreen" : function(allowOffscreen) {
                return (typeof allowOffscreen === "boolean");
            }
        }
    };

    function isCloseRegionOnScreen(properties) {
        log.d("isCloseRegionOnScreen");
        log.d("defaultPosition " + defaultPosition.x + " " + defaultPosition.y);
        log.d("offset " + properties.offsetX + " " + properties.offsetY);

        var resizeRect = {};
        resizeRect.x = defaultPosition.x + properties.offsetX;
        resizeRect.y = defaultPosition.y + properties.offsetY;
        resizeRect.width = properties.width;
        resizeRect.height = properties.height;
        printRect("resizeRect", resizeRect);

        var customClosePosition = properties.hasOwnProperty("customClosePosition") ?
                properties.customClosePosition : resizeProperties.customClosePosition;
        log.d("customClosePosition " + customClosePosition);

        var closeRect = { "width": 50, "height": 50 };

        if (customClosePosition.search("left") !== -1) {
            closeRect.x = resizeRect.x;
        } else if (customClosePosition.search("center") !== -1) {
            closeRect.x = resizeRect.x + (resizeRect.width / 2) - 25;
        } else if (customClosePosition.search("right") !== -1) {
            closeRect.x = resizeRect.x + resizeRect.width - 50;
        }

        if (customClosePosition.search("top") !== -1) {
            closeRect.y = resizeRect.y;
        } else if (customClosePosition === "center") {
            closeRect.y = resizeRect.y + (resizeRect.height / 2) - 25;
        } else if (customClosePosition.search("bottom") !== -1) {
            closeRect.y = resizeRect.y + resizeRect.height - 50;
        }

        var maxRect = { "x": 0, "y": 0 };
        maxRect.width = maxSize.width;
        maxRect.height = maxSize.height;

        return isRectContained(maxRect, closeRect);
    }

    function fitResizeViewOnScreen(properties) {
        log.d("fitResizeViewOnScreen");
        log.d("defaultPosition " + defaultPosition.x + " " + defaultPosition.y);
        log.d("offset " + properties.offsetX + " " + properties.offsetY);

        var resizeRect = {};
        resizeRect.x = defaultPosition.x + properties.offsetX;
        resizeRect.y = defaultPosition.y + properties.offsetY;
        resizeRect.width = properties.width;
        resizeRect.height = properties.height;
        printRect("resizeRect", resizeRect);

        var maxRect = { "x": 0, "y": 0 };
        maxRect.width = maxSize.width;
        maxRect.height = maxSize.height;

        var adjustments = { "x": 0, "y": 0 };

        if (isRectContained(maxRect, resizeRect)) {
            log.d("no adjustment necessary");
            return adjustments;
        }

        if (resizeRect.x < maxRect.x) {
            adjustments.x = maxRect.x - resizeRect.x;
        } else if ((resizeRect.x + resizeRect.width) > (maxRect.x + maxRect.width)) {
            adjustments.x = (maxRect.x + maxRect.width) - (resizeRect.x + resizeRect.width);
        }
        log.d("adjustments.x " + adjustments.x);

        if (resizeRect.y < maxRect.y) {
            adjustments.y = maxRect.y - resizeRect.y;
        } else if ((resizeRect.y + resizeRect.height) > (maxRect.y + maxRect.height)) {
            adjustments.y = (maxRect.y + maxRect.height) - (resizeRect.y + resizeRect.height);
        }
        log.d("adjustments.y " + adjustments.y);

        resizeRect.x = defaultPosition.x + properties.offsetX + adjustments.x;
        resizeRect.y = defaultPosition.y + properties.offsetY + adjustments.y;
        printRect("adjusted resizeRect", resizeRect);

        return adjustments;
    }

    function isRectContained(containingRect, containedRect) {
        log.d("isRectContained");
        printRect("containingRect", containingRect);
        printRect("containedRect", containedRect);
        return (containedRect.x >= containingRect.x &&
            (containedRect.x + containedRect.width) <= (containingRect.x + containingRect.width) &&
            containedRect.y >= containingRect.y &&
            (containedRect.y + containedRect.height) <= (containingRect.y + containingRect.height));
    }

    function printRect(label, rect) {
        log.d(label +
            " [" + rect.x + "," + rect.y + "]" +
            ",[" + (rect.x + rect.width) + "," + (rect.y + rect.height) + "]" +
            " (" + rect.width + "x" + rect.height + ")");
    }

  mraid.resize = function() {
    log.i("mraid.resize");
  		// The only time it is valid to call resize is when the ad is
  		// a banner currently in either default or resized state.
  		// Trigger an error if the current state is expanded.
  		if (placementType === PLACEMENT_TYPES.INTERSTITIAL || state === STATES.LOADING || state === STATES.HIDDEN) {
  			// do nothing
  			return;
  		}
  		if (state === STATES.EXPANDED) {
  			mraidbridge.fireErrorEvent("mraid.resize called when ad is in expanded state", "mraid.resize");
  			return;
  		}
  		if (!isResizeReady) {
  			mraidbridge.fireErrorEvent("mraid.resize is not ready to be called", "mraid.resize");
  			return;
  		}
    bridge.executeNativeCall('resize');
  };

  mraid.getResizeProperties = function() {
    return resizeProperties;
  };

  mraid.getCurrentPosition = function() {
    return currentPosition;
  };

  mraid.getDefaultPosition = function() {
  		return defaultPosition;
  };

  mraid.getMaxSize = function() {
  		return maxSize;
  };

  mraid.getScreenSize = function() {
 		return screenSize;
  };

  mraid.setCurrentPosition = function(x, y, width, height) {
    		var previousSize = {};
    		previousSize.width = currentPosition.width;
    		previousSize.height = currentPosition.height;

    		currentPosition.x = x;
    		currentPosition.y = y;
    		currentPosition.width = width;
    		currentPosition.height = height;

    		if (width !== previousSize.width || height !== previousSize.height) {
    			mraid.fireSizeChangeEvent(width, height);
    		}
   };

   mraid.setDefaultPosition = function(x, y, width, height) {
    		defaultPosition.x = x;
    		defaultPosition.y = y;
    		defaultPosition.width = width;
    		defaultPosition.height = height;
   };

   mraid.setMaxSize = function(width, height) {
    		maxSize.width = width;
    		maxSize.height = height;
    };

    mraid.setScreenSize = function(width, height) {
    		screenSize.width = width;
    		screenSize.height = height;
    		if (!isExpandPropertiesSet) {
    			expandProperties.width = width;
    			expandProperties.height = height;;
    		}
    };

    mraid.fireSizeChangeEvent = function(width, height) {
    	if (state !== mraid.STATES.LOADING) {
    		mraidbridge.fireEvent(mraid.EVENTS.SIZECHANGE, width, height);
    	}
    };

  var CalendarEventParser = {
    initialize: function(parameters) {
      this.parameters = parameters;
      this.errors = [];
      this.arguments = ['createCalendarEvent'];
    },

    parse: function() {
      if (!this.parameters) {
        this.errors.push('The object passed to createCalendarEvent cannot be null.');
      } else {
        this.parseDescription();
        this.parseLocation();
        this.parseSummary();
        this.parseStartAndEndDates();
        this.parseReminder();
        this.parseRecurrence();
        this.parseTransparency();
      }

      var errorCount = this.errors.length;
      if (errorCount) {
        this.arguments.length = 0;
      }

      return (errorCount === 0);
    },

    parseDescription: function() {
      this._processStringValue('description');
    },

    parseLocation: function() {
      this._processStringValue('location');
    },

    parseSummary: function() {
      this._processStringValue('summary');
    },

    parseStartAndEndDates: function() {
      this._processDateValue('start');
      this._processDateValue('end');
    },

    parseReminder: function() {
      var reminder = this._getParameter('reminder');
      if (!reminder) {
        return;
      }

      if (reminder < 0) {
        this.arguments.push('relativeReminder');
        this.arguments.push(parseInt(reminder) / 1000);
      } else {
        this.arguments.push('absoluteReminder');
        this.arguments.push(reminder);
      }
    },

    parseRecurrence: function() {
      var recurrenceDict = this._getParameter('recurrence');
      if (!recurrenceDict) {
        return;
      }

      this.parseRecurrenceInterval(recurrenceDict);
      this.parseRecurrenceFrequency(recurrenceDict);
      this.parseRecurrenceEndDate(recurrenceDict);
      this.parseRecurrenceArrayValue(recurrenceDict, 'daysInWeek');
      this.parseRecurrenceArrayValue(recurrenceDict, 'daysInMonth');
      this.parseRecurrenceArrayValue(recurrenceDict, 'daysInYear');
      this.parseRecurrenceArrayValue(recurrenceDict, 'monthsInYear');
    },

    parseTransparency: function() {
      var validValues = ['opaque', 'transparent'];

      if (this.parameters.hasOwnProperty('transparency')) {
        var transparency = this.parameters['transparency'];
        if (contains(transparency, validValues)) {
          this.arguments.push('transparency');
          this.arguments.push(transparency);
        } else {
          this.errors.push('transparency must be opaque or transparent');
        }
      }
    },

    parseRecurrenceArrayValue: function(recurrenceDict, kind) {
      if (recurrenceDict.hasOwnProperty(kind)) {
        var array = recurrenceDict[kind];
        if (!array || !(array instanceof Array)) {
          this.errors.push(kind + ' must be an array.');
        } else {
          var arrayStr = array.join(',');
          this.arguments.push(kind);
          this.arguments.push(arrayStr);
        }
      }
    },

    parseRecurrenceInterval: function(recurrenceDict) {
      if (recurrenceDict.hasOwnProperty('interval')) {
        var interval = recurrenceDict['interval'];
        if (!interval) {
          this.errors.push('Recurrence interval cannot be null.');
        } else {
          this.arguments.push('interval');
          this.arguments.push(interval);
        }
      } else {
        // If a recurrence rule was specified without an interval, use a default value of 1.
        this.arguments.push('interval');
        this.arguments.push(1);
      }
    },

    parseRecurrenceFrequency: function(recurrenceDict) {
      if (recurrenceDict.hasOwnProperty('frequency')) {
        var frequency = recurrenceDict['frequency'];
        var validFrequencies = ['daily', 'weekly', 'monthly', 'yearly'];
        if (contains(frequency, validFrequencies)) {
          this.arguments.push('frequency');
          this.arguments.push(frequency);
        } else {
          this.errors.push('Recurrence frequency must be one of: "daily", "weekly", "monthly", "yearly".');
        }
      }
    },

    parseRecurrenceEndDate: function(recurrenceDict) {
      var expires = recurrenceDict['expires'];

      if (!expires) {
        return;
      }

      this.arguments.push('expires');
      this.arguments.push(expires);
    },

    _getParameter: function(key) {
      if (this.parameters.hasOwnProperty(key)) {
        return this.parameters[key];
      }

      return null;
    },

    _processStringValue: function(kind) {
      if (this.parameters.hasOwnProperty(kind)) {
        var value = this.parameters[kind];
        this.arguments.push(kind);
        this.arguments.push(value);
      }
    },

    _processDateValue: function(kind) {
      if (this.parameters.hasOwnProperty(kind)) {
        var dateString = this._getParameter(kind);
        this.arguments.push(kind);
        this.arguments.push(dateString);
      }
    },
  };
}());