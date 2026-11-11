// Entrega al navegador un archivo pedido por el HttpClient con token (un <a href> no lleva el JWT).
// Es un módulo de SharedComponents para que sirva en cualquier portal sin tocar su página de inicio.
export function downloadFile(fileName, contentType, base64) {
    var byteChars = atob(base64);
    var bytes = new Uint8Array(byteChars.length);
    for (var i = 0; i < byteChars.length; i++) bytes[i] = byteChars.charCodeAt(i);
    var blob = new Blob([bytes], { type: contentType || 'application/octet-stream' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = fileName || 'download';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(function () { URL.revokeObjectURL(url); }, 1500);
}
