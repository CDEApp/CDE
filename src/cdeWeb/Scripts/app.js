'use strict';
// ReSharper disable InconsistentNaming

var cdeWebApp = angular.module('cdeweb', [
    'ngResource',
    'ngCookies',
    'ui.directives',
    'restangular'
]);

// todo consider a version tag appended for when version bumps for load partials ?
cdeWebApp.config(function ($routeProvider) {
    $routeProvider
        .when('/about', {
            controller: 'cdeWebAboutCtrl',
            templateUrl: 'partials/about.html',
            header: 'partials/navbar.html'
        })
        .when('/search/:query', {
            controller: 'cdeWebCtrl',
            templateUrl: 'partials/cdeWeb.html',
            header: 'partials/navbar.html',
            nosearch: false,
            noResultsMessage: 'No Search Results to display.'
        })
        .when('/nosearch/:query', {
            controller: 'cdeWebCtrl',
            templateUrl: 'partials/cdeWeb.html',
            header: 'partials/navbar.html',
            nosearch: true,
            noResultsMessage: 'Search not performed. No Results to display.'
        })
        .when('/copyPath/:path', {
            controller: 'cdeWebCopyCtrl',
            templateUrl: 'partials/copyPath.html',
            header: 'partials/navbar.html'
        })
        .otherwise({ redirectTo: '/about' });
});

cdeWebApp.config(function (RestangularProvider) {
    var r = RestangularProvider;
    r.setBaseUrl('/odata');
    r.setListTypeIsArray(false); // odata returns an object with a value field for list.
    
    // Not modifying the response format for now. but this is how it culd be modified
    // NOTE: possibly should make a modified response which returns an object
    //       with 3 fields as below, possibly should make each field a promise
    //       so fields can be assigned immediately rather than waiting on promise resolve.

    //r.setResponseExtractor(function(response, operation, what, url) {
    //    if (operation === 'getList') {
    //        // rearrange odata response.
    //        var newResponse = response.value;
    //        return newResponse;
    //    } else {
    //        return response;
    //    }
    //});
});

cdeWebApp.run(function($rootScope, $route) {
    $rootScope.layoutPartial = function (partialName) {
        //console.log('$route.current[partialName]', $route.current[partialName]);
        return $route.current[partialName];
    };
});

cdeWebApp.controller('cdeWebCopyCtrl', function($scope, $location, $routeParams) {
    $scope.resultPath = $routeParams.path;
    console.log('$location', $location);
    console.log('$routeParams.path', $routeParams.path);
    //window.prompt("Copy to clipboard: Ctrl+C, Enter", $scope.resultPath);
    //after copy leave ? //$location.path('/#');
});

cdeWebApp.controller('navbarCtrl', function ($scope, $location, DataModel) {
    //console.log('navbarCtrl init');
    $scope.data = DataModel;

    // modify ui-reset to only have remove icon visible if input has content.
    // This logic could be pushed back into uiReset possibly ? makes for easier styling control ?
    $scope.$watch('data.query', function queryWatch() {
        if ($scope.model === null || $scope.data.query === null || $scope.data.query === "") {
            $('a.hascontent').removeClass('hascontent');
        } else {
            $('a.ui-reset').addClass('hascontent'); // TODO use the default ui-reset class for this..
        }
    });
    
    $scope.clearResult = function () {
        // want to remove results, change route but not search again and /search/  will search all.
        $scope.data.searchResult = [];
        $scope.data.nextLink = undefined;
        var query = $scope.data.query || '';
        var path = '/nosearch/' + query;
        $location.path(path);
    };

    $scope.searchInputActive = function() {
        return document.activeElement.id === 'search';
    };
    
    $scope.search = function () {
        //console.log('navbarCtrl.search [' + $scope.data.query + ']');
        $scope.data.searchInputActive = $scope.searchInputActive();
        var query = $scope.data.query || '';
        $location.path('/search/' + query);
    };
    
    $scope.haveResults = function () {
        return $scope.data.searchResult.length !== 0;
    };
});

cdeWebApp.controller('cdeWebAboutCtrl', function ($scope, DataModel) {
    //console.log('cdeWebAboutCtrl init');
    $scope.data = DataModel;
});

// todo make Escape key in input query - clear it... or maybe two escapes clears it ?
cdeWebApp.controller('cdeWebCtrl', function ($scope, $routeParams, $location, $route, DataModel, DirEntryRepository) {
    //console.log('cdeWebCtrl init');
    $scope.data = DataModel;
    var query = $routeParams.query;
    if (!$scope.data.query) {
        $scope.data.query = query;
    }
    var current = $route.current;
    $scope.data.searchResult = [];
    
    if (!current.nosearch) {
        $scope.data.noResultsMessage = "Waiting for search results.";
        $location.path('/search/' + query);
        DirEntryRepository.get(query)
            .then(function(results) {
                $scope.data.metadata = results['odata.metadata'];
                $scope.data.nextLink = results['odata.nextLink'];
                $scope.data.searchResult = results.value;
                $scope.data.noResultsMessage = current.noResultsMessage;
            });
    } else {
        $scope.data.noResultsMessage = current.noResultsMessage;
    }
     
    $scope.haveResults = function () {
        return $scope.data.searchResult.length !== 0;
    };
    
    // use web browser modal dialog for clipboard copy hackery. Abandoned at moment.
    $scope.copyPathDialog = function (path) {
        console.log('path ' + path);
        $('#copyPathDialog').modal({});
    };
});

cdeWebApp.directive('selectall', function () {
    return {
        restrict: 'A',
        link: function (scope, element) {
            element[0].focus();
            scope.$watch(function () { return element.val(); }, function (newVal, oldVal) {
                // only on initialize, or it does it for new typed input.
                if (oldVal === newVal) {
                    element[0].select();
                }
            });
        }
    };
});

cdeWebApp.directive('restoresearchfocus', function() {
    return function (scope, element) {
        if (scope.data.searchInputActive && !scope.searchInputActive()) {
            element[0].focus();
        }
        scope.data.searchInputActive = false;
    };
});

cdeWebApp.service('DirEntryRepository', function(Restangular) {
    this.get = function(substring) {
        var baseDE = Restangular.all("DirEntries" + substringFilter(substring, 'Name'));
        return baseDE.getList();
    };
});

cdeWebApp.factory('DataModel', function ($rootScope) {
        var data = $rootScope.data;;
        if (!data) {
            //console.log('DataModel new');
            data = {
                email: 'rob@queenofblad.es',
                name: 'cdeWeb',
                version: '0.1',
                query: '',
                queryOptions: {},
                searchResult: []
            };
        }
        return data;
    });

function substringFilter(query, field) {
    var notNullQuery = query || '';
    return "?$filter=substringof('" + notNullQuery + "'," + field + ")";
}


var searchResultTest = [
    { Name: 'moo1', Size: 10, Modified: 'adate1', Path: 'C:\\Here' },
    { Name: 'moo2', Size: 12, Modified: 'adate2', Path: 'C:\\' },
    { Name: 'Here', Size: 10, Modified: 'adate3', Path: 'C:\\' },
    { Name: 'Deep1', Size: 13, Modified: 'adate3', Path: 'C:\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate' },
    { Name: 'Deep2 Six', Size: 13, Modified: 'adate3', Path: 'C:\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate' },
    { Name: 'Deep3 Six', Size: 13, Modified: 'adate3', Path: 'C:\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate' },
    { Name: 'Deep4 Six', Size: 13, Modified: 'adate3', Path: 'C:\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate' }
];
// pull Contact from web.config - so roy can setup to him ?
// ReSharper restore InconsistentNaming


