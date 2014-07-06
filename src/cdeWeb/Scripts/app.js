'use strict';

var app = angular.module('cdeweb', [
    'ngResource',
    'ngCookies',
    'ui.directives',
    'restangular',
    'signalrAngular'
]);

app.constant('connectionStateMap', {
    0: 'connecting',
    1: 'connected',
    2: 'reconnecting',
    4: 'disconnected'
});

app.value('ngModuleOptions', {
    moduleLogging: false,
    //signalrClientLogging: true,
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

app.run(function ($rootScope, $route, dataModel) {
    $rootScope.data = dataModel;

    $rootScope.layoutPartial = function (partialName) {
        //console.log('$route.current[partialName]', $route.current[partialName]);
        return $route.current[partialName];
    };
});

app.factory('dataModel', function (connectionStateMap) {
  //noinspection UnnecessaryLocalVariableJS
  var data = {
        email: 'rob@queenofblad.es',
        name: 'cdeWeb',
        version: '0.1',
        query: '',
        queryOptions: {},
        searchResult: [],
        metrics: [],
        totalMsec: 0,
        hubActive: false,
        connected: false,
        canQuery: function() {
          return this.hubActive && this.connected;
        },
        connectionStatusManager: function(evt) {
            this.connected = connectionStateMap[evt.newState] === 'connected';
            if (this.connected) {
                this.statusMessage = 'Connected to server';
            } else {
                this.statusMessage = "No Server available for search";
            }
        },
        haveResults: function () {
            return this.searchResult.length !== 0;
        }
    };
    return data;
});


app.factory('resetSearchResult', function(dataModel) {
    return function (scope) {
        console.log('resetSearchResult() scope.$id', scope.$id);
        dataModel.searchResult = [];
        dataModel.nextLink = undefined;
        dataModel.metrics = undefined;
        dataModel.totalMsec = undefined;
        dataModel.statusMessage = 'Results Reset';
    };
});

app.controller('copyCtrl', function($scope, $location, $routeParams, $window) {
    $scope.resultPath = $routeParams.path;
    console.log('$location', $location);
    console.log('$routeParams.path', $routeParams.path);
    //window.prompt("Copy to clipboard: Ctrl+C, Enter", $scope.resultPath);
    //after copy leave ? //$location.path('/#');

    $scope.goBack = function () {
        $window.history.back();
    }
});

// todo make Escape key in input query - clear it... or maybe two escapes clears it ?
app.controller('navbarCtrl', function ($scope, $location, $route, resetSearchResult, dataModel) {
    //console.log('navbarCtrl init');

    // modify ui-reset to only have remove icon visible if input has content.
    // This logic could be pushed back into uiReset possibly ? makes for easier styling control ?
    $scope.$watch('data.query', function queryWatch() {
        if (dataModel === null || dataModel.query === null || dataModel.query === "") {
            $('a.hascontent').removeClass('hascontent');
        } else {
            $('a.ui-reset').addClass('hascontent'); // TODO use the default ui-reset class for this..
        }
    });

    $scope.clearResult = function () {
        // want to remove results, change route but not search again and /search/  will search all.
        resetSearchResult($scope);
        var query = dataModel.query || '';
        var path = '/nosearch/' + query;
        $location.path(path);
    };

    $scope.searchInputActive = function() {
        return document.activeElement.id === 'search';
    };
    
    $scope.search = function () {
        //console.log('navbarCtrl.search [' + $scope.data.query + ']');
        console.log('navabarCtrl calling resetSearchResult($scope)', $scope.$id);
        dataModel.searchInputActive = $scope.searchInputActive();
        var query = dataModel.query || '';
        $location.path('/search/' + query);

        resetSearchResult($scope);
        $route.reload(); // let it reload even if path not changed, just cause user clicked search again.
    };
});

app.controller('aboutCtrl', function ($scope) {
    //console.log('aboutCtrl init');
});

app.controller('searchCtrl', function ($scope, $routeParams, $location, $route, dirEntryRepository, myHubFactory, resetSearchResult, searchHubInit, startHubs, dataModel) {
    dataModel.hubActive = true;

    var query = $routeParams.query;
    if (!dataModel.query) {
        dataModel.query = query;
    }
    var current = $route.current;
    dataModel.noResultsMessage = current.noResultsMessage;
    resetSearchResult($scope);
    $location.path('/search/' + query);

    // use web browser modal dialog for clipboard copy hackery. Abandoned at moment.
    $scope.copyPathDialog = function (path) {
        console.log('path ' + path);
        $('#copyPathDialog').modal({});
    };

    searchHubInit($scope);
    startHubs($scope);
    console.log('$scope.doQuery();');
    $scope.doQuery();  // do the query.
});


app.factory('searchHubInit', function (myHubFactory, resetSearchResult, connectionStateMap, dataModel) {

    function addProxyEventListeners(proxy, scope) {
        if (!dataModel.hasOwnProperty('proxyEventListenersConfigured')){

            proxy.on('filesToLoad', function(data) {
                dataModel.filesToLoad = data;
                dataModel.statusMessage = 'Catalog files to load ' + data;
                console.log('Catalog files to load', data);
            });

            proxy.on('searchProgress', function(count, progressEnd) {
                //console.log('searchProgress', count, progressEnd);
                dataModel.searchCurrent = count;
                dataModel.searchEnd = progressEnd;
                dataModel.statusMessage = 'Searched ' + count + ' of ' + progressEnd;
            });

            proxy.on('searchStart', function() {
                resetSearchResult(scope);
            });

            proxy.on('searchDone', function() {
                dataModel.statusMessage =
                    'Found ' + dataModel.searchResult.length + ' entries. '
                    + 'Searched ' + dataModel.searchCurrent
                    + ' entries. This is ' + ((100.0 * dataModel.searchCurrent) / dataModel.searchEnd).toFixed(1)
                    + '% of the searchable entries.';
            });

            proxy.on('addDirEntry', function(dirEntry) {
                //console.log('addDirEntry', dirEntry);
                dataModel.searchResult.push(dirEntry);
            });

            dataModel.proxyEventListenersConfigured = true;
        }
    }

    return function(scope) {
        var proxy = myHubFactory.getHubProxy('searchHub');
        addProxyEventListeners(proxy, scope);

        myHubFactory.stateChanged(function (evt) {
            console.log('startHubs connection id ()', evt);
            console.log('searchHubInit stateChanged to ', connectionStateMap[evt.newState]);
        });

        scope.doQuery = function() {
            // This now only invokes Search when next connected, this might.
            // TODO consider disconnect behaviour further?
            myHubFactory.start().done(function() {
                proxy.invoke('Search', dataModel.query, function(data) {
                    console.log('doQuery', data);
                    console.log('doQuery total server side time (msec)', data.TotalMsec, 'nextUri "' + data.NextUri + '"');
                    dataModel.metrics = data.ElapsedList;
                    console.log('metrics', dataModel.metrics);
                });
            });
        };

        scope.doLog1 = function() {
          console.log('manamana');
        }
    };
});

app.controller('nosearchCtrl', function ($scope, $routeParams, $location, $route, dirEntryRepository, resetSearchResult, searchHubInit, startHubs, dataModel) {
    dataModel.hubActive = true; // for now we are allowing hub to be active.... in nosearch

    var query = $routeParams.query;
    if (!dataModel.query) {
        dataModel.query = query;
    }
    var current = $route.current;
    dataModel.noResultsMessage = current.noResultsMessage;
    resetSearchResult($scope);

    /*var searchHubProxy = */
    searchHubInit($scope);
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

app.directive('restoresearchfocus', function(dataModel) {
    return function (scope, element) {
        if (dataModel.searchInputActive && !scope.searchInputActive()) {
            element[0].focus();
        }
        dataModel.searchInputActive = false;
    };
});

app.service('dirEntryRepository', function(Restangular) {
    this.getList = function(query) {
        var baseDE = Restangular.all("Search?query=" + (query || ''));
        return baseDE.getList();
    };
});

app.factory('myHubFactory', function (dataModel, signalrHubFactory, ngModuleOptions, connectionStateMap) {
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
        console.log('__ stateChanged ' + connectionStateMap[evt.oldState] + ' -> ' + connectionStateMap[evt.newState]);
        dataModel.connectionStatusManager(evt);
    });
    factory.error(function (error) {
        console.log('__ eek error = ' + error);
        console.log(error);
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
    return function(scope) {
        //console.log('_| serverTimeHubInit');
        var proxy = myHubFactory.getHubProxy('serverTimeHub');
        scope.getServerTime = function() {
            proxy.invoke('getServerTime', function(data) {
                scope.currentServerTimeManually = data;
            })
                .fail(function(error) {
                    console.log('>invoke(getServerTime).fail() -> ' + error);
                });
        };
        return proxy;
    };
});

app.factory('clientPushHubInit', function(myHubFactory) {
    //console.log('__ clientPushHubInit');
    return function(scope) {
        //console.log('_| clientPushHubInit');
        var proxy = myHubFactory.getHubProxy('clientPushHub');
        proxy.on('serverTime', function(data) {
            scope.currentServerTime = data;
        });

        return proxy;
    };
});

app.factory('startHubs', function(myHubFactory) {
    console.log('__ startHub');
    return function (scope) {
        console.log('_! startHub', myHubFactory.start().state());
        myHubFactory.start().then(function () {
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
            //    myHubFactory.stop(); // try get it out of $apply scope. and it does
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
        console.log('__ disconnected - trial = ' + d);
        console.log(d);
        $scope.trace = 'disconnected trial ' + trialCounter++;
    });//, $scope);

    myHubFactory.received(function (d) {
        //console.log('__ received = ' + trialCounter);
        //console.log(d);
        $scope.trace = 'received trial ' + trialCounter++;
    });

    console.log('__ ServerTimeController');
    /*var serverTimeHubProxy = */serverTimeHubInit($scope);
    var clientPushHubProxy = clientPushHubInit($scope);
    startHubs($scope);
    testButtonStuff($scope, clientPushHubProxy);
});

