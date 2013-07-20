/// <reference path="jasmine.js"/>
/// <reference path="../cdeWeb/Scripts/jquery-2.0.2.js"/>
/// <reference path="../cdeWeb/Scripts/angular.js"/>
/// <reference path="../cdeWeb/Scripts/angular-mocks.js"/>
/// <reference path="../cdeWeb/Scripts/signalrAngular.js"/>

// myScope causes circular object which console.log() errors on.
//var s = testFactory.myScope;
//testFactory.myScope = null; // can cause circulars on console.log
//console.log(testFactory);
//testFactory.myScope = s;

var logging = false;
function log(message) { if (logging) { console.log(message); } }

describe('signalrAngular', function () {
    beforeEach(module('signalrAngular')); // module under test
    beforeEach(function () {
        //log('__A0');
        //console.log('> ' + this.getFullName());
    });

    it('can get an instance of factory (tests if angular mocks in place)', inject(function (signalrHubFactory) {
        expect(signalrHubFactory).toBeDefined();
    }));

    describe('with factory,', function () {
        var _srConnection = { };
        beforeEach(function () {
            log('__A00');
            $.hubConnection = jasmine.createSpy('mockHubConnection').andReturn(_srConnection);
        });

        describe('basic constructor', function () {

            it('default serverUrl', inject(function (signalrHubFactory) {
                signalrHubFactory();
                expect($.hubConnection).toHaveBeenCalledWith('');
            }));

            it('sets serverUrl from options', inject(function (signalrHubFactory) {
                signalrHubFactory({ serverUrl: 'serverUrl.com' });
                expect($.hubConnection).toHaveBeenCalledWith('serverUrl.com');
            }));

            it('default connection.logging', inject(function (signalrHubFactory) {
                var testFactory = signalrHubFactory();
                expect(testFactory.connection.logging).toEqual(false);
            }));

            it('sets connection.logging from options', inject(function (signalrHubFactory) {
                var testFactory = signalrHubFactory({ signalrClientLogging: true });
                expect(testFactory.connection.logging).toEqual(true);
            }));

            // removed as don't want to expose moduleOptions on connectProxy - to brittle
            //it('default moduleLogging', inject(function (signalrHubFactory) {
            //    var testFactory = signalrHubFactory();
            //    expect(testFactory.moduleOptions.moduleLogging).toEqual(false); // some other way than using moduleOptions ?
            //}));

            // removed as don't want to expose moduleOptions on connectProxy - to brittle
            //it('sets moduleLogging from options', inject(function (signalrHubFactory) {
            //    var testFactory = signalrHubFactory({ moduleLogging: true });
            //    expect(testFactory.moduleOptions.moduleLogging).toEqual(true); // some other way than using moduleOptions ?
            //}));
        });

        describe('with testProxy,', function () {
            var testProxy;
            beforeEach(inject(function (signalrHubFactory) {
                log('__A000');
                var _mockStartResponse = { a: 'mockStart' };
                _srConnection.start = jasmine.createSpy('mockStart').andReturn(_mockStartResponse);

                testProxy = signalrHubFactory({
                    moduleLogging: false,
                    signalrClientLogging: true,
                    serverUrl: 'server'
                });
            }));

            it('proxy.itStarted() false before start.', function () {
                expect(testProxy.isStarted()).toEqual(false);
            });

            describe('proxy.start(),', function () {
                var startResult;
                beforeEach(function () {
                    log('__A0000');
                    startResult = testProxy.start();
                });

                it('calls connection.start()', function () {
                    expect(_srConnection.start).toHaveBeenCalled();
                });

                it('returns value returned from start()', function () {
                    expect(startResult).toEqual({ a: 'mockStart' });
                });

                it('proxy.itStarted() true after start.', function () {
                    expect(testProxy.isStarted()).toEqual(true);
                });

                it('proxy.itStarted() false after start and stop.', function () {
                    _srConnection.stop = jasmine.createSpy('mockStop');
                    testProxy.stop();
                    expect(testProxy.isStarted()).toEqual(false);
                });

                it('second start() returns first result', function () {
                    _srConnection.start = jasmine.createSpy('mockStart2').andReturn({ b: 'mockStart2'});
                    var startResult2 = testProxy.start();
                    expect(startResult2).toEqual({ a: 'mockStart' });
                });

                it('stop() then start(), different start result', function () {
                    _srConnection.stop = jasmine.createSpy('mockStop');
                    _srConnection.start = jasmine.createSpy('mockStart2').andReturn({ c: 'mockStart3'});

                    testProxy.stop();
                    var startResult2 = testProxy.start();

                    expect(startResult2).toEqual({ c: 'mockStart3' });
                });
            });

            describe('testProxy2,', function () {

                it('same serverUrl same proxy', inject(function (signalrHubFactory) {
                    $.hubConnection = jasmine.createSpy('mockHubConnection').andReturn({ extraField: 'value' });
                    var testProxy2 = signalrHubFactory({
                        moduleLogging: false,
                        signalrClientLogging: true,
                        serverUrl: 'server'
                    });

                    expect(testProxy2).toEqual(testProxy);
                }));

                describe('new Proxy different serverUrl,', function () {
                    var testProxy2;

                    beforeEach(inject(function (signalrHubFactory) {
                        log('__A00000');
                        testProxy.extraField = 'extra'; // touch instance to Assert
                        testProxy2 = signalrHubFactory({
                            moduleLogging: false,
                            signalrClientLogging: true,
                            serverUrl: 'serverX'
                        });
                    }));

                    it('not same as testProxy', function () {
                        expect(testProxy2.extraField).toBeUndefined();
                    });
                });
            });

            describe('proxy.stop(),', function () {

                beforeEach(function () {
                    log('__A0001');
                    _srConnection.stop = jasmine.createSpy('mockStop');
                });

                it('calls connection.stop()', function () {
                    testProxy.stop();
                    expect(_srConnection.stop).toHaveBeenCalled();
                });
            });

            describe('getHubProxy(),', function () {
                beforeEach(function () {
                    log('__A0002');
                    _srConnection.createHubProxy =
                        jasmine.createSpy('mockCreateHubProxy')
                            .andReturn({ a: 'srHubProxy' });
                });

                describe('test hubProxy,', function () {
                    var testHubProxy;
                    beforeEach(function () {
                        log('__A00020');
                        testHubProxy = testProxy.getHubProxy('testHub');
                    });

                    it('calls connection.createHubProxy()', function () {
                        expect(_srConnection.createHubProxy).toHaveBeenCalled();
                    });

                    it('returns createHubProxy() result', function () {
                        expect(testHubProxy.srHubProxy).toEqual({ a: 'srHubProxy' });
                    });


                });
            });

            describe('with testHubProxy,', function () {
                var _srHubProxyOn;
                var _srHubProxyOff;
                var _srHubProxyInvoke;
                var capturedOnCB;
                var capturedOnEventName;
                var testHubProxy;
                var capturedInvokeArgs;
                var invokeResultPromise;

                beforeEach(function () {
                    log('__A0003');
                    invokeResultPromise = $.Deferred();
                    _srHubProxyOn = jasmine
                        .createSpy('mockHubProxyOn')
                        .andCallFake(function (eventName, cb) {
                            log(' mockHubProxyOn');
                            capturedOnEventName = eventName;
                            capturedOnCB = cb;
                        });
                    _srHubProxyOff =  jasmine
                        .createSpy('mockHubProxyOff');
                    _srHubProxyInvoke =  jasmine
                        .createSpy('mockHubProxyInvoke')
                        .andCallFake(function (methodName) {
                            log('_mockHubProxyInvoke');
                            capturedInvokeArgs = arguments;
                            return invokeResultPromise;
                        });
                    var _srHubProxy = {
                        on: _srHubProxyOn,
                        off: _srHubProxyOff,
                        invoke: _srHubProxyInvoke
                    };

                    _srConnection.createHubProxy =
                        jasmine.createSpy('mockCreateHubProxy')
                            .andReturn(_srHubProxy);

                    testHubProxy = testProxy.getHubProxy('testHub');
                });

                it('off() calls through', function () {
                    testHubProxy.off('mockEvent');
                    expect(_srHubProxyOff).toHaveBeenCalled();
                });

                describe('on(),', function () {
                    it('registers function', function () {
                        testHubProxy.on('mockEvent', function () {});
                        expect(_srHubProxyOn).toHaveBeenCalled();
                    });

                    it('event callback $digest called', inject(function ($rootScope) {
                        testHubProxy.on('mockEvent', function () {});
                        spyOn($rootScope, '$digest');
                        capturedOnCB();
                        expect($rootScope.$digest).toHaveBeenCalled();
                    }));

                    it('event 0 params right', function () {
                        testHubProxy.on('mockEvent', function () {
                            expect(arguments.length).toEqual(0);
                        });
                        capturedOnCB();
                    });

                    it('event 1 params right', function () {
                        testHubProxy.on('mockEvent', function () {
                            expect(arguments.length).toEqual(1);
                            expect(arguments[0]).toEqual('A');
                        });
                        capturedOnCB('A');
                    });

                    it('event 2 params right', function () {
                        testHubProxy.on('mockEvent', function () {
                            expect(arguments.length).toEqual(2);
                            expect(arguments[0]).toEqual(12);
                            expect(arguments[1]).toEqual({ x: 5 });
                        });
                        capturedOnCB(12, { x: 5 });
                    });
                });

                it('invoke() calls through', function () {
                    testHubProxy.invoke('dummyMethodName');
                    expect(_srHubProxyInvoke).toHaveBeenCalled();
                });

                it('invoke() methodName passes 1 arg through', function () {
                    testHubProxy.invoke('dummyMethodName');
                    expect(capturedInvokeArgs.length).toEqual(1);
                });

                it('invoke() methodName + cb passes 1 arg through', function () {
                    testHubProxy.invoke('dummyMethodName', 'dummyCallBack');
                    expect(capturedInvokeArgs.length).toEqual(1);
                });

                it('invoke() methodName + arg + cb passes 2 arg through', function () {
                    testHubProxy.invoke('dummyMethodName', 333, 'dummyCallBack');
                    expect(capturedInvokeArgs.length).toEqual(2);
                    expect(capturedInvokeArgs[0]).toEqual('dummyMethodName');
                    expect(capturedInvokeArgs[1]).toEqual(333);
                });

                it('invoke() callback run with $digest', inject(function ($rootScope) {
                    var invokeResultCallback = jasmine.createSpy('mockCallback');
                    testHubProxy.invoke('dummyMethodName', invokeResultCallback);
                    spyOn($rootScope, '$digest');

                    invokeResultPromise.resolve('resolved promise');

                    expect($rootScope.$digest).toHaveBeenCalled();
                }));

                it('invoke() no callback so no $digest run', inject(function ($rootScope) {
                    testHubProxy.invoke('dummyMethodName');
                    spyOn($rootScope, '$digest');

                    invokeResultPromise.resolve('resolved promise');

                    expect($rootScope.$digest).not.toHaveBeenCalled();
                }));

                it('invoke() rejected promise so no $digest run', inject(function ($rootScope) {
                    testHubProxy.invoke('dummyMethodName');
                    spyOn($rootScope, '$digest');

                    invokeResultPromise.resolve('resolved promise');

                    expect($rootScope.$digest).not.toHaveBeenCalled();
                }));

            });

            describe('event register,', function () {
                beforeEach(function () {
                    log('__A0003');
                    _srConnection.starting = jasmine.createSpy('mockStarting');
                    _srConnection.received = jasmine.createSpy('mockReceived');
                    _srConnection.stateChanged = jasmine.createSpy('mockStateChanged');
                    _srConnection.error = jasmine.createSpy('mockError');
                    _srConnection.disconnected = jasmine.createSpy('mockDisconnected');
                    _srConnection.connectionSlow = jasmine.createSpy('mockConnectionSlow');
                    _srConnection.reconnecting = jasmine.createSpy('mockReconnecting');
                    _srConnection.reconnected = jasmine.createSpy('mockReconnected');
                });

                describe('call correct proxy method,', function () {
                    it('connection.starting', function () {
                        testProxy.starting();
                        expect(_srConnection.starting).toHaveBeenCalled();
                    });
                    it('connection.received', function () {
                        testProxy.received();
                        expect(_srConnection.received).toHaveBeenCalled();
                    });
                    it('connection.stateChanged', function () {
                        testProxy.stateChanged();
                        expect(_srConnection.stateChanged).toHaveBeenCalled();
                    });
                    it('connection.error', function () {
                        testProxy.error();
                        expect(_srConnection.error).toHaveBeenCalled();
                    });
                    it('connection.disconnected', function () {
                        testProxy.disconnected();
                        expect(_srConnection.disconnected).toHaveBeenCalled();
                    });
                    it('connection.connectionSlow', function () {
                        testProxy.connectionSlow();
                        expect(_srConnection.connectionSlow).toHaveBeenCalled();
                    });
                    it('connection.reconnecting', function () {
                        testProxy.reconnecting();
                        expect(_srConnection.reconnecting).toHaveBeenCalled();
                    });
                    it('connection.reconnected', function () {
                        testProxy.reconnected();
                        expect(_srConnection.reconnected).toHaveBeenCalled();
                    });
                });
            });

            // representative test for all 8 event registers which have same code.
            describe('test stateChanged() type functions,', function () {
                var capturedStateChangedCB;

                beforeEach(function () {
                    log('__A0003');
                    _srConnection.stateChanged = jasmine
                        .createSpy('mockStateChanged')
                        .andCallFake(function (cb) {
                            capturedStateChangedCB = cb;
                        });
                });

                it('callback 0 params right', function () {
                    testProxy.stateChanged(function () {
                        expect(arguments.length).toEqual(0);
                    });
                    capturedStateChangedCB();
                });

                it('callback 1 params right', function () {
                    testProxy.stateChanged(function () {
                        expect(arguments.length).toEqual(1);
                        expect(arguments[0]).toEqual(31.3);
                    });
                    capturedStateChangedCB(31.3);
                });

                it('callback 2 params right', function () {
                    testProxy.stateChanged(function () {
                        expect(arguments.length).toEqual(2);
                        expect(arguments[0]).toEqual('2');
                        expect(arguments[1]).toEqual(234);
                    });
                    capturedStateChangedCB('2', 234);
                });

                it('$apply then no $digest()', inject(function ($rootScope) {
                    // this happens in web page from a button click tied to
                    // a scope method that calls stop() then signalr in same context
                    // fires the disconnected and stateChange so they are still
                    // inside an active $apply scope.
                    testProxy.stateChanged(function () { });
                    spyOn($rootScope, '$digest');
                    $rootScope.$apply(function () {
                        capturedStateChangedCB();
                        expect($rootScope.$digest).not.toHaveBeenCalled();
                    });
                }));

                it('no $apply active then $digest', inject(function ($rootScope) {
                    testProxy.stateChanged(function () { });
                    spyOn($rootScope, '$digest');
                    capturedStateChangedCB();
                    expect($rootScope.$digest).toHaveBeenCalled();
                }));
            });
        });
    });
});


