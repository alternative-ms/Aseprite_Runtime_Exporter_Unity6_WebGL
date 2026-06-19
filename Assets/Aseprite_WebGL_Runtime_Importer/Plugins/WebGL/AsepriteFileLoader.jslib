// version tag Compare1-Step-by-Step5-NamedFrames
mergeInto(LibraryManager.library, {
    TriggerFileOpenDialog: function (objectNamePtr, methodNamePtr) {
        var objectName = UTF8ToString(objectNamePtr);
        var methodName = UTF8ToString(methodNamePtr);

        // get or make a hidden input for file select
        var fileInput = document.getElementById('unity-aseprite-loader-input');
        if (!fileInput) {
            fileInput = document.createElement('input');
            fileInput.id = 'unity-aseprite-loader-input';
            fileInput.type = 'file';
            fileInput.accept = '.ase,.aseprite';
            fileInput.style.display = 'none';
            document.body.appendChild(fileInput);
        }

        fileInput.onchange = function (event) {
            var file = event.target.files[0];
            if (!file) return;

            var fileName = file.name;
            var reader = new FileReader();
            
            reader.onload = function (e) {
                var arrayBuffer = e.target.result;
                var bytes = new Uint8Array(arrayBuffer);
                
                // turn into Base64 to secure transfer into Unity
                var binary = '';
                var len = bytes.byteLength;
                for (var i = 0; i < len; i++) {
                    binary += String.fromCharCode(bytes[i]);
                }
                var base64String = window.btoa(binary);

                var combinedData = fileName + "|" + base64String;

                // transfer combined data into Unity GameObject
                SendMessage(objectName, methodName, combinedData);
                
                // reset input to allow select file again
                fileInput.value = '';
            };
            reader.readAsArrayBuffer(file);
        };

        fileInput.click();
    }
});