'use strict';

angular.module('cdeweb.service', [])
    .value('version', '0.1');

angular.module('cdeweb.directive', []);

angular.module('cdeweb.filter', []);


var cdeWebApp = angular.module('cdeweb', [
        'ngResource',
        'cdeweb.service',
        'cdeweb.directive',
        'cdeweb.filter',
        'ui.directives'
    ]).
    config(function($routeProvider) {
        $routeProvider.
            when('/', {
                controller: cdeWebCtrl,
                templateUrl: 'partials/cdeWeb.html',
                header: 'partials/navbar.html'
            }).
            when('/search/:query', {
                controller: cdeWebSearchCtrl,
                templateUrl: 'partials/cdeWebSearch.html',
                header: 'partials/navbar.html'
            }).
            when('/copyPath/:path', {
                controller: cdeWebCopyCtrl,
                templateUrl: 'partials/copyPath.html',
                header: 'partials/navbar.html'
            }).
            otherwise({ redirectTo: '/' });
    }).
    directive('selectall', function () {
        return {
            restrict: 'A',
            link: function (scope, element) {
                element.focus(); //element[0].focus(); // not require jquery
                // be careful of slow $watch - dom isnt fast usually
                scope.$watch(function() { return element.val(); }, function(newVal, oldVal) {
                    // only on initialize, or it does it for new typed input.
                    if (oldVal === newVal) {
                        element.select(); //element[0].select(); // not require jquery
                    }
                });
            }
        };
    }).
    run(function($rootScope, $route, $location) {
        $rootScope.layoutPartial = function (partialName) {
            console.log('$route.current', $route.current);
            console.log('partialName', partialName);
            console.log('$route.current[partialName]', $route.current[partialName]);

            return $route.current[partialName];
        };
    });

var cdeWebCopyCtrl = function($scope, $location, $routeParams) {
    $scope.resultPath = $routeParams.path;
    console.log('$location', $location);
    console.log('$routeParams.path', $routeParams.path);
    //window.prompt("Copy to clipboard: Ctrl+C, Enter", $scope.resultPath);
    //after copy leave ? //$location.path('/#');
};

var cdeWebSearchCtrl = function ($scope, $routeParams, $location, version) {
    console.log('cdeWebSearchCtrl setup', $routeParams.query);
    $scope.data.query = $routeParams.query;
    $scope.data.searchResult = searchResultTest;

    $scope.clearResult = function () {
        $scope.data.searchResult = [];
    };
    
    $scope.clearQuery = function () {
        $scope.data.query = '';
    };

    $scope.search = function () {
        console.log('search [' + $scope.data.query + ']');
        //$location.path('/search/' + $scope.data.query);
        $scope.data.searchResult = searchResultTest;
    };
    
    $scope.showClearResult = function () { // TODO modify to go back to no path.
        return $scope.data.searchResult.length !== 0;
    };
};

var cdeWebCtrl = function ($scope, $location, version) {
    $scope.clearResult = function() {
        $scope.data.searchResult = [];
    };

    $scope.clearQuery = function() {
        $scope.data.query = '';
    };

    $scope.search = function () {
        console.log('search query', $scope.data.query);
        //$location.path('/search/' + $scope.data.query);
        $scope.data.searchResult = searchResultTest;
    };

    $scope.hideClear = function() {
        return !$scope.data.query;
    };

    $scope.showClearResult = function() {
        return $scope.data.searchResult.length !== 0;
    };

    $scope.copyPathDialog = function(path) {
        //var s = $scope;
        //var rs = $scope.resultPath;
        //var p = path;
        console.log('path ' + path)
        $('#copyPathDialog').modal({});
    };

    // make our reset on input field only visible when query has value.
    // - how do i make this part of the reset control ?
    // - be nice if it didnt need to know model 'name' or control to fiddle class on ?
    // - reset creates the anchor so should be avail ?
    // - reset require ng-model we should be able to get its value ?
    // - then just create an extra watch hmm :) :) :)

    // modify ui-reset to only have remove icon visible if input has content.
    $scope.$watch('data.query', function queryWatch() {
        if ($scope.model === null || $scope.data.query === null || $scope.data.query === "") {
            $('a.hascontent').removeClass('hascontent');
        } else {
            $('a.ui-reset').addClass('hascontent'); // TODO use the default ui-reset class for this..
        }
        //console.log("$scope.data.query:" + $scope.data.query);
    });

    // TODO make Escape key in input - clear it... or maybe two escapes clears it ?

    var data = {};
    $scope.data = data;
    data.query = '';
    data.version = version;
    data.name = 'cdeWeb';
    data.queryOptions = {};
    $scope.clearQuery();
    $scope.clearResult();
};

var searchResultTest = [
    {Name: 'moo1', Size: 10, Modified: 'adate1', Path: 'C:\\Here'},
    {Name: 'moo2', Size: 12, Modified: 'adate2', Path: 'C:\\'},
    {Name: 'Here', Size: 10, Modified: 'adate3', Path: 'C:\\'},
    {Name: 'Deep1', Size: 13, Modified: 'adate3', Path: 'C:\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate'},
    {Name: 'Deep2 Six', Size: 13, Modified: 'adate3', Path: 'C:\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate'},
    {Name: 'Deep3 Six', Size: 13, Modified: 'adate3', Path: 'C:\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate'},
    {Name: 'Deep4 Six', Size: 13, Modified: 'adate3', Path: 'C:\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate'}
];
// pull Contact from web.config - so roy can setup to him ?







