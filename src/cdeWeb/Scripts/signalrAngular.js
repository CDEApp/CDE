'use strict';

// Angular module to make integrating with SignalR simpler.
angular.module('signalrAngular', ['ng'])
    .factory('signalrHubFactory', ['$rootScope', function ($rootScope) {

        var slice = [].slice;
        var connections = { domains: [] };
        var defaultModuleOptions = {
            moduleLogging: false,
            signalrClientLogging: false,
            serverUrl: '',  // domain the hubs are on.
            hubStartOptions: { // signalr connection options.
                logging: false
            }
        };

        function HubProxy(connection, hubName, moduleOptions, scope) {
            function log(message) { if (moduleOptions.moduleLogging) { console.log(message); } }
            var baseScope = scope || $rootScope;

            return {
                srHubProxy: connection.createHubProxy(hubName),

                // Making this result a $q promise doesn't seem a good fit.
                // as it keeps getting called and promises are a one shot affair.
                // A callback is a better fit.
                on: function(eventName, callback) {
                    if (eventName && callback) {
                        var self = this;
                        self.srHubProxy.on(eventName, function () {
                            log('"' + hubName + '".on("' + eventName + '", f(arguments.length = "' + arguments.length + '"))');
                            callback.apply(callback, arguments);
                            baseScope.$digest();
                        });
                    }
                },
                
                off: function (eventName) {
                    var self = this;
                    self.srHubProxy.off(eventName);
                    log('"' + hubName + '".off(' + eventName + '", f())');
                },

                // Tried $q promise result bit its extra work for no apparent extra value
                // Any chained promise then needs to handle $apply context as well.
                invoke: function () { // params methodName, argsForMethodName..., callback
                    var srProxy = this.srHubProxy;
                    var len = arguments.length;
                    var args = slice.call(arguments); // convert to real array
                    var callback = undefined;
                    if (len > 1) {
                        callback = args.pop();
                    }
                    return srProxy.invoke.apply(srProxy, args)
                        .then(function (result) {
                            if (callback) {
                                callback(result);
                                baseScope.$digest();
                            }
                        });
                }
            };
        }

        // returned instance this[hubName] holds instances of ConnectProxy
        function ConnectProxy(connection, moduleOptions, scope) {
            function log(message) { if (moduleOptions.moduleLogging) { console.log(message); } }
            var baseScope = scope || $rootScope;

            return {
                connection: connection,
                startedPromise: null,
                myScope: baseScope,
                //moduleOptions: moduleOptions, // maybe for testing sometime.

                getHubProxy: function(hubName) {
                    var self = this;
                    var hubProxy = self[hubName];
                    if (!hubProxy) {
                        hubProxy = new HubProxy(connection, hubName, moduleOptions, baseScope);
                        self[hubName] = hubProxy;
                    }
                    return hubProxy;
                },

                // converting to $q promises brings no value as callee still needs to $apply
                start: function() {
                    var self = this;
                    if (!self.startedPromise) {
                        log('Starting connection...');
                        self.startedPromise = self.connection.start(moduleOptions.hubStartOptions);
                    }
                    return self.startedPromise;
                },

                stop: function() {
                    var self = this;
                    log('Stopping connection...');
                    var stop = self.connection.stop();
                    self.startedPromise = null;
                    return stop;
                },

                isStarted: function() {
                    return this.startedPromise !== null;
                },

                //
                // The following functions are event registration proxy's.
                // They manage Angular digest context for registered callback function.
                // The following functions have the same implementation.
                //   starting, received, stateChanged, error, disconnected,
                //   connectionSlow, reconnecting, reconnected.
                //
                // $$phase is used because a UI event like a button click that calls
                // ConnectionProxy.stop() may well be running in the digest context
                // of the click, and SignalR can send some notifications in same
                // digest context.
                //

                starting: function(cb) {
                    var self = this;
                    return self.connection.starting(function () {
                        log('starting');
                        cb.apply(self, arguments);
                        if (!baseScope.$$phase) { baseScope.$digest(); }
                    });
                },
                received: function(cb) {
                    var self = this;
                    return self.connection.received(function () {
                        log('received');
                        cb.apply(self, arguments);
                        if (!baseScope.$$phase) { baseScope.$digest(); }
                    });
                },
                stateChanged: function(cb) {
                    var self = this;
                    return self.connection.stateChanged(function () {
                        log('stateChanged');
                        cb.apply(self, arguments);
                        if (!baseScope.$$phase) { baseScope.$digest(); }
                    });
                },
                error: function(cb) {
                    var self = this;
                    return self.connection.error(function () {
                        log('error');
                        cb.apply(self, arguments);
                        if (!baseScope.$$phase) { baseScope.$digest(); }
                    });
                },
                disconnected: function(cb) {
                    var self = this;
                    return self.connection.disconnected(function () {
                        log('disconnected');
                        cb.apply(self, arguments);
                        if (!baseScope.$$phase) { baseScope.$digest(); }
                    });
                },
                connectionSlow: function(cb) {
                    var self = this;
                    return self.connection.connectionSlow(function () {
                        log('connectionSlow');
                        cb.apply(self, arguments);
                        if (!baseScope.$$phase) { baseScope.$digest(); }
                    });
                },
                reconnecting: function(cb) {
                    var self = this;
                    return self.connection.reconnecting(function () {
                        log('reconnecting');
                        cb.apply(self, arguments);
                        if (!baseScope.$$phase) { baseScope.$digest(); }
                    });
                },
                reconnected: function (cb) {
                    var self = this;
                    return self.connection.reconnected(function () {
                        log('reconnected');
                        cb.apply(self, arguments);
                        if (!baseScope.$$phase) { baseScope.$digest(); }
                    });
                }

            };
        }

        function newConnectProxy(moduleOptions, scope) {
            var connection = $.hubConnection(moduleOptions.serverUrl);
            connection.logging = moduleOptions.signalrClientLogging;
            return new ConnectProxy(connection, moduleOptions, scope);
        }

        function connectProxyFactory(ngModuleOptions, scope) {
            var moduleOptions = $.extend(true, {}, defaultModuleOptions, ngModuleOptions || {});

            var serverUrl = moduleOptions.serverUrl;
            if (!connections[serverUrl]) {
                connections[serverUrl] = newConnectProxy(moduleOptions, scope);
                connections.domains.push(serverUrl);
            }
            return connections[serverUrl];
        }

        return connectProxyFactory;
    }]);

