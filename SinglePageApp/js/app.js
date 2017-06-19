
var angular = require('angular');
require('angular-route');
var AWS = require('aws-sdk');

AWS.config.region = 'us-west-2';
AWS.config.credentials = new AWS.CognitoIdentityCredentials({
    IdentityPoolId: 'us-west-2:63111ab5-4710-4ae7-bcb8-a64fad4a9e0b',
});

var app = angular.module('app', ['ngRoute', 'chart.js']);
 
require('./services');
require('./controllers');
 
app.config([
    '$routeProvider', 
    function($route) {
        $route.
            when('/prices', {
                templateUrl: 'html/prices-PR.html',
                controller: 'PricesPRController'
            }).
            when('/prices/:PR', {
                templateUrl: 'html/prices-PR.html',
                controller: 'PricesPRController'
            }).
            when('/prices/:PR/:AR', {
                templateUrl: 'html/prices-PR-AR.html',
                controller: 'PricesPRARController'
            }).
            when('/prices/:PR/:AR/:RE', {
                templateUrl: 'html/prices-PR-AR-RE.html',
                controller: 'PricesPRARREController'
            }).
            when('/prices/:PR/:AR/:RE/:RI', {
                templateUrl: 'html/prices-PR-AR-RE-RI.html',
                controller: 'PricesPRARRERIController'
            }).
            when('/prices/:PR/:AR/:RE/:RI/:FA', {
                templateUrl: 'html/prices-PR-AR-RE-RI-FA.html',
                controller: 'PricesPRARRERIFAController'
            }).
            otherwise({
                redirectTo: '/prices'
            });
    }
]);
