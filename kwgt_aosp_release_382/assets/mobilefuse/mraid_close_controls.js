"use strict";
function _typeof(o) { "@babel/helpers - typeof"; return _typeof = "function" == typeof Symbol && "symbol" == typeof Symbol.iterator ? function (o) { return typeof o; } : function (o) { return o && "function" == typeof Symbol && o.constructor === Symbol && o !== Symbol.prototype ? "symbol" : typeof o; }, _typeof(o); }
function _classCallCheck(a, n) { if (!(a instanceof n)) throw new TypeError("Cannot call a class as a function"); }
function _defineProperties(e, r) { for (var t = 0; t < r.length; t++) { var o = r[t]; o.enumerable = o.enumerable || !1, o.configurable = !0, "value" in o && (o.writable = !0), Object.defineProperty(e, _toPropertyKey(o.key), o); } }
function _createClass(e, r, t) { return r && _defineProperties(e.prototype, r), t && _defineProperties(e, t), Object.defineProperty(e, "prototype", { writable: !1 }), e; }
function _toPropertyKey(t) { var i = _toPrimitive(t, "string"); return "symbol" == _typeof(i) ? i : i + ""; }
function _toPrimitive(t, r) { if ("object" != _typeof(t) || !t) return t; var e = t[Symbol.toPrimitive]; if (void 0 !== e) { var i = e.call(t, r || "default"); if ("object" != _typeof(i)) return i; throw new TypeError("@@toPrimitive must return a primitive value."); } return ("string" === r ? String : Number)(t); }

var CloseButton = /*#__PURE__*/function () {
  function CloseButton(button) {
    _classCallCheck(this, CloseButton);

    this.isClosable = false;
    this.isVisible = false;
    this.countdownMode = true;
    this.delaySeconds = -1;
    this.button = button;
    this.spinSvg = this.button.querySelector("#c_p");
    this.closeSvg = this.button.querySelector("#c_x");
    this.countdownDiv = document.createElement("div");
    this.button.innerHTML = '';
    this.button.style.opacity = 0;
  }

  return _createClass(CloseButton, [{
    key: "setVisible",
    value: function setVisible() {
      if (this.isVisible) {
        return;
      }

      this.isVisible = true;
      this.button.style.visibility = "visible";
      this.button.style.opacity = 1;
    }
  }, {
    key: "setDelaySeconds",
    value: function setDelaySeconds(delaySeconds) {
      this.delaySeconds = delaySeconds;
    }
  }, {
    key: "setCountdownMode",
    value: function setCountdownMode(countdownMode) {
      this.countdownMode = countdownMode;
    }
  }, {
    key: "show",
    value: function show(delaySeconds) {
      this.delaySeconds = delaySeconds;

      if (this.isClosable) {
        return;
      }

      if (this.delaySeconds == -1) {
        return;
      }

      if (this.countdownMode) {
        this.setVisible();
      }

      this.setDelaySeconds(delaySeconds);
      this.updateButtonState(this.delaySeconds);
      this.startTimer();
    }
  }, {
    key: "setClosable",
    value: function setClosable() {
      this.isClosable = true;
      this.updateButtonState(0);
      this.setVisible();
    }
  }, {
    key: "startTimer",
    value: function startTimer() {
      var _this = this;

      var remainingSeconds = this.delaySeconds;
      var remainingMs = this.delaySeconds * 1000;
      this.timerInterval = setInterval(function () {
        remainingSeconds--;

        if (remainingSeconds <= 0) {
          _this.clearTimer();

          _this.updateButtonState(0, 1);

          _this.setClosable();
        } else {
          _this.updateButtonState(remainingSeconds);
        }
      }, 1000);
      this.timerInterval2 = setInterval(function () {
        remainingMs -= 50;

        _this.updateFractionalGraphic(1 - remainingMs / 1000 / _this.delaySeconds);
      }, 50);
    }
  }, {
    key: "updateButtonState",
    value: function updateButtonState(remainingSeconds) {
      while (this.button.firstChild) {
        this.button.removeChild(this.button.lastChild);
      }

      if (remainingSeconds === 0) {
        this.button.appendChild(this.closeSvg);
      } else {
        this.countdownDiv.innerText = "".concat(remainingSeconds);
        this.button.appendChild(this.spinSvg);
        this.button.appendChild(this.countdownDiv);
      }
    }
  }, {
    key: "updateFractionalGraphic",
    value: function updateFractionalGraphic(percent) {
      this.spinSvg.querySelector('.pc-prog').style.strokeDasharray = (percent * 88).toFixed(0) + ' 88';
    }
  }, {
    key: "clearTimer",
    value: function clearTimer() {
      clearInterval(this.timerInterval);
      clearInterval(this.timerInterval2);
    }
  },
   {
       key: "setThumbnailMode",
       value: function setThumbnailMode(thumbnailMode) {
         if (thumbnailMode) {
           this.enterThumbnailMode();
         } else {
           this.exitThumbnailMode();
         }
       }
   },
   {
    key: "enterThumbnailMode",
    value: function enterThumbnailMode() {
      this.nonThumbnailTop = this.button.style.top;
      this.nonThumbnailRight = this.button.style.right;
      this.button.style.top = '4px';
      this.button.style.right = '4px';
    }
  }, {
    key: "exitThumbnailMode",
    value: function exitThumbnailMode() {
      if (typeof this.nonThumbnailTop === 'undefined') {
        return;
      }

      this.button.style.top = this.nonThumbnailTop;
      this.button.style.right = this.nonThumbnailRight;
    }
  }]);
}();