
module.exports = function($scope, $location, $route, $routeParams, UtilityService) {
 
    var url = "https://sa.octank.biz/api/prices" 
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

};

