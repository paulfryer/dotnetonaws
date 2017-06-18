'use strict';

var app = require('angular').module('app');

app.controller('PricesController', require('./prices'));
app.controller('PricesPRController', require('./prices-PR'));
app.controller('PricesPRARController', require('./prices-PR-AR'));
app.controller('PricesPRARREController', require('./prices-PR-AR-RE'));
app.controller('PricesPRARRERIController', require('./prices-PR-AR-RE-RI'));
app.controller('PricesPRARRERIFAController', require('./prices-PR-AR-RE-RI-FA'));
