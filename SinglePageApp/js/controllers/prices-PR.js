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
        loadChart();
        $scope.$apply();
    }
  };
  xhttp.open("GET", url, true);
  xhttp.send();


  function loadChart(){
    $scope.myChartObject = {
      "type": "LineChart",
      "displayed": false,
      "data": {
        "cols": [
          {
            "id": "month",
            "label": "Month",
            "type": "string",
            "p": {}
          },
          {
            "id": "laptop-id",
            "label": "Laptop",
            "type": "number",
            "p": {}
          },
          {
            "id": "desktop-id",
            "label": "Desktop",
            "type": "number",
            "p": {}
          },
          {
            "id": "server-id",
            "label": "Server",
            "type": "number",
            "p": {}
          },
          {
            "id": "cost-id",
            "label": "Shipping",
            "type": "number"
          }
        ],
        "rows": [
          {
            "c": [
              {
                "v": "January"
              },
              {
                "v": 19,
                "f": "42 items"
              },
              {
                "v": 12,
                "f": "Ony 12 items"
              },
              {
                "v": 7,
                "f": "7 servers"
              },
              {
                "v": 4
              }
            ]
          },
          {
            "c": [
              {
                "v": "February"
              },
              {
                "v": 13
              },
              {
                "v": 1,
                "f": "1 unit (Out of stock this month)"
              },
              {
                "v": 12
              },
              {
                "v": 2
              }
            ]
          },
          {
            "c": [
              {
                "v": "March"
              },
              {
                "v": 24
              },
              {
                "v": 5
              },
              {
                "v": 11
              },
              {
                "v": 6
              }
            ]
          }
        ]
      },
      "options": {
        "title": "Sales per month",
        "isStacked": "true",
        "fill": 20,
        "displayExactValues": true,
        "vAxis": {
          "title": "Sales unit",
          "gridlines": {
            "count": 10
          }
        },
        "hAxis": {
          "title": "Date"
        }
      },
      "formatters": {}
    }

  }

  function loadChart2(){
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




