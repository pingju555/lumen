// mraid.js
// Copyright 2021 MobileFuse LLC

class EventListener {
    constructor(event) {
        this.event = event;
        this.listeners = {};
    }

    add(callback) {
        const id = String(callback);
        this.listeners[id] = callback;
    }

    remove(callback) {
        const id = String(callback);
        if(this.listeners[id]) {
            delete this.listeners[id];
            return true;
        }
        return false;
    }

    removeAll() {
        this.listeners = {};
    }

    invoke(scope, args) {
        for(const callbackId in this.listeners){
            if( ! this.listeners.hasOwnProperty(callbackId)){
                continue;
            }
            this.listeners[callbackId].apply(scope, args);
        }
    }
}

class NativeBridge {
    constructor(mraid) {
        this.mraid = mraid;
        this.queue = [];
        this.requestInFlight = false;
    }

    call(method, args){
        let callUri = "mraid://" + method;
        if(args){
            callUri += "?" + this._buildQueryString(args);
        }

        this.queue.push(callUri);

        if( ! this.requestInFlight){
            this._next();
        }
    }

    _buildQueryString(obj) {
        const str = [];
        for (let p in obj) {
            if (obj.hasOwnProperty(p)) {
                str.push(encodeURIComponent(p) + "=" + encodeURIComponent(obj[p]));
            }
        }
        return str.join("&");
    }

    _next() {
        if(this.queue.length === 0 || this.requestInFlight){
            return;
        }
        const call = this.queue.shift();
        this.requestInFlight = true;
        this.mraid._debug("Bridge, calling native " + call);

        // This check will only succeed on iOS
        if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.mfusesdk) {
            // Notify iOS layer
            window.webkit.messageHandlers.mfusesdk.postMessage("mraidBridgeCall:" + call);
        } else {
            // Notify Android layer
            var xhr = new XMLHttpRequest();
            const url = call.replace("mraid://", "https://sdk-webview.mobilefuse.com/mraid/");
            xhr.open('GET', url, true);
            xhr.send();
        }
    }

    nativeCallComplete() {
        if( ! this.requestInFlight) return;
        this.requestInFlight = false;
        this._next();
    }

    // Interface methods for the native layer:

    init(appDetails){
        this.call("initBridge", appDetails);
    }

    setCurrentPosition(properties){
        console.log("[mraid] :: setCurrentPosition("+JSON.stringify(properties)+")");
        this.mraid.currentPosition = this.mraid._assignProperties(properties, this.mraid.currentPosition);
    }

    setCurrentAppOrientation(properties){
        console.log("[mraid] :: setCurrentAppOrientation("+JSON.stringify(properties)+")");
        this.mraid.currentAppOrientation = this.mraid._assignProperties(properties, this.mraid.currentAppOrientation);
    }

    setDefaultPosition(properties){
        console.log("[mraid] :: setDefaultPosition("+JSON.stringify(properties)+ ")");
        this.mraid.defaultPosition = this.mraid._assignProperties(properties, this.mraid.defaultPosition);
    }

    setMaxSize(properties){
        this.mraid.maxSize = this.mraid._assignProperties(properties, this.mraid.maxSize);
    }

    setPlacementType(placementType) {
        this.mraid.placementType = placementType;
    }

    setScreenSize(properties){
        console.log("[mraid] :: setScreenSize("+JSON.stringify(properties)+")");
        this.mraid.screenSize = this.mraid._assignProperties(properties, this.mraid.screenSize);
    }

    setState(state){
        this.mraid.state = state;
        this.mraid._broadcastEvent(Mraid.Event.STATE_CHANGE, state);
    }

    setIsViewable(isViewable){
        if (isViewable === this.mraid.viewable) return;
        this.mraid.viewable = isViewable;
        this.mraid._broadcastEvent(Mraid.Event.VIEWABLE_CHANGE, isViewable);
    }

    setExposureChange(exposurePercent, visibleRectangle, occlusionRectangles){
        this.mraid._broadcastEvent(Mraid.Event.EXPOSURE_CHANGE, exposurePercent, visibleRectangle, occlusionRectangles);
    }

    setSupports(supports){
        if (!supports) {
            this._broadcastEvent(Mraid.Event.ERROR, 'supports argument is not defined', 'setSupports');
        }
        this.mraid.supportProperties = this.mraid._assignProperties(supports, this.mraid.supportProperties);
    }

    setTransparentBackground(transparent){
        this.mraid.transparentBackground = transparent;
        this.mraid._broadcastEvent(Mraid.Event.TRANSPARENT_BG_CHANGE, transparent);
    }

    notifyReadyEvent() {
        this.mraid._broadcastEvent(Mraid.Event.READY);
    }

    notifySizeChangeEvent(width, height) {
        this.mraid._broadcastEvent(Mraid.Event.SIZE_CHANGE, width, height);
    }

    notifyStateChangeEvent() {
        this.mraid._broadcastEvent(Mraid.Event.STATE_CHANGE, this.mraid.state);
    }

    notifyViewableChangeEvent() {
        this.mraid._broadcastEvent(Mraid.Event.VIEWABLE_CHANGE, this.mraid.isViewable());
    }

    // Platform-specific tools
    isIOS() {
        return navigator.platform.substr(0, 2) === 'iP'; // iPhone, iPad, iPod
    }

    setLocation(location) {
        this.mraid.locationProperties = this.mraid._assignProperties(location, this.mraid.locationProperties);
     }
}

