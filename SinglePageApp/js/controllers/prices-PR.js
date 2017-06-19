require('chart.js');

module.exports = function($scope, $location, $route, $routeParams, UtilityService) {

    var sortKey = $routeParams.PR;
    sortKey = sortKey.replace(/\|/g, "/");

    var url = "https://spot.octank.biz/api/prices/" + sortKey;

  var xhttp = new XMLHttpRequest();
  xhttp.onreadystatechange = function() {
    if (this.readyState == 4 && this.status == 200) {
        $scope.resp = JSON.parse(this.responseText);
        loadChart();

        $scope.codes = $scope.resp.PA.split('|');
        $scope.names = {};

        var i = 0;
        for(var code in $scope.resp.NM){
            $scope.names[$scope.codes[i]] = $scope.resp.NM[code];
            i++;
        }       
        console.log($scope.names);
        $scope.$apply();
    }
  };
  xhttp.open("GET", url, true);
  xhttp.send();


  function loadChart(){
    $scope.series = [];
      $scope.data = [];
      $scope.options = {
        scales: {
          xAxes: [{
            type: 'time',
            position: 'bottom'
          }]
        }
      };
    $scope.resp.IT.forEach(function(item){
        $scope.series.push(item.NA);
        var data = [];
        item.ST.forEach(function(stat) {
                data.push({ x: stat.TM, y: stat.AV});
            });
        $scope.data.push(data);
    });

  }

}




