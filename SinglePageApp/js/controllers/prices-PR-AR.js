
module.exports = function($scope, $location, $route, $routeParams, UtilityService) {
 
    var url = "https://spot.octank.biz/api/prices" 
    var sortKey = $routeParams.PR;
    if (sortKey)
    {
        sortKey = sortKey.replace(/\|/g, "/");
        url += "/" + sortKey;
    }
        
  var xhttp = new XMLHttpRequest();
  xhttp.onreadystatechange = function() {
    if (this.readyState == 4 && this.status == 200) {
        $scope.resp = JSON.parse(this.responseText);
        loadTable();

        $scope.codes = $scope.resp.PA.split('|');
        $scope.names = {};

        var i = 0;
        var path = "/#!/prices/";
        for(var code in $scope.resp.NM){
            path += $scope.codes[i] + "|";
            $scope.names[$scope.codes[i]] = { Path: path, Name: $scope.resp.NM[code] };
            i++;
        }       

        if ($scope.resp.PA)
            $scope.resp.PA += "|";

        $scope.$apply();
    }
  };
  xhttp.open("GET", url, true);
  xhttp.send();

  anonymousAuthentication();

  function loadTable(){

    console.log("Loading table...");

    $scope.labels = []; // Instace Types
    $scope.series = []; // AZs
    $scope.data = [];
    $scope.resp.IT.forEach(function(item){
        var label = item.FA + item.GE + "." + item.SI;
        if ($scope.labels.indexOf(label) == -1)
            $scope.labels.push(label);
        if ($scope.series.indexOf(item.AZ) == -1)
            $scope.series.push(item.AZ);
    });

    // AZ[InstanceType]

    $scope.series.forEach(function(series) {

        var seriesData = [];

        $scope.resp.IT.forEach(function(item){
            if (series == item.AZ){
                seriesData.push(item.PE);
            }
        });

        $scope.data.push(seriesData); 
    })
  }

    function anonymousAuthentication(){
        AWS.config.getCredentials(function(err) {
            if (err) console.log(err.stack); // credentials not loaded
            else {
                var identityId = AWS.config.credentials.data.IdentityId;
                var iot = new AWS.Iot();
                var params = {
                    policyName: 'SpotPriceAnalyticsSubscriber',
                    principal:  identityId
                };
                iot.attachPrincipalPolicy(params, function(err, data) {
                    if (err) console.log(err, err.stack);
                    else {                  
                        console.log(data);                    
                        iotConnect(AWS.config.credentials, AWS.config.region, 
                            "afczxfromx2vu.iot.us-west-2.amazonaws.com");
                    }
                });
            }
        });
    }

    function iotConnect(credentials, region, host) {

        

        //console.log("TRYNG TO CONNECT TO IOT");

        //var sessionToken = credentials.sessionToken;

        //console.log(AWS.config.credentials.sessionToken);

        // CREATE CANONICAL REQUEST

        var datetime = AWS.util.date.iso8601(new Date()).replace(/[:\-]|\.\d{3}/g, '');
        var date = datetime.substr(0, 8);

        var method = 'GET';
        var protocol = 'wss';
        var uri = '/mqtt';
        var service = 'iotdevicegateway';
        var algorithm = 'AWS4-HMAC-SHA256';

        var credentialScope = date + '/' + region + '/' + service + '/' + 'aws4_request';
        var canonicalQuerystring = 'X-Amz-Algorithm=' + algorithm;
        canonicalQuerystring += '&X-Amz-Credential=' + encodeURIComponent(credentials.accessKeyId + '/' + credentialScope);
        canonicalQuerystring += '&X-Amz-Date=' + datetime;
        canonicalQuerystring += '&X-Amz-SignedHeaders=host';

        var canonicalHeaders = 'host:' + host + '\n';
        var payloadHash = AWS.util.crypto.sha256('', 'hex')
        var canonicalRequest = method + '\n' + uri + '\n' + canonicalQuerystring + '\n' + canonicalHeaders + '\nhost\n' + payloadHash;

        // CREATE STRING TO SIGN

        var stringToSign = algorithm + '\n' + datetime + '\n' + credentialScope + '\n' + AWS.util.crypto.sha256(canonicalRequest, 'hex');
        var signingKey = SigV4Utils.getSignatureKey(credentials.secretAccessKey, date, region, service);
        var signature = AWS.util.crypto.hmac(signingKey, stringToSign, 'hex');

        // 
        canonicalQuerystring += '&X-Amz-Signature=' + signature;
        canonicalQuerystring += '&X-Amz-Security-Token=' + encodeURIComponent(credentials.sessionToken);
        var requestUrl = protocol + '://' + host + uri + '?' + canonicalQuerystring;



        //console.log(requestUrl);

        var clientId = AWS.util.crypto.sha256(credentials.sessionToken, 'hex');

        var client = new Paho.MQTT.Client(requestUrl, clientId);
        var connectOptions = {
            onSuccess: onConnect,
            useSSL: true,
            timeout: 3,
            mqttVersion: 4,
            onFailure: function(err) {
                console.log(err);
            }
        };

        // set callback handlers
        client.onConnectionLost = onConnectionLost;
        client.onMessageArrived = onMessageArrived;


        // connect the client
        client.connect(connectOptions);


        // called when the client connects
        function onConnect() {

            // Once a connection has been made, make a subscription and send a message.
            console.log("Connected to IoT");

            var topicFilter = "prices/" + sortKey + "/#";
            console.log("Subscribing to: " + topicFilter);

            client.subscribe(topicFilter);
        }

        // called when the client loses its connection
        function onConnectionLost(responseObject) {
            if (responseObject.errorCode !== 0) {
                console.log("onConnectionLost:" + responseObject.errorMessage);
            }
        } 

        // called when a message arrives
        function onMessageArrived(message) {
            var observation = JSON.parse(message.payloadString);
            console.log(observation);
   
            var instanceTypeIndex = 0;            
            $scope.labels.forEach(function (instanceType) {
                var seriesIndex = 0;
                $scope.series.forEach(function (serie){
                    //console.log(instanceType, serie, instanceTypeIndex, seriesIndex);
                    var label = observation.FA + observation.GE + "." + observation.SI;
                    if (instanceType == label && serie == observation.AZ){
                        //console.log("About to update: ", observation);

                        $scope.data[instanceTypeIndex][seriesIndex] = observation.PE;

                    }

                    seriesIndex++;
                });
                instanceTypeIndex++;
            });

            $scope.$apply();
        }

    }

// END
};



