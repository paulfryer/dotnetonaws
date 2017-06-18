'use strict';
var AWS = require('aws-sdk');
var dynamodb, s3;

require('angular-chart.js');
require('paho-mqtt');

module.exports = function($scope, $location, $route, $routeParams, UtilityService) {
    console.log("inside the prices controller.");



Chart.scaleService.updateScaleDefaults('logarithmic', {
    ticks: {
        min: 0
    }
});

  $scope.series = [];
  $scope.data = [];
  $scope.datasetOverride = [];
  $scope.options = {
  	tooltipTemplate: 'This is a test.',
  	animation : false,
  	scales: {
  		xAxes: [
  			{
  				id: 'first-x-axis',
  				type: 'linear',
  				display: true
  			}
  		],
  		yAxes: [
  			{
  				id: 'first-y-axis',
  				type: 'logarithmic',
  				display: true
  				//position: 'bottom'
  			}
  		]
  	}
  }



    // Initialize the Amazon Cognito credentials provider
	AWS.config.region = 'us-west-2'; // Region
	AWS.config.credentials = new AWS.CognitoIdentityCredentials({
	    IdentityPoolId: 'us-west-2:63111ab5-4710-4ae7-bcb8-a64fad4a9e0b',
	});

	$scope.prices = [];

	$scope.rebuildPricesTable = function(){

		$observations = $scope.prices;
	}

    AWS.config.getCredentials(function(err) {
        if (err) console.log(err.stack); // credentials not loaded
        else {
            console.log("credentials:", AWS.config.credentials);
            
            var identityId = AWS.config.credentials.data.IdentityId;
            console.log(identityId);
    
            // Only need to do this once, so check user data first.
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


        function iotConnect(credentials, region, host) {

        	

            console.log("TRYNG TO CONNECT TO IOT");

            //var sessionToken = credentials.sessionToken;

            console.log(AWS.config.credentials.sessionToken);

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



            console.log(requestUrl);

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
                console.log("onConnect");

                var topicFilter = "prices/" + $routeParams.filter.replace(/\|/g, "/") + "/#";
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
                ///console.log(observation);
                
                var updated = false;
                var i = 0;

                if ($scope.series.indexOf(observation.SK) < 0)
                {
                	$scope.series.push(observation.SK); 
                	$scope.data.push([]);
                	$scope.datasetOverride.push({ yAxisID: 'first-y-axis', xAxisID: 'first-x-axis'});
                }

				//$scope.labels.push(new Date());
                $scope.data[$scope.series.indexOf(observation.SK)].push({
                	x: observation.PM, 
                	y: observation.PE,
                	r: parseInt(observation.PC * 100)
                });

                
                $scope.prices.forEach(function(price){
                	if (price.SK === observation.SK)
                	{ 
                		$scope.prices[i] = observation;
                		updated = true;
                	}
                	i++;
                })

                if (updated === false)
                	$scope.prices.push(observation);

                $scope.$apply();
            }

        }


/*
const https = require('https');

	https.get('https://6lgiv8850i.execute-api.us-west-2.amazonaws.com/Prod/api/prices/Windows|us|west|2', (res) => {
	  console.log('statusCode:', res.statusCode);
	  console.log('headers:', res.headers);

	  res.on('data', (d) => {
	    process.stdout.write(d);
	  });

	}).on('error', (e) => {
	  console.error(e);
	});
	*/

}




// IOT FUNCTIONS

/**
 * utilities to do sigv4
 * @class SigV4Utils
 */
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

