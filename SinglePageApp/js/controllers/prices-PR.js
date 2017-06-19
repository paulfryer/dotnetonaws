module.exports = function($scope, $location, $route, $routeParams, UtilityService) {

    var sortKey = $routeParams.PR;

    

    sortKey = sortKey.replace(/\|/g, "/");

    console.log(sortKey);

    var url = "https://spot.octank.biz/api/prices/" + sortKey;

  var xhttp = new XMLHttpRequest();
  xhttp.onreadystatechange = function() {
    if (this.readyState == 4 && this.status == 200) {
        $scope.resp = JSON.parse(this.responseText);
        
        console.log($scope.resp);
        //loadChart();
        $scope.$apply();
    }
  };
  xhttp.open("GET", url, true);
  xhttp.send();


  function loadChart(){
      $scope.labels = [];
      $scope.series = [];
      $scope.data = [];
      $scope.datasetOverride = [];
      $scope.options = {
        scales: {
          yAxes: [
            {
              id: 'y-axis-1',
              type: 'linear',
              display: true,
              position: 'left'
            }
          ]
        }
      };

      $scope.resp.IT.forEach(function(item){
        console.log(item);
        $scope.series.push(item.NA);
        
        var data = [];
        var label = [];
        item.ST.forEach(function(stat) {
            data.push(stat.AV);
            label.push(stat.TM);
        });
        $scope.data.push(data);
        $scope.labels.push(label);
      });

  }

}




