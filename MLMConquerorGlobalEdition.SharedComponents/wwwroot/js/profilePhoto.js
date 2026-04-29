// Reads a file selected via <input type="file"> and returns it as a base64 data
// URL so the Blazor side can ship it to the API without a multipart upload.
// Keeping this in JS avoids round-tripping the entire file through SignalR as a
// .NET stream (which is brutal on Server-mode latency).

export function readFileAsBase64(inputElement) {
    return new Promise(function (resolve, reject) {
        if (!inputElement || !inputElement.files || inputElement.files.length === 0) {
            resolve(null);
            return;
        }
        var file = inputElement.files[0];
        // 5 MB limit — protects the SignalR pipe and the API's multipart body limit.
        if (file.size > 5 * 1024 * 1024) {
            reject('Image is larger than 5 MB. Please choose a smaller file.');
            return;
        }
        var reader = new FileReader();
        reader.onload = function () {
            // reader.result is a data URL: "data:image/png;base64,..."
            // We strip the prefix so the API can store just the raw base64 payload.
            var dataUrl = reader.result || '';
            var commaIdx = dataUrl.indexOf(',');
            var base64 = commaIdx >= 0 ? dataUrl.substring(commaIdx + 1) : dataUrl;
            resolve({
                base64: base64,
                contentType: file.type || 'image/jpeg',
                name: file.name,
                size: file.size
            });
        };
        reader.onerror = function () { reject('Could not read file.'); };
        reader.readAsDataURL(file);
    });
}

export function clickFilePicker(inputElement) {
    if (inputElement) inputElement.click();
}