class SplashAd {

    constructor(bridge) {
        this.bridge = bridge;
    }

    transition() {
        this.bridge.call('splashAdTransition');
    }

    expand() {
        this.bridge.call('splashAdExpand');
    }
}

class Mraid {
    constructor(options) {
        options = options || {};
        this.VERSION = "3.0";
        this.isMobileFuseSdk = true;
        this.DEBUG = options.DEBUG || false;
        this.bridge = new NativeBridge(this);


        // Internal mraid state:
        this.state = Mraid.State.LOADING;
        this.viewable = false;
        this.eventListeners = {};
        this.placementType = Mraid.PlacementType.UNKNOWN;

        // Our mraid properties:

        this.expandProperties = {
            width: false,
            height: false,
            useCustomClose: false,
            isModal: true,
        };

        this.resizeProperties = {
            width: false,
            height: false,
            offsetX: false,
            offsetY: false,
            customClosePosition: 'top-right',
            allowOffscreen: true,
        };

        this.orientationProperties = {
            allowOrientationChange: true,
            forceOrientation: "none",
        };

        this.supportProperties = {
            sms: false,
            tel: false,
            calendar: false,
            storePicture: false,
            inlineVideo: false,
            vpaid: false,
            location: false, // Updated by the SDK based on whether location permission is available
            // Extensions:
            ar: false,
            barometricPressure: false,
            transparentBackground: true,
            splashAd: true,
            productStoreView: false,
            overlay: false,
        };

        this.locationProperties = {
            lat: 0,
            lon: 0,
            type: Mraid.LocationType.INVALID,
            accuracy: -1,
            lastfix: -1
        };

        // State:
        this.maxSize = { width: 0, height: 0 };
        this.currentPosition = { x:0, y:0, width:0, height:0 };
        this.currentAppOrientation = { orientation: "none", locked: false };
        this.defaultPosition = { x:0, y:0, width:0, height:0 };
        this.screenSize = { width: 0, height: 0 };

        this.transparentBackground = false;

        // SplashAd
        this.splashAd = new SplashAd(this.bridge);

    }

    static Event = {
        ERROR:               'error',
        READY:               'ready',
        SIZE_CHANGE:         'sizeChange',
        STATE_CHANGE:        'stateChange',
        EXPOSURE_CHANGE:     'exposureChange',
        AUDIO_VOLUME_CHANGE: 'audioVolumeChange',

        VIEWABLE_CHANGE:     'viewableChange', // Deprecated
        DEBUG_LOG:           'debugLog',       // Extension
    };

    static LocationType = {
        INVALID:        0,
        GPS:            1,
        IP_ADDRESS:     2,
        USER_PROVIDED:  3
    };

    static State = {
        LOADING:  'loading',
        DEFAULT:  'default',
        EXPANDED: 'expanded',
        HIDDEN:   'hidden',
        RESIZED:  'resized'
    };

    static PlacementType = {
        UNKNOWN:      'unknown',
        INLINE:       'inline',
        INTERSTITIAL: 'interstitial'
    };

