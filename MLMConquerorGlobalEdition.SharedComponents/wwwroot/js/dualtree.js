// Drag-to-pan for the binary tree visualizer container.
// Position state is stored on the element so resetPan can clear it without re-attaching listeners.

export function initPan(containerId) {
    var body = document.getElementById(containerId);
    if (!body || body._panInit) return;
    body._panInit = true;
    body._tx = 0;
    body._ty = 0;
    body.style.userSelect = 'none';
    body.style.cursor = 'grab';

    var dragging = false, moved = false;

    body.addEventListener('mousedown', function (e) {
        if (e.button !== 0) return;
        dragging = true;
        moved = false;
        body.style.cursor = 'grabbing';
        e.preventDefault();
    });

    window.addEventListener('mouseup', function () {
        if (dragging) {
            dragging = false;
            if (body) body.style.cursor = 'grab';
        }
    });

    body.addEventListener('mousemove', function (e) {
        if (!dragging) return;
        if (!moved &&
            Math.abs(e.movementX) < 2 &&
            Math.abs(e.movementY) < 2) return;
        moved = true;
        body._tx += e.movementX;
        body._ty += e.movementY;
        var inner = body.querySelector('.genealogy-tree');
        if (inner) inner.style.transform = 'translate(' + body._tx + 'px,' + body._ty + 'px)';
    });

    // Capture-phase click suppression: block Blazor click handlers when user dragged.
    body.addEventListener('click', function (e) {
        if (moved) {
            e.stopPropagation();
            moved = false;
        }
    }, true);
}

export function resetPan(containerId) {
    var body = document.getElementById(containerId);
    if (!body) return;
    body._tx = 0;
    body._ty = 0;
    var inner = body.querySelector('.genealogy-tree');
    if (inner) inner.style.transform = '';
}
