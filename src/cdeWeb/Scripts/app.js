'use strict';

var app = angular.module('cdeweb', [
    'ngResource',
    'ngCookies',
    'ui.directives',
    'restangular',
    'ngSignalR'
]);
app.value('$', $); // jQuery - seems a good idea.
app.value('signalRServer', ''); // Specify SignalR server URL here for supporting CORS

// get Contact from web.config - so can be configured for site ?
// todo hitting search of same string doesnt do the search... ? force it ?
// todo support back navigation ? without doing a search ? ? 
// todo consider a version tag appended for when version bumps for load partials ?
app.config(function ($routeProvider) {
    $routeProvider
        .when('/about', {
            controller: 'aboutCtrl',
            templateUrl: 'partials/about.html',
            header: 'partials/navbar.html'
        })
        .when('/search/:query', {
            controller: 'searchCtrl',
            templateUrl: 'partials/cdeWeb.html',
            header: 'partials/navbar.html',
            noResultsMessage: 'No Search Results to display.',
            reloadOnSearch: false
        })
        .when('/nosearch/:query', {
            controller: 'nosearchCtrl',
            templateUrl: 'partials/cdeWeb.html',
            header: 'partials/navbar.html',
            noResultsMessage: 'Search not performed. No Results to display.'
        })
        .when('/copyPath/:path', {
            controller: 'copyCtrl',
            templateUrl: 'partials/copyPath.html',
            header: 'partials/navbar.html'
        })
        .otherwise({ redirectTo: '/about' });
});


// A simple background color flash effect that uses jQuery Color plugin
$.fn.flash = function(color, duration) {
    var current = this.css('backgroundColor');
    this.animate({ backgroundColor: 'rgb(' + color + ')' }, duration / 2)
        .animate({ backgroundColor: current }, duration / 2);
};

