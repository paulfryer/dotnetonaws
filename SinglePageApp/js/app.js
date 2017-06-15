
var angular = require('angular');
require('angular-route');
var AWS = require('aws-sdk');

AWS.config.region = 'us-west-2';

var app = angular.module('app', ['ngRoute']);
 
require('./services');
require('./controllers');
 
app.config([
    '$routeProvider', 
    function($route) {
        $route.
            when('/products', {
                templateUrl: 'html/products.html',
                controller: 'ProductsController'
            }).
            when('/prices/:sortKey', {
                templateUrl: 'html/prices.html',
                controller: 'PricesController'
            }).
            otherwise({
                redirectTo: '/products'
            });
    }
]);
