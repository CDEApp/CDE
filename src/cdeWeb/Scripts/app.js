'use strict';

var app = angular.module('cdeweb', [
    'ngResource',
    'ngCookies',
    'ui.directives',
    'restangular'
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
    
    $scope.haveResults = function () {
        return $scope.data.searchResult.length !== 0;
    };


});

app.controller('aboutCtrl', function ($scope, DataModel) {
    //console.log('aboutCtrl init');
    $scope.data = DataModel;
});

app.controller('searchCtrl', function ($scope, $routeParams, $location, $route, DataModel, DirEntryRepository, signalRHubProxy) {
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
     
    $scope.haveResults = function () {
        return $scope.data.searchResult.length !== 0;
    };
    
    // use web browser modal dialog for clipboard copy hackery. Abandoned at moment.
    $scope.copyPathDialog = function (path) {
        console.log('path ' + path);
        $('#copyPathDialog').modal({});
    };
});

app.controller('nosearchCtrl', function ($scope, $routeParams, $location, $route, DataModel, DirEntryRepository, signalRHubProxy) {
    $scope.data = DataModel;
    $scope.data.hubActive = false;
    var query = $routeParams.query;
    if (!$scope.data.query) {
        $scope.data.query = query;
    }
    var current = $route.current;
    $scope.data.searchResult = [];
    $scope.data.noResultsMessage = current.noResultsMessage;
    
    $scope.haveResults = function () {
        return false;
    };
    
    var searchHubProxy = signalRHubProxy(signalRHubProxy.defaultServer,
        'searchHub', { logging: true });
    searchHubProxy.hubStartPromise.done(function () {
            $scope.data.hubActive = true;
            //console.log('connection id', searchHubProxy.connection.id);
        });

    searchHubProxy.on('filesToLoadFred', function (data, extra) {
        $scope.data.filesToLoad = data;
        console.log('filesToLoadFred', data, extra);
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

// replacing guts using signalRHubProxy.
app.controller('StockTickerCtrl', function ($scope, signalRHubProxy) {
    $scope.stocks = [];
    $scope.marketIsOpen = false;
    var stockTickerProxy = signalRHubProxy(signalRHubProxy.defaultServer,
        'stockTicker', { logging: true });
    stockTickerProxy.hubStartPromise.done(function () {
        initializeStockMarket();
    });

    $scope.openMarket = function () { stockTickerProxy.invoke('openMarket'); };
    $scope.closeMarket = function () { stockTickerProxy.invoke('closeMarket'); };
    $scope.reset = function () { stockTickerProxy.invoke('reset'); };
    stockTickerProxy.on('marketOpened', function () { setMarketState(true); });
    stockTickerProxy.on('marketClosed', function () { setMarketState(false); });
    stockTickerProxy.on('marketReset', function () { initializeStockMarket(); });
    stockTickerProxy.on('updateStockPrice', function (stock) { replaceStock(stock); });

    function initializeStockMarket() {
        stockTickerProxy.invoke('getAllStocks', assignStocks);
        // todo this prob should chain with promises. after getAllStocks - ick callbacks at moment
        stockTickerProxy.invoke('getMarketState', function(state) {
            if (state == 'Open')
                setMarketState(true);
            else
                setMarketState(false);
        });
    }

    function assignStocks(stocks) {
        $scope.stocks = stocks;
    }

    function replaceStock(stock) {
        for (var count = 0; count < $scope.stocks.length; count++) {
            if ($scope.stocks[count].Symbol == stock.Symbol) {
                $scope.stocks[count] = stock;
            }
        }
    }

    function setMarketState(isOpen) {
        $scope.marketIsOpen = isOpen;
    }
});

app.filter('percentage', function () {
    return function(changeFraction) {
        return (changeFraction * 100).toFixed(2) + "%";
    };
});

app.filter('change', function () {
    return function(changeAmount) {
        if (changeAmount > 0) {
            return "▲ " + changeAmount.toFixed(2);
        } else if (changeAmount < 0) {
            return "▼ " + changeAmount.toFixed(2);
        } else {
            return changeAmount.toFixed(2);
        }
    };
});

app.directive('flash', function ($) {
    return function(scope, elem, attrs) {
        var flag = attrs.flash;
        var $elem = $(elem);

        function flashRow() {
            var value = scope.stock.LastChange;
            var changeStatus = scope.$eval(flag);
            if (changeStatus) {
                var bg = value === 0
                    ? '255,216,0' // yellow
                    : value > 0
                        ? '154,240,117' // green
                        : '255,148,148'; // red

                $elem.flash(bg, 1000);
            }
        }

        scope.$watch(flag, function(value) {
            flashRow();
        });
    };
});

app.directive('scrollTicker', function ($) {
    return function(scope, elem, attrs) {
        var $scrollTickerUI = $(elem);
        var flag = attrs.scrollTicker;
        scroll();

        function scroll() {
            if (scope.$eval(flag)) {
                var w = $scrollTickerUI.width();
                $scrollTickerUI.css({ marginLeft: w });
                $scrollTickerUI.animate({ marginLeft: -w }, 15000, 'linear', scroll);
            } else
                $scrollTickerUI.stop();
        }

        scope.$watch(flag, function(value) {
            scroll();
        });
    };
});

// reference if get stuck alternate angular hubs factory
// https://stuff2share.codeplex.com/SourceControl/latest#angular-signalr.js

// from http://henriquat.re/server-integration/signalr/integrateWithSignalRHubs.html
// Slight rerrange to reduce code duplicate of proxyOnApplyCallback code.
// Modified to allow parameters to be forwarded to invoked server side functions.
// Modified to allow events parameters to be passed back to handling function.
//
// this is still to callbacky...
// each of the on/off/invoke should be returning promises [and angular ones not jquery ones ?]
//
app.factory('signalRHubProxy', ['$rootScope', 'signalRServer',
    function($rootScope, signalRServer) {

        function signalRHubProxyFactory(serverUrl, hubName, startOptions) {
            var connection = $.hubConnection(signalRServer);
            var proxy = connection.createHubProxy(hubName);
            var hubStartPromise = connection.start(startOptions);

            function proxyEventOn(eventName, callback) {
                proxy.on(eventName, function () {
                    var eventNameArguments = arguments;
                    if (callback) {
                        $rootScope.$apply(function() {
                            callback.apply(callback, eventNameArguments);
                        });
                    }
                });
            }
            
            function proxyEventOff(eventName, callback) {
                proxy.off(eventName, function () {
                    var eventNameArguments = arguments;
                    if (callback) {
                        $rootScope.$apply(function () {
                            callback.apply(callback, eventNameArguments);
                        });
                    }
                });
            }
            
            // first parameter is methodName.
            // last parameter is always callback if argument length > 1.
            // rest of parameters are parameters to methodName.
            // now returns the promise (jquery) from the invoke. 
            //      Drawback down chain needs $apply using the promise... ick
            function proxyInvoke() {
                var len = arguments.length;
                var args = Array.prototype.slice.call(arguments); // convert to real array
                var callback = undefined;
                if (len > 1) {
                    callback = args.pop();
                }
                return proxy.invoke.apply(proxy, args)
                    .done(function (result) {
                        $rootScope.$apply(function () {
                                if (callback) {
                                    callback(result);
                                }
                            });
                        });
            }
            
            return {
                on: proxyEventOn,
                off: proxyEventOff,
                invoke: proxyInvoke,
                connection: connection,
                hubStartPromise: hubStartPromise
            };
        }

        return signalRHubProxyFactory;
    }]);

app.controller('ServerTimeController', function ($scope, signalRHubProxy) {
    var clientPushHubProxy = signalRHubProxy(
        signalRHubProxy.defaultServer, 'clientPushHub',
            { logging: true });
    var serverTimeHubProxy = signalRHubProxy(
        signalRHubProxy.defaultServer, 'serverTimeHub',
            { logging: true });

    clientPushHubProxy.on('serverTime', function (data) {
        $scope.currentServerTime = data;
        var x = clientPushHubProxy.connection.id;
    });

    $scope.getServerTime = function () {
        serverTimeHubProxy.invoke('getServerTime', function (data) {
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
