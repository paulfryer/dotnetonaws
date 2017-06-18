module.exports = function($scope, $location, $route, $routeParams, UtilityService) {

    var sortKey = $routeParams.PR;

    

    sortKey = sortKey.replace(/\|/g, "/");

    console.log(sortKey);

    var url = "https://spot.octank.biz/api/prices/" + sortKey;

  var xhttp = new XMLHttpRequest();
  xhttp.onreadystatechange = function() {
    if (this.readyState == 4 && this.status == 200) {
        $scope.resp = JSON.parse(this.responseText);
        $scope.$apply();
        console.log($scope.resp);
    }
  };
  xhttp.open("GET", url, true);
  xhttp.send();

}


