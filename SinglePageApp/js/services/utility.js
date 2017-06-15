'use strict';

module.exports = function(){


    this.getParameterByName = function(name, url) {
        if (!url) url = window.location.href;
        name = name.replace(/[\[\]]/g, "\\$&");
        var regex = new RegExp("[?&]" + name + "(=([^&#]*)|&|#|$)", "i"),
            results = regex.exec(url);
        if (!results) return null;
        if (!results[2]) return '';
        return decodeURIComponent(results[2].replace(/\+/g, " "));
    }

    this.guid = function () {
        function s4() {
            return Math.floor((1 + Math.random()) * 0x10000)
              .toString(16)
              .substring(1);
        }
        return s4() + s4() + '-' + s4() + '-' + s4() + '-' +
          s4() + '-' + s4() + s4() + s4();
    }

    this.redirectUri = function() {
        var url = window.location.protocol + "//" + window.location.hostname;
        if (window.location.port != "")
            url += ":" + window.location.port;
        return url;
    }

    this.makeUniqueInverseTick = function(prefix, maxLength) {
        var seed = new Date;
        var seedEpoch = seed.getTime() - seed.getMilliseconds();
        var ticksPerMillisecond = 10000;
        var seedTicks = seedEpoch * ticksPerMillisecond;        
        var maxDate = new Date("12/31/9999 11:59:59 PM");
        var maxDateEpoch = maxDate.getTime() - maxDate.getMilliseconds();
        var maxTicks = maxDateEpoch * ticksPerMillisecond;
        var inverseTick = maxTicks - seedTicks;
        var value = prefix + inverseTick + this.guid() + this.guid();
        value = value.replace(/-/g, "");
        value = value.substring(0, maxLength);
        return value;
    }

}