    // Private/Helper methods:
    _broadcastEvent() {
        const args = [...arguments];
        const event = args.shift();

        if (this.eventListeners[event]) {
            this.eventListeners[event].invoke(this, args);
        }

        if(event !== Mraid.Event.DEBUG_LOG) {
            this._debug("_broadcastEvent", event, args);
        }
    }

    _debug() {
        if(!this.DEBUG){
            return;
        }
        let logOutput = [];
        for(let arg of arguments){
            if(arg === null) {
                continue;
            }
            if(typeof arg !== 'object') {
                logOutput.push(arg.toString());
                continue;
            }
            logOutput.push(JSON.stringify(arg));
        }

        if(!this.bridge.isIOS()) { // On iOS, this causes a log loop
            console.log("[mraid.js] " + logOutput.join(' '));
        }
        this._broadcastEvent(Mraid.Event.DEBUG_LOG, logOutput.join(' '));
    }

    _assignProperties(from, to, properties){
        if(!properties) properties = Object.keys(to);
        const obj = {...to};
        for(const p of properties){
            if(from.hasOwnProperty(p)){
                obj[p] = from[p];
            }
        }
        return obj;
    }

    // Core MRAID 3.0 interface:
    // See: https://www.iab.com/wp-content/uploads/2017/07/MRAID_3.0_FINAL.pdf
    // Section 2.3 - Interface, Page 14

    getVersion() {
        if (!this.VERSION) {
            this._broadcastEvent(Mraid.Event.ERROR, 'VERSION is not defined', 'getVersion');
        }
        return this.VERSION;
    }

    addEventListener(event, listener) {
        if (!listener) {
            this._broadcastEvent(Mraid.Event.ERROR, 'listener argument is not defined', 'addEventListener');
            return;
        }
        if (!event) {
             this._broadcastEvent(Mraid.Event.ERROR, 'event argument is not defined', 'addEventListener');
             return;
        }
        if (!this.eventListeners[event]) {
            this.eventListeners[event] = new EventListener(event);
        }
        this.eventListeners[event].add(listener);
        this.bridge.call('eventListenerWasAdded', {
            event: event
        });
    }

    removeEventListener(event, listener) {
        if (!event) {
            this._broadcastEvent(Mraid.Event.ERROR, 'event argument is not defined', 'removeEventListener');
            return;
        }

        if (listener) {
            // If we have a valid event, we'll try to remove the listener from it.
            let success = false;
            if (this.eventListeners[event]) {
                success = this.eventListeners[event].remove(listener);
            }

            // If we didn't have a valid event or couldn't remove the listener from the event, broadcast an error and return early.
            if (!success) {
                this._broadcastEvent(Mraid.Event.ERROR, 'removing event listener failed: there was no listener registered for the specified event', 'removeEventListener');
            }
        } else if (!listener && this.eventListeners[event]) {
            this.eventListeners[event].removeAll();
        }
    }

    open(url) {
        if (!url){
            this._broadcastEvent(Mraid.Event.ERROR, 'url is not defined', 'open');
        } else {
            this.bridge.call('open', {url: url});
        }
    }

    close() {
        if (this.state === Mraid.State.HIDDEN) {
            this._broadcastEvent(Mraid.Event.ERROR, 'close failed: ad cannot be closed when it is already hidden', 'close');
        } else {
            this.bridge.call('close');
        }
    }

    // Deprecated
    useCustomClose(shouldUseCustomClose) {
        this.expandProperties.useCustomClose = shouldUseCustomClose;
        this.bridge.call('useCustomClose', {shouldUseCustomClose:shouldUseCustomClose});
    }

    unload() {
        console.log("[mraid] :: unload");
        this.bridge.call('unload');
    }

    expand(url) {
        if (!(this.state === Mraid.State.DEFAULT || this.state === Mraid.State.RESIZED)) {
            this._broadcastEvent(Mraid.Event.ERROR, 'expand failed: ad can only be expanded from the default or resized state', 'expand');
            return;
        }

        var args = {...this.expandProperties};
        args.allowOrientationChange = this.orientationProperties.allowOrientationChange;
        args.forceOrientation = this.orientationProperties.forceOrientation;

//        args.allowOrientationChange = true;
//        args.forceOrientation = "landscape";

        if (url) {
            args.url = url;
        }

        this.bridge.call('expand', args);
    }

