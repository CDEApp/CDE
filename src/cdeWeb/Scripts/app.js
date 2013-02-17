"use strict";
angular.module('cdeweb.service', [])
    .value('version', '0.1');

angular.module('cdeweb.directive', []);

angular.module('cdeweb.filter', []);


var cdeWebApp = angular.module('cdeweb', ['ngResource', 'cdeweb.service', 'cdeweb.directive', 'cdeweb.filter']).
    config(function($routeProvider) {
        $routeProvider.
            when('/', { controller: cdeWebCtrl, templateUrl: 'partials/cdeWeb.html' }).
            otherwise({ redirectTo: '/' });
    });

var cdeWebCtrl = function ($scope, $location, version) {
    $scope.clearResult = function() {
        $scope.searchResult = [];
    };

    $scope.clearQuery = function() {
        $scope.query = "";
    };

    $scope.search = function() {
        console.log('search ' + $scope.query);
        $scope.searchResult = searchResultTest;
    };

    $scope.hideClear = function() {
        return !$scope.query;
    };

    $scope.hideClearResult = function() {
        return $scope.searchResult.length === 0;
    };

    $scope.version = version;
    $scope.name = "cdeWeb";
    $scope.queryOptions = {};
    $scope.clearQuery();
    $scope.clearResult();
};


var searchResultTest = [
    {Name: 'moo1', Size: 10, Modified: 'adate1', Path: 'C:\\Here'},
    {Name: 'moo2', Size: 12, Modified: 'adate2', Path: 'C:\\'},
    {Name: 'Here', Size: 10, Modified: 'adate3', Path: 'C:\\'}
];
// pull Contact from web.config - so roy can setup to him ?









