// version tag Compare1-Step-by-Step5-NamedFrames
mergeInto(LibraryManager.library, {
    DownloadFileFromUnity: function (fileNamePtr, base64DataPtr) {
        var fileName = UTF8ToString(fileNamePtr);
        var base64Data = UTF8ToString(base64DataPtr);

        // create link for download
        var link = document.createElement('a');
        link.download = fileName;
        
        // turn Base64 into Blob to avoid long URI string errors
        var byteCharacters = window.atob(base64Data);
        var byteNumbers = new Array(byteCharacters.length);
        for (var i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        var byteArray = new Uint8Array(byteNumbers);
        var blob = new Blob([byteArray], { type: 'application/octet-stream' });
        
        link.href = URL.createObjectURL(blob);
        
        // click on link to start downloading
        document.body.appendChild(link);
        link.click();
        
        // cleanup memory
        document.body.removeChild(link);
        URL.revokeObjectURL(link.href);
    }
});