function SigV4Utils() {}

SigV4Utils.getSignatureKey = function(key, date, region, service) {
    var kDate = AWS.util.crypto.hmac('AWS4' + key, date, 'buffer');
    var kRegion = AWS.util.crypto.hmac(kDate, region, 'buffer');
    var kService = AWS.util.crypto.hmac(kRegion, service, 'buffer');
    var kCredentials = AWS.util.crypto.hmac(kService, 'aws4_request', 'buffer');
    return kCredentials;
};

SigV4Utils.getSignedUrl = function(host, region, credentials) {
    var datetime = AWS.util.date.iso8601(new Date()).replace(/[:\-]|\.\d{3}/g, '');
    var date = datetime.substr(0, 8);

    var method = 'GET';
    var protocol = 'wss';
    var uri = '/mqtt';
    var service = 'iotdevicegateway';
    var algorithm = 'AWS4-HMAC-SHA256';

    var credentialScope = date + '/' + region + '/' + service + '/' + 'aws4_request';
    var canonicalQuerystring = 'X-Amz-Algorithm=' + algorithm;
    canonicalQuerystring += '&X-Amz-Credential=' + encodeURIComponent(credentials.accessKeyId + '/' + credentialScope);
    canonicalQuerystring += '&X-Amz-Date=' + datetime;
    canonicalQuerystring += '&X-Amz-SignedHeaders=host';

    var canonicalHeaders = 'host:' + host + '\n';
    var payloadHash = AWS.util.crypto.sha256('', 'hex')
    var canonicalRequest = method + '\n' + uri + '\n' + canonicalQuerystring + '\n' + canonicalHeaders + '\nhost\n' + payloadHash;

    var stringToSign = algorithm + '\n' + datetime + '\n' + credentialScope + '\n' + AWS.util.crypto.sha256(canonicalRequest, 'hex');
    var signingKey = SigV4Utils.getSignatureKey(credentials.secretAccessKey, date, region, service);
    var signature = AWS.util.crypto.hmac(signingKey, stringToSign, 'hex');

    canonicalQuerystring += '&X-Amz-Signature=' + signature;
    if (credentials.sessionToken) {
        canonicalQuerystring += '&X-Amz-Security-Token=' + encodeURIComponent(credentials.sessionToken);
    }

    var requestUrl = protocol + '://' + host + uri + '?' + canonicalQuerystring;
    return requestUrl;
};