    // Deprecated
    isViewable() {
        if (this.viewable === null || this.viewable === undefined) {
            this._broadcastEvent(Mraid.Event.ERROR, 'viewable is not defined', 'isViewable');
        }
        return this.viewable;
    }

    playVideo(uri) {
        if (!mraid.isViewable()) {
            this._broadcastEvent(Mraid.Event.ERROR, 'playVideo cannot be called until the ad is viewable', 'playVideo');
            return;
        }

        if (!uri) {
            this._broadcastEvent(Mraid.Event.ERROR, 'playVideo must be called with a valid URI', 'playVideo');
        } else {
            this.bridge.call('playVideo', {uri: uri});
        }
    }

    resize() {
        if (!(this.getState() === Mraid.State.DEFAULT || this.getState() === Mraid.State.RESIZED)) {
            this._broadcastEvent(Mraid.Event.ERROR, 'resize failed: ad can only be resized from the default or resized state', 'resize');
        } else if (!this.resizeProperties.width || !this.resizeProperties.height) {
            this._broadcastEvent(Mraid.Event.ERROR, 'resize failed: must set resize properties before calling resize()', 'resize');
        } else {
            this.bridge.call('resize', this.resizeProperties);
        }
    }

    storePicture(uri) {
        if (!mraid.isViewable()) {
            this._broadcastEvent(Mraid.Event.ERROR, 'storePicture cannot be called until the ad is viewable', 'storePicture');
            return;
        }

        if (!uri) {
            this._broadcastEvent(Mraid.Event.ERROR, 'storePicture must be called with a valid URI', 'storePicture');
        } else {
            this.bridge.call('storePicture', {uri: uri});
        }
    }

    setTransparentBackground(transparent) {
        console.log("[mraid Transparent Interstitial] :: setTransparentBackground("+transparent+")");
        this.bridge.call('setTransparentBackground', { transparent: transparent } );
    }

    createCalendarEvent(parameters) {
//        throw "Not implemented :: createCalendarEvent()";
        if (!mraid.isViewable()) {
            this._broadcastEvent(Mraid.Event.ERROR, 'createCalendarEvent cannot be called until the ad is viewable', 'createCalendarEvent');
            return;
        }

        if (!parameters) {
            this._broadcastEvent(Mraid.Event.ERROR, 'createCalendarEvent must be called with a valid parameters', 'createCalendarEvent');
        } else {
            this.bridge.call('createCalendarEvent', { details: JSON.stringify(parameters) } );
        }
    }

    // VPAID --
    initVpaid(vpaidObject) {
        throw "Not implemented :: initVpaid()";
    }

    // Properties --

    supports(feature) {
        return this.supportProperties[feature];
    }

    getPlacementType() {
        if (!this.placementType || this.placementType === Mraid.PlacementType.UNKNOWN) {
            this._broadcastEvent(Mraid.Event.ERROR, 'placementType is not defined', 'getPlacementType');
        }
        return this.placementType;
    }

    getOrientationProperties() {
        console.log("[mraid] :: getOrientationProperties()");
        return {...this.orientationProperties};
    }

    setOrientationProperties(properties) {
        console.log("[mraid] :: setOrientationProperties("+JSON.stringify(properties)+")");

        if ( this.getState() === Mraid.State.LOADING ) {
            this._broadcastEvent(Mraid.Event.ERROR, "setting orientation properties failed: setOrientationProperties cannot be called during the loading state", 'setOrientationProperties');
            return;
        }

        if (typeof properties === "undefined") {
            this._broadcastEvent(Mraid.Event.ERROR, "properties argument is not defined", 'setOrientationProperties');
            return;
        }

        if (!this.getCurrentAppOrientation().locked) {
            if (properties.forceOrientation === 'portrait' || properties.forceOrientation === 'landscape' || properties.forceOrientation === 'none') {
                this.orientationProperties.forceOrientation = properties.forceOrientation;
            } else {
                this._broadcastEvent(Mraid.Event.ERROR, "forceOrientation property was ignored because it had an unsupported value", 'setOrientationProperties');
            }
        }

        if (typeof properties.allowOrientationChange === "boolean") {
            this.orientationProperties.allowOrientationChange = properties.allowOrientationChange;
        } else {
            this._broadcastEvent(Mraid.Event.ERROR, "allowOrientationChange property was ignored because it was not a boolean value", 'setOrientationProperties');
        }

        /*this.orientationProperties = this._assignProperties(
            properties,
            this.orientationProperties
        );*/
        this.bridge.call('setOrientationProperties', this.orientationProperties);
    }

