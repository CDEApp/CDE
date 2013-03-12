﻿'use strict';

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
            when('/', { controller: cdeWebCtrl, templateUrl: 'partials/cdeWeb.html' }).
            when('/', { controller: cdeWebCtrl, templateUrl: 'partials/cdeWeb.html' }).
            when('/search/:query', { controller: cdeWebSearchCtrl, templateUrl: 'partials/cdeWebSearch.html' }).
            when('/copyPath/:path', { controller: cdeWebCopy, templateUrl: 'partials/copyPath.html' }).
            otherwise({ redirectTo: '/about' });
    }).
    directive('mySelectall', ['$timeout', '$interpolate', function ($timeout, $interpolate) {
        return {
            restrict: 'A',
            link: function (scope, element, attrs) {
                element.focus(); // requires jquery = element[0].focus() for no jquery.
                scope.$watch(function() { return element.val(); }, function(newVal, oldVal) {
                    // only on initialize, or it does it for new typed input.
                    if (oldVal === newVal) {
                        element.select(); // requires jquery = element[0].select() for no jquery.
                    }
                });
            }
        };
    }]);

var cdeWebCopy = function($scope, $location, $routeParams) {
    $scope.resultPath = $routeParams.path;
    window.prompt("Copy to clipboard: Ctrl+C, Enter", $scope.resultPath);
    /// controlelr time

    //    $(document).ready(function() { // wrong this is in controller.. :(
    //        $('#copyField').focus(function() {
    //            debugger;
    //            $(this).select(); }
    //        );
    //    });
    //    $('#copyField')
    //        .focus();
    $location('/#');
};

var cdeWebSearchCtrl = function($scope, $location, version) {
};

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

    $scope.showClearResult = function() {
        return $scope.searchResult.length !== 0;
    };

    $scope.copyPathDialog = function(pZath) {
        //var s = $scope;
        //var rs = $scope.resultPath;
        //var p = path;
        $('#copyPathDialog').modal({});
    };

    // make our reset on input field only visible when query has value.
    // - how do i make this part of the reset control ?
    // - be nice if it didnt need to know model 'name' or control to fiddle class on ?
    // - reset creates the anchor so should be avail ?
    // - reset require ng-model we should be able to get its value ?
    // - then just create an extra watch hmm :) :) :)

    // modify ui-reset to only have remove icon visible if input has content.
    $scope.$watch('query', function queryWatch() {
        if ($scope.query === null || $scope.query === "") {
            $('form a.hascontent').removeClass('hascontent');
        } else {
            $('form a.ui-reset').addClass('hascontent'); // TODO use the default ui-reset class for this..
        }
        //console.log("$scope.query:" + $scope.query);
    });

    // TODO make Escape key in input - clear it... or maybe two escapes clears it ?

    $scope.version = version;
    $scope.name = 'cdeWeb';
    $scope.queryOptions = {};
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






