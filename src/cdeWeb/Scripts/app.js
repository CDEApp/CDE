'use strict';
// ReSharper disable InconsistentNaming

var cdeWebApp = angular.module('cdeweb', [
    'ngResource',
    'ngCookies',
    'ui.directives',
    'restangular'
]);

cdeWebApp.config(function($routeProvider) {
    $routeProvider
        .when('/about', {
            controller: 'cdeWebAboutCtrl',
            templateUrl: 'partials/about.html',
            header: 'partials/navbar.html'
            // hack ensure it grabs a new one when loaded, 
            // todo consider a version tag appended for when version bumps ?
            // and just make sure expire app.js, or version.js hourly... ?
            //header: 'partials/navbar.html?r='+Math.random()
        })
        .when('/search/:query', {
            controller: 'cdeWebCtrl',
            templateUrl: 'partials/cdeWeb.html',
            header: 'partials/navbar.html'
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
    //
    
    //r.setResponseExtractor(function(response, operation, what, url) {
    //    if (operation === 'getList') {
    //        // rearrange odata response.
    //        var newResponse = response.value;
    //        newResponse['odata.metadata'] = response['odata.metadata'];
    //        newResponse['odata.nextLink'] = response['odata.nextLink'];
    //        return newResponse;
    //    } else {
    //        return response;
    //    }
    //});
});

cdeWebApp.directive('selectall', function () {
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
});

cdeWebApp.run(function($rootScope, $route) {
    $rootScope.layoutPartial = function (partialName) {
        //console.log('$route.current[partialName]', $route.current[partialName]);
        return $route.current[partialName];
    };

    // $routeChangeStart
    // $routeChangeSuccess

    $rootScope.$on('$routeChangeSuccess', function(next, current) {
        // if route changed ? cause a search - this is to cause a search on route load to happen ?
    });
});


//cdeWebApp.run(function ($rootScope, $templateCache) {
//    //// helps on not caching partials, not sure why as browser is caching not js code.
//    //// but this runs on ever time view content is loaded - far to often.
//    //$rootScope.$on('$viewContentLoaded', function () {
//    //    // this addresses cached partials not refreshing with edits..
//    //    console.log('ker-flush!');
//    //    $templateCache.removeAll();
//    //});
//    
//    //$templateCache.removeAll();  // see if just on app load it does its thing.. doesnt work
//});

var controllers = {};

controllers.cdeWebCopyCtrl = function($scope, $location, $routeParams) {
    $scope.resultPath = $routeParams.path;
    console.log('$location', $location);
    console.log('$routeParams.path', $routeParams.path);
    //window.prompt("Copy to clipboard: Ctrl+C, Enter", $scope.resultPath);
    //after copy leave ? //$location.path('/#');
};

controllers.navbarCtrl = function ($scope, $location, DataModel, DirEntryRepository) {
    console.log('navbarCtrl init');
    $scope.data = DataModel;


    // modify ui-reset to only have remove icon visible if input has content.
    // This logic could be pushed back into uiReset possibly ? makes for easier styling control ?
    $scope.$watch('data.query', function queryWatch() {
        if ($scope.model === null || $scope.data.query === null || $scope.data.query === "") {
            $('a.hascontent').removeClass('hascontent');
        } else {
            $('a.ui-reset').addClass('hascontent'); // TODO use the default ui-reset class for this..
        }
        //console.log("$scope.data.query:" + $scope.data.query);
    });
    
    $scope.clearResult = function () {
        $scope.data.searchResult = [];
    };

    $scope.clearQuery = function () {
        $scope.data.query = '';
    };

    $scope.searchInputActive = function() {
        return document.activeElement.id === 'search';
    };
    
    $scope.search = function () {
        console.log('navbarCtrl.search [' + $scope.data.query + ']');
        //console.log(document.activeElement, document.activeElement.id);
        $scope.data.searchInputActive = $scope.searchInputActive();
        $scope.clearResult();
        $location.path('/search/' + $scope.data.query);

        DirEntryRepository.get($scope.data.query)
            .then(function (blah) {
                $scope.data.metadata = blah['odata.metadata'];
                $scope.data.nextLink = blah['odata.nextLink'];
                $scope.data.searchResult = blah.value;
                //console.log('nextLink', $scope.data.nextLink);
                //console.log('value', $scope.data.searchResult.length);
            });
    };
    
    $scope.haveResults = function () {
        return $scope.data.searchResult.length !== 0;
    };
};

cdeWebApp.controller(controllers);

cdeWebApp.controller('cdeWebAboutCtrl', function ($rootScope, $scope, $location, DataModel) {
    console.log('cdeWebAboutCtrl init');
    $scope.data = DataModel;

});

// todo make Escape key in input query - clear it... or maybe two escapes clears it ?
cdeWebApp.controller('cdeWebCtrl', function ($scope, $location, DataModel) {
    console.log('cdeWebCtrl init');
    $scope.data = DataModel;
    console.log('$scope.data.query', $scope.data.query);
    console.log('$scope.data.searchResult.length', $scope.data.searchResult.length);
    //$scope.search();
    
    $scope.haveResults = function () {
        return $scope.data.searchResult.length !== 0;
    };
    
    // use web browser modal dialog for clipboard copy hackery. ABONDONED at moment.
    $scope.copyPathDialog = function (path) {
        console.log('path ' + path);
        $('#copyPathDialog').modal({});
    };
});

cdeWebApp.directive('restoresearchfocus', function() {
    return function (scope, element) {
        if (scope.data.searchInputActive && !scope.searchInputActive()) {
            element[0].focus();
            scope.data.searchInputActive = false;
        }
    };
});

cdeWebApp.service('DirEntryRepository', function (Restangular) {
        this.get = function(substring) {
            console.log('DirEntryRepository.get()');
            var baseDE = Restangular.all("DirEntries" + substringFilter(substring, 'Name'));
            return baseDE.getList();
        };
    })
    .factory('DataModel', function($rootScope, $cookies) {
        var data = $rootScope.data;;
        if (!data) {
            console.log('DataModel new');
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


