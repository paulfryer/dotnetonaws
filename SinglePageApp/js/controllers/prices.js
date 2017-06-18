var AWS = require('aws-sdk');
var cloudwatch = new AWS.CloudWatch();

module.exports = function($scope, $location, $route, $routeParams, UtilityService) {
  
    var now = new Date();
    var startDate = new Date();
    startDate.setHours(startDate.getHours() - 1);

    var params = {
        EndTime: now,
        MetricName: 'PR',
        Namespace: 'SpotAnalytics',
        Period: 300,
        StartTime: startDate,
        Statistics: [
            'Average'
        ],
          Dimensions: [
            { Name: 'PR',  Value: 'Linux-UNIX' },
            //{Name: 'PR', Value: 'Windows'}
            ]
    };
    var cloudwatch = new AWS.CloudWatch();
    cloudwatch.getMetricStatistics(params, function(err, data) {
        if (err) console.log(err, err.stack); // an error occurred
        else console.log(data); // successful response
    });

}