/*
// desires.
// 1. connection per url [actually per domain] DONE
// 2. one start() per domain. [can happen before events registered] DONE
// 3. provide promise from start(). DONE
// 4. can stop() connection which affects all hubs on it. DONE
// 5. make getHub() be smart check for local browser connection by another window first.
//    use local communications to other window and don't open direct connection.
//    eg of way to do this https://github.com/diy/intercom.js
//

//
// USAGE
//
// Order of loading of javascript files.
//     jquery
//     jquery.signalR
//     angular
//     signalrAngular
//
// In your angular app add 'signalrAngular' to dependencies.
//
// Example.

app.value('ngModuleOptions', {
    moduleLogging: true,
    signalrClientLogging: true,
    hubStartOptions: { logging: true }
});

// include signalrAngular.js to register factory 'signalrHubFactory'
// Example of creating a personalised hub factory
app.factory('myHubFactory', function (signalrHubFactory, ngModuleOptions) {
    return signalrHubFactory(ngModuleOptions);
});

// example controller
app.controller('Controller1', function ($scope, myHubFactory) {

    var proxyPush = myHubFactory.getHubProxy('clientPushHub');

    myHubFactory.start().then(function () {
        $scope.hubActive1 = true;
    });

    $scope.getServerTime = function() {
        proxyPush.invoke('getServerTime', function(data) {
            console.log('pull serverTime ' + data);
            $scope.pullTime = data;
        });
    };
});

// example controller
app.controller('Controller2', function ($scope, myHubFactory) {

    var proxyTime = myHubFactory.getHubProxy('serverTimeHub');

    myHubFactory.start().then(function () {
        $scope.hubActive2 = true;
    });

    proxyTime.on('serverTime', function (data) {
        console.log('push serverTime ' + data);
        $scope.pushTime = data;
    });
});

//
// hubFactory.start() to start connection
// - this returns a promise so you can chain then() or fail() as desired..
// - then() and fail() require $apply for integration to Angular digest cycle.
// hubFactory.stop() to stop connection
//
// start() is required once per unique serverUrl to allow hubs to work on that connection.
// at least one on() on a hub must be registered before start() completes.
//    if it isn't then none of the on() events will trigger.
//
// The DefaultServerUrl is just '' and connects to sites source.
//
// To do something once connection started, or error on start then use
// the returned jquery promise from .start().
//
// To find out if connection is started() call hubFactory.isStarted().
//

//
// For Hub factory.
//
// .on() does an $apply for you on the parameter callback function.
// - is simple event, so will run every function() attached to it.
// - if the 2nd parameter null it does nothing.
//
// .off() stops the function being called.
// - it removes functions for the given event in one step it seems.
//
// .invoke() does an $apply for you on the parameter callback function.
// - returns the jquery promise.
// - so if you wish to handle errors calls .fail(function(error) { })
// - this error isn't called if network is down.
// - what occurs is that connection.error() handler occurs.
//
// - you can register callbacks on the factory for [ signalr events ]
//   starting(cb), received(cb),
//   stateChanged(cb), error(cb), disconnected(cb), connectionSlow(cb),
//   reconnecting(cb), reconnected(cb)
//


//
// See defaultModuleOptions for module options.
// If logging truth then enables a few different console log messages.
//

// If lose connection and hit disconnected state. a start() wont reconnect.
// but it seems a stop() and start() will reconnect.

// serverUrl should work - but isn't really test at moment.

//
// IDEA could put a start() for each hub... as i hold my startPromise
// and can pass it back for each hub ? do care or not ? hmm not really at moment....
//

 // not to impressed with signalr library.
 // it uses 'delete' where it could just set field null

// TODO there current is no way to discard a connectProxy... ? care ?
*/