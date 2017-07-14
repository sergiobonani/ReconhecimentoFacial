angular.module('myFaceApp', [])
.controller('faceSimilarCtrl', function ($scope, FileUploadService) {
    $scope.Title = 'Microsoft FaceAPI - Find Face Similar';
    $scope.resultMessage = 'No result found!!';
    $scope.SelectedFileForUpload = null;
    $scope.UploadedFiles = [];
    $scope.SimilarFace = [];

    //File Select & Save 
    $scope.selectCandidateFileforUpload = function (file) {
        $scope.SelectedFileForUpload = file;
        $scope.loaderMoreupl = true;
        $scope.uplMessage = 'Uploading, please wait....!';
        $scope.result = "color-red";

        //Save File
        var uploaderUrl = "/FaceSimilar/SaveCandidateFiles";
        var fileSave = FileUploadService.UploadFile($scope.SelectedFileForUpload, uploaderUrl);
        fileSave.then(function (response) {
            if (response.data.Status) {
                $scope.GetCandidateFile();
                angular.forEach(angular.element("input[type='file']"), function (inputElem) {
                    angular.element(inputElem).val(null);
                });
                $scope.f1.$setPristine();
                //$scope.uplMessage = response.data.Message;
                $scope.loaderMoreupl = false;
            }
        },
        function (error) {
            console.warn("Error: " + error);
        });
    }

    $scope.GetCandidateFile = function () {
        $scope.loaderMore = true;
        $scope.faceMessage = 'Preparing, please wait....!';
        $scope.result = "color-red";

        var fileUrl = "/FaceSimilar/GetCandidateFiles";
        var fileView = FileUploadService.GetUploadedFile(fileUrl);
        fileView.then(function (response) {
            $scope.UploadedFiles = response.data.FacesCollection;
            $scope.resultFaceMessage = response.data.Message;
            $scope.loaderMore = false;
        },
        function (error) {
            console.warn("Error: " + error);
        });
    };

    $scope.selectFileforFindSimilar = function (file) {
        $scope.SelectedFileForUpload = file;
        $scope.loaderMorefacefinder = true;
        $scope.facefinderMessage = 'Preparing, detecting faces, please wait....!';
        $scope.result = "color-red";

        //Find Similar Face
        var uploaderUrl = "/FaceSimilar/FindSimilar";
        var fileSave = FileUploadService.UploadFile($scope.SelectedFileForUpload, uploaderUrl);
        fileSave.then(function (response) {
            if (response.data.Status) {
                $scope.QueryFace = response.data.SimilarFace[0].QueryFace.FilePath;
                $scope.SimilarFace = response.data.SimilarFace[0].Faces;
                angular.forEach(angular.element("input[type='file']"), function (inputElem) {
                    angular.element(inputElem).val(null);
                });
                $scope.f2.$setPristine();
                $scope.resultMessage = response.data.Message;
                $scope.loaderMoreupl = false;
            }
        },
        function (error) {
            console.warn("Error: " + error);
        });
    }
})
.factory('FileUploadService', function ($http, $q) {
    var fact = {};
    fact.UploadFile = function (files, uploaderUrl) {
        var formData = new FormData();
        angular.forEach(files, function (f, i) {
            formData.append("file", files[i]);
        });
        var request = $http({
            method: "post",
            url: uploaderUrl,
            data: formData,
            withCredentials: true,
            headers: { 'Content-Type': undefined },
            transformRequest: angular.identity
        });
        return request;
    }
    fact.GetUploadedFile = function (fileUrl) {
        return $http.get(fileUrl);
    }
    return fact;
})