    getCurrentAppOrientation() {
        console.log("[mraid] :: getCurrentAppOrientation()");
        return {...this.currentAppOrientation};
    }

    getCurrentPosition() {
        if (!this.currentPosition) {
            this._broadcastEvent(Mraid.Event.ERROR, 'currentPosition is not defined', 'getCurrentPosition');
        }
        return {...this.currentPosition};
    }

    getDefaultPosition() {
        if (!this.defaultPosition) {
            this._broadcastEvent(Mraid.Event.ERROR, 'defaultPosition is not defined', 'getDefaultPosition');
        }
        return {...this.defaultPosition};
    }

    getState() {
        if (!this.state) {
            this._broadcastEvent(Mraid.Event.ERROR, 'state is not defined', 'getState');
        }
        return this.state;
    }

    getExpandProperties() {
        if (!this.expandProperties) {
            this._broadcastEvent(Mraid.Event.ERROR, 'expandProperties is not defined', 'getExpandProperties');
        }
        return {...this.expandProperties};
    }

    setExpandProperties(properties) {
        if (!this.expandProperties) {
            this._broadcastEvent(Mraid.Event.ERROR, 'expandProperties is not defined', 'setExpandProperties');
        }
        if (!properties) {
            this._broadcastEvent(Mraid.Event.ERROR, 'properties argument is not defined', 'setExpandProperties');
        }
        this.expandProperties = this._assignProperties(
            properties,
            this.expandProperties
        );
    }

    getMaxSize() {
        console.log("[mraid] :: getMaxSize()" +JSON.stringify(this.maxSize));
        if (!this.maxSize) {
            this._broadcastEvent(Mraid.Event.ERROR, 'maxSize is not defined', 'getMaxSize');
        }
        return {...this.maxSize};
    }

    getScreenSize() {
        console.log("[mraid] :: getScreenSize() "+JSON.stringify(this.screenSize));
        if (!this.screenSize) {
            this._broadcastEvent(Mraid.Event.ERROR, 'screenSize is not defined', 'getScreenSize');
        }
        return {...this.screenSize};
    }

    getResizeProperties() {
        return {...this.resizeProperties};
    }

    setResizeProperties(properties) {
        this._assignProperties(properties, this.resizeProperties);
    }

    getLocation() {
        const location = {...this.locationProperties};
        if (location.lastfix instanceof Date) {
            // 'lastfix' should be number of seconds since the last GPS fix:
            location.lastfix = (new Date() - location.lastfix)/1000;
        }
        if (location.lat === 0 && location.lon === 0) {
            this._broadcastEvent(Mraid.Event.ERROR, '-1', 'getLocation')
        }
        return location;
    }

    showOverlay(properties) {
        console.log("[mraid] :: showOverlay(" + JSON.stringify(properties) + ")");
        this.bridge.call('showOverlay',{ overlay: JSON.stringify(properties) });
    }

    hideOverlay() {
        this.bridge.call('hideOverlay');
    }

    showProductStoreView() {
        // Only for iOS
        this.bridge.call('showProductStoreView');
    }
}

if (!window.mraid || !window.mraid.isMobileFuseSdk){
    const mraid = window.mraid = new Mraid({
        DEBUG: MRAID_ENV.debug || false
    });
}

// On iOS, override window.open - this allows window.open clickthroughs to work
if(window.mraid.bridge.isIOS()) {
	const mfMraidOriginalWindowOpen = window.open;
	window.open = function (url, target, features) {
    	if (typeof mraid !== 'undefined' && typeof mraid.open === 'function') {
        	mraid.open(url);
    	} else {
        	// Fall back to the original window.open if mraid.open is not available
        	return mfMraidOriginalWindowOpen.call(window, url, target, features);
    	}
	};
}