app.config(function (RestangularProvider) {
    var r = RestangularProvider;
    r.setBaseUrl('/api');
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

app.run(function ($rootScope, $route, DataModel) {
    $rootScope.data = DataModel;

    $rootScope.layoutPartial = function (partialName) {
        //console.log('$route.current[partialName]', $route.current[partialName]);
        return $route.current[partialName];
    };

    $rootScope.resetSearchResult = function () {
        $rootScope.data.searchResult = [];
        $rootScope.data.nextLink = undefined;
        $rootScope.data.metrics = undefined;
        $rootScope.data.totalMsec = undefined;
    };
    
    $rootScope.haveResults = function () {
        return $rootScope.data.searchResult.length !== 0;
    };
});

app.controller('copyCtrl', function($scope, $location, $routeParams) {
    $scope.resultPath = $routeParams.path;
    console.log('$location', $location);
    console.log('$routeParams.path', $routeParams.path);
    //window.prompt("Copy to clipboard: Ctrl+C, Enter", $scope.resultPath);
    //after copy leave ? //$location.path('/#');
});

// todo make Escape key in input query - clear it... or maybe two escapes clears it ?
app.controller('navbarCtrl', function ($scope, $location, $route, DataModel) {
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
        $scope.resetSearchResult();
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

        $scope.resetSearchResult();
        $route.reload();
    };
});

app.controller('aboutCtrl', function ($scope, DataModel) {
    //console.log('aboutCtrl init');
    $scope.data = DataModel;
});

app.controller('searchCtrl', function ($scope, $routeParams, $location, $route, DataModel, DirEntryRepository) {
    //console.log('cdeWebCtrl init');
    $scope.data = DataModel;
    var query = $routeParams.query;
    if (!$scope.data.query) {
        $scope.data.query = query;
    }
    var current = $route.current;

    $scope.resetSearchResult();
    $scope.data.noResultsMessage = "Waiting for search results.";
    $location.path('/search/' + query);
    var getList = DirEntryRepository.getList(query);

    // restangular promise of field value.
    //$scope.data.searchResult = getList.get('Value'); //now this is a promise means my 'searching' message isnt displayed.
    
    getList.then(function(results) {
        //$scope.data.metadata = results['odata.metadata'];
        //$scope.data.nextLink = results['odata.nextLink'];
        $scope.data.searchResult = results.Value;
        $scope.data.metrics = results.ElapsedList;
        $scope.data.totalMsec = results.TotalMsec;
        $scope.data.noResultsMessage = current.noResultsMessage;
    }, function (response) {
        console.log('getList() fail.')
        if (response.status === 404) { // NotFound
            $scope.data.noResultsMessage = current.noResultsMessage;
        } else {
            $scope.data.noResultsMessage = "Error occurred searching. (Error Code " + response.statusf + ")";
        }
    });
    //$route.reload(); // force reload even if url not changed, search again.
     
    // use web browser modal dialog for clipboard copy hackery. Abandoned at moment.
    $scope.copyPathDialog = function (path) {
        console.log('path ' + path);
        $('#copyPathDialog').modal({});
    };
});

app.controller('nosearchCtrl', function ($scope, $routeParams, $location, $route, DataModel, DirEntryRepository, hubProxy) {
    $scope.data = DataModel;
    $scope.data.statusMessage = 'Connecting to server';
    $scope.data.hubActive = false;
    var query = $routeParams.query;
    if (!$scope.data.query) {
        $scope.data.query = query;
    }
    var current = $route.current;
    $scope.data.searchResult = [];
    $scope.data.noResultsMessage = current.noResultsMessage;
    
    var searchHubProxy = hubProxy(hubProxy.defaultServer, 'searchHub');
    searchHubProxy.start({ logging: true }).done(function () {
        $scope.data.hubActive = true;
        $scope.data.statusMessage = 'Connected to server';
        console.log('connection id', searchHubProxy.connection.id);
    });

    searchHubProxy.on('filesToLoad', function (data, extra) {
        console.log('fileToLoad', data, extra);
        $scope.data.filesToLoad = data;
        $scope.data.statusMessage = 'Catalog files to load ' + $scope.data.filesToLoad;
    });
    
    searchHubProxy.on('addDirEntry', function (dirEntry) {
        console.log('addDirEntry', dirEntry);
        $scope.data.searchResult.push(dirEntry);
    });

    $scope.doQuery = function (search, more) {
        searchHubProxy.invoke('query', search, more, function (data) {
            console.log('doQuery data', data);
        });
    };
});

app.directive('selectall', function () {
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

app.directive('restoresearchfocus', function() {
    return function (scope, element) {
        if (scope.data.searchInputActive && !scope.searchInputActive()) {
            element[0].focus();
        }
        scope.data.searchInputActive = false;
    };
});

app.service('DirEntryRepository', function(Restangular) {
    this.getList = function(query) {
        var baseDE = Restangular.all("Search?query=" + (query || ''));
        return baseDE.getList();
    };
});

app.factory('DataModel', function ($rootScope) {
        var data = $rootScope.data;;
        if (!data) {
            //console.log('DataModel new');
            data = {
                email: 'rob@queenofblad.es',
                name: 'cdeWeb',
                version: '0.1',
                query: '',
                queryOptions: {},
                searchResult: [],
                metrics: []
            };
        }
        return data;
});

app.controller('ServerTimeController', function ($scope, hubProxy) {
    var clientPushHubProxy = hubProxy(hubProxy.defaultServer, 'clientPushHub');
    clientPushHubProxy.start({ logging: true });
    var serverTimeHubProxy = hubProxy(hubProxy.defaultServer, 'serverTimeHub');
    serverTimeHubProxy.start({ logging: true });
        
    clientPushHubProxy.on('serverTime', function (data) {
        $scope.currentServerTime = data;
    });

    $scope.getServerTime = function () {
        serverTimeHubProxy.invoke('getServerTime', function(data) {
            $scope.currentServerTimeManually = data;
        });

        //serverTimeHubProxy.invoke('getServerTime') 
        //    .done(function (data) {
        //        $scope.$apply(function() { /// must apply. or not inside angular.
        //            $scope.currentServerTimeManually = data;
        //        });
        //    });
    };
});
