'use strict';

var app = angular.module('cdeweb', [
    'ngRoute',
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

        .when('/about/:query?', {
            controller: 'aboutCtrl',
            templateUrl: 'partials/about.html',
            header: 'partials/navbar.html'
        })
        .when('/search/:query?', {
            controller: 'searchCtrl',
            templateUrl: 'partials/cdeWeb.html',
            header: 'partials/navbar.html',
            noResultsMessage: 'No Search Results to display.',
            reloadOnSearch: false
        })
        .when('/copyPath/:path*', {
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
        dataModel.statusMessage = 'Search Results Cleared';
    };
});

app.controller('copyCtrl', function($scope, $routeParams, $window) {
    $scope.resultPath = $routeParams.path;
    $scope.goBack = function () {
        $window.history.back();
    }
});

// todo make Escape key in input query - clear it... or maybe two escapes clears it ?
app.controller('navbarCtrl', function ($scope, $location, $route, resetSearchResult, dataModel) {

    // modify ui-reset to only have remove icon visible if input has content.
    // This logic could be pushed back into uiReset possibly ? makes for easier styling control ?
    $scope.$watch('data.query', function queryWatch() {
        if (dataModel === null || typeof dataModel.query === 'undefined' || dataModel.query === null || dataModel.query === "") {
            $('a.hascontent').removeClass('hascontent');
        } else {
            $('a.ui-reset').addClass('hascontent'); // TODO use the default ui-reset class for this..
        }
    });

    $scope.searchInputActive = function() {
        return document.activeElement.id === 'search';
    };

    $scope.navSearch = function () {
        var query = dataModel.query || '';
        dataModel.searchInputActive = $scope.searchInputActive();
        $location.path('/search/' + query);
        $route.reload(); // let it reload even if path not changed, just cause user clicked search again.
    };

    $scope.navAbout = function () {
        // clear results, and change route to about so it doesnt search, but saves search
        var query = dataModel.query || '';
        resetSearchResult($scope);
        $location.path('/about/' + query);
    }
});

function commonSearchCtrl($scope, $routeParams, $route, resetSearchResult, searchHubInit, startHubs, dataModel) {
    var current = $route.current;
    dataModel.hubActive = true;
    dataModel.noResultsMessage = current.noResultsMessage;

    var query = $routeParams.query;
    if (!dataModel.query) {
        dataModel.query = query;
    }
    searchHubInit($scope);
    startHubs($scope);
}

app.controller('aboutCtrl', function ($scope, $routeParams, $route, resetSearchResult, searchHubInit, startHubs, dataModel) {
    commonSearchCtrl($scope, $routeParams, $route, resetSearchResult, searchHubInit, startHubs, dataModel);
});

app.controller('searchCtrl', function ($scope, $routeParams, $route, resetSearchResult, searchHubInit, startHubs, dataModel) {
    commonSearchCtrl($scope, $routeParams, $route, resetSearchResult, searchHubInit, startHubs, dataModel);
    $scope.doSearch();
});

app.factory('searchHubInit', function (myHubFactory, resetSearchResult, dataModel) {

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
                    'For pattern "' + dataModel.query + '" found '
                    + dataModel.searchResult.length + ' entries. '
                    + 'Searched ' + dataModel.searchCurrent
                    + ' entries. This is ' + ((100.0 * dataModel.searchCurrent) / dataModel.searchEnd).toFixed(1)
                    + '% of the searchable entries.';
            });

            proxy.on('addDirEntry', function(dirEntry) {
                dataModel.searchResult.push(dirEntry);
            });

            dataModel.proxyEventListenersConfigured = true;
        }
    }

    return function(scope) {
        var proxy = myHubFactory.getHubProxy('searchHub');
        addProxyEventListeners(proxy, scope);

        scope.doSearch = function() {
            dataModel.errorMessage = '';
            if (dataModel.query) {
                // This invokes Search when next connected, or now if allready connected.
                // TODO consider disconnect behaviour further?
                myHubFactory.start().done(function() {
                    proxy.invoke('Search', dataModel.query, function(data) {
                        console.log('doSearch', data);
                        console.log('doSearch total server side time (msec)', data.TotalMsec, 'nextUri "' + data.NextUri + '"');
                        dataModel.metrics = data.ElapsedList;
                        console.log('metrics', dataModel.metrics);
                    });
                });
            } else {
                dataModel.errorMessage = "Search not executed, pattern string empty.";
                resetSearchResult(scope);
            }
        };
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
    //factory.error(function (error) {
    //    console.log('__ eek error = ' + error);
    //    console.log(error);
    //});
    //factory.disconnected(function () {
    //    console.log('__ disconnected');
    //});
    //factory.connectionSlow(function () {
    //    console.log('__ connectionSlow');
    //});
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
        var startedHub = myHubFactory.start();
        console.log('_! startHub', startedHub.state());
        startedHub.then(function () {
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
