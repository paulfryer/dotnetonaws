require('chart.js');

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
        loadChart();

        $scope.codes = $scope.resp.PA.split('|');
        $scope.names = {};

        var i = 0;
        var path = "/#!/prices/";
        $scope.nextLive = "";
        for(var code in $scope.resp.NM){
            if (code == "RI")
              $scope.nextLive = "/live";
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




