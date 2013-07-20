'use strict';

var app = angular.module('cdeweb', [
    'ngResource',
    'ngCookies',
    'ui.directives',
    'restangular',
    'signalrAngular'
]);

//app.value('signalRServer', ''); // Specify SignalR server URL here for supporting CORS

app.value('ngModuleOptions', {
    moduleLogging: true,
    signalrClientLogging: true,
    serverUrl: ''
});

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
    //$rootScope.data = DataModel;

    $rootScope.data = {
        email: 'rob@queenofblad.es',
        name: 'cdeWeb',
        version: '0.1',
        query: '',
        queryOptions: {},
        searchResult: [],
        metrics: []
    };

    $rootScope.layoutPartial = function (partialName) {
        //console.log('$route.current[partialName]', $route.current[partialName]);
        return $route.current[partialName];
    };

    $rootScope.haveResults = function () { // todo not good form to put code on $rootScope it seems
        return $rootScope.data.searchResult.length !== 0;
    };
});

app.factory('resetSearchResult', function() {
    return function (scope) {
        scope.data.searchResult = [];
        scope.data.nextLink = undefined;
        scope.data.metrics = undefined;
        scope.data.totalMsec = undefined;
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
app.controller('navbarCtrl', function ($scope, $location, $route, DataModel, resetSearchResult) {
    //console.log('navbarCtrl init');
    //$scope.data = DataModel;

    // modify ui-reset to only have remove icon visible if input has content.
    // This logic could be pushed back into uiReset possibly ? makes for easier styling control ?
    $scope.$watch('data.query', function queryWatch() {
        if ($scope.data === null || $scope.data.query === null || $scope.data.query === "") {
            $('a.hascontent').removeClass('hascontent');
        } else {
            $('a.ui-reset').addClass('hascontent'); // TODO use the default ui-reset class for this..
        }
    });

    $scope.clearResult = function () {
        // want to remove results, change route but not search again and /search/  will search all.
        resetSearchResult($scope);
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

        resetSearchResult($scope);
        $route.reload(); // let it reload even if path not changed, just cause user clicked search again.
    };
});

app.controller('aboutCtrl', function ($scope, DataModel) {
    //console.log('aboutCtrl init');
    //$scope.data = DataModel;
});

app.controller('searchCtrl', function ($scope, $routeParams, $location, $route, DataModel, DirEntryRepository, myHubFactory, resetSearchResult) {
    //console.log('searchCtrl init');
    //$scope.data = DataModel;
    var query = $routeParams.query;
    if (!$scope.data.query) {
        $scope.data.query = query;
    }
//    var current = $route.current;

    resetSearchResult($scope);
    $scope.data.noResultsMessage = "Waiting for search results.";
    $location.path('/search/' + query);
    
    //$scope.doQuery = function () {
    //    // todo, if click again, this just starts 'another' search
    //    //   inside client if click again cancel, then start search...
    //    searchHubProxyXX.invoke('Search', $scope.data.query, function (data) {
    //        console.log('doQuery data', data);
    //    });
    //};


    //var getList = DirEntryRepository.getList(query);

    // restangular promise of field value.
    //$scope.data.searchResult = getList.get('Value'); //now this is a promise means my 'searching' message isnt displayed.
    
    // Disable old 'Search' for now.
    //getList.then(function(results) {
    //    //$scope.data.metadata = results['odata.metadata'];
    //    //$scope.data.nextLink = results['odata.nextLink'];
    //    $scope.data.searchResult = results.Value;
    //    $scope.data.metrics = results.ElapsedList;
    //    $scope.data.totalMsec = results.TotalMsec;
    //    $scope.data.noResultsMessage = current.noResultsMessage;
    //}, function (response) {
    //    console.log('getList() fail.');
    //    if (response.status === 404) { // NotFound
    //        $scope.data.noResultsMessage = current.noResultsMessage;
    //    } else {
    //        $scope.data.noResultsMessage = "Error occurred searching. (Error Code " + response.statusf + ")";
    //    }
    //});
    //$route.reload(); // force reload even if url not changed, search again.
     
    // use web browser modal dialog for clipboard copy hackery. Abandoned at moment.
    $scope.copyPathDialog = function (path) {
        console.log('path ' + path);
        $('#copyPathDialog').modal({});
    };
});

app.factory('searchHubInit', function (myHubFactory, resetSearchResult) {
   return function (scope) {
       var proxy = myHubFactory.getHubProxy('searchHub');

       proxy.on('filesToLoad', function (data, extra) {
           //console.log('fileToLoad', data, extra);
           scope.data.filesToLoad = data;
           scope.data.statusMessage = 'Catalog files to load ' + data;
       });

       proxy.on('searchProgress', function (count, progressEnd) {
           //console.log('searchProgress', count, progressEnd);
           scope.data.searchCurrent = count;
           scope.data.searchEnd = progressEnd;
           scope.data.statusMessage = 'Searched ' + count + ' of ' + progressEnd;
       });

       proxy.on('searchStart', function () {
           resetSearchResult(scope);
       });

       proxy.on('searchDone', function () {
           var data = scope.data;
           data.statusMessage =
               'Found ' + data.searchResult.length + ' entries. '
               + 'Searched ' + data.searchCurrent
               + ' entries. This is ' + ((100.0 * data.searchCurrent) / data.searchEnd).toFixed(1)
               + '% of the searchable entries.';
       });

       proxy.on('addDirEntry', function (dirEntry) {
           //console.log('addDirEntry', dirEntry);
           scope.data.searchResult.push(dirEntry);
       });

       scope.doQuery = function () {
           console.log('scope.doQuery()');
           // todo, if click again, this just starts 'another' search
           //   inside client if click again cancel, then start search...
           proxy.invoke('Search', scope.data.query, function (data) {
               console.log('doQuery data', data);
           });
       };
   }
});

app.controller('nosearchCtrl', function ($scope, $routeParams, $location, $route, DataModel, DirEntryRepository, resetSearchResult, searchHubInit, startHubs) {
    //$scope.data = DataModel;
    $scope.data.statusMessage = 'Connecting to server';
    $scope.data.hubActive = false;
    
    var query = $routeParams.query;
    if (!$scope.data.query) {
        $scope.data.query = query;
    }
    var current = $route.current;
    $scope.data.searchResult = [];
    $scope.data.noResultsMessage = current.noResultsMessage;

    /*var searchHubProxy = */searchHubInit($scope);
    startHubs($scope);
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
        var data = $rootScope.data;
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

app.factory('myHubFactory', function (signalrHubFactory, ngModuleOptions) {
    console.log('__ myHubFactory');

    var factory = signalrHubFactory(ngModuleOptions);

    //factory.starting(function () {
    //    console.log('__ starting');
    //});
    //factory.received(function (d) {
    //    console.log('__ received = ' + d);
    //    console.log(d);
    //});
    factory.stateChanged(function (evt) {
        var stateConversion = {0: 'connecting', 1: 'connected', 2: 'reconnecting', 4: 'disconnected'};
        console.log('__ stateChanged ' + stateConversion[evt.oldState] + ' -> ' + stateConversion[evt.newState]);
    });
    factory.error(function (error) {
        console.log('__ eek error = ' + error);
    });
    factory.disconnected(function () {
        console.log('__ disconnected');
    });
    factory.connectionSlow(function () {
        console.log('__ connectionSlow');
    });
    //factory.reconnecting(function (d) {
    //    console.log('__ reconnecting = ' + d);
    //});
    //factory.reconnected(function () {
    //    console.log('__ reconnected = ' + d);
    //});

    return factory;
});

app.factory('serverTimeHubInit', function(myHubFactory) {
    console.log('__ serverTimeHubInit');
    return function (scope) {
        //console.log('_| serverTimeHubInit');
        var proxy = myHubFactory.getHubProxy('serverTimeHub');
        scope.getServerTime = function () {
            proxy.invoke('getServerTime', function (data) {
                    scope.currentServerTimeManually = data;
                })
                .fail(function(error) {
                    console.log('>invoke(getServerTime).fail() -> ' + error);
                });
        };
        return proxy;
    }
});

app.factory('clientPushHubInit', function(myHubFactory) {
    //console.log('__ clientPushHubInit');
    return function (scope) {
        //console.log('_| clientPushHubInit');
        var proxy = myHubFactory.getHubProxy('clientPushHub');
        proxy.on('serverTime', function (data) {
            scope.currentServerTime = data;
        });

        return proxy;
    }
});

app.factory('startHubs', function(myHubFactory) {
    console.log('__ startHub');
    return function (scope) {
        console.log('_! startHub');
        console.log(myHubFactory.start().state());
        myHubFactory.start().then(function () {
            scope.data.hubActive = true;
            scope.data.statusMessage = 'Connected to server';
            console.log('startHubs connection id ()', myHubFactory.connection.id);
        });
    };
});

app.factory('testButtonStuff', function (myHubFactory) {
    // bits and pieces for some poke framework testing buttons
    return function(scope, clientPushHubProxy) {
        scope.startConnection = function() {
            myHubFactory.start().fail(function(err) {
                console.log('>start().fail -> ' + err);
            });
        };

        scope.stopConnection = function() {
            console.log('stopConnection function');
            myHubFactory.stop();
            //setTimeout(function () {
            //    myHubFactory.stop(); // try get it out of $apply scope.
            //, 100);
        };

        scope.hubOff = function() {
            console.log('hubOff function');
            clientPushHubProxy.off('serverTime');
        };

        scope.hubOn = function() {
            console.log('hubOn called');
            clientPushHubProxy.on('serverTime', function(data) {
                scope.currentServerTime = data;
            });
        };
    };
});

app.controller('ServerTimeController', function ($scope, serverTimeHubInit, clientPushHubInit, startHubs, testButtonStuff, myHubFactory) {
    var trialCounter = 0;

    myHubFactory.disconnected(function (d) {
        console.log('__ received - trial = ' + d);
        console.log(d);
        $scope.trace = 'disconnected trial ' + trialCounter++;
    });//, $scope);

    myHubFactory.received(function (d) {
        console.log('__ received = ' + d);
        console.log(d);
        $scope.trace = 'received trial ' + trialCounter++;
    });


    console.log('__ ServerTimeController');
    /*var serverTimeHubProxy = */serverTimeHubInit($scope);
    var clientPushHubProxy = clientPushHubInit($scope);
    startHubs($scope);
    testButtonStuff($scope, clientPushHubProxy);
});

