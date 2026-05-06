// Fraud-detection fingerprint helper.
// Loads FingerprintJS OSS (v4, MIT, free, no API key) from the public CDN and
// exposes a stable visitorId hash that the signup pages attach to outgoing
// payloads. Best-effort only — if anything fails, visitorId stays null and the
// signup flow proceeds normally (the backend treats it as optional).
window.fingerprintHelper = (function () {
    let _visitorId = null;
    let _initPromise = null;

    async function _doInitialize() {
        try {
            const FingerprintJS = await import('https://openfpcdn.io/fingerprintjs/v4');
            const fp = await FingerprintJS.load();
            const result = await fp.get();
            _visitorId = result && result.visitorId ? result.visitorId : null;
            return _visitorId;
        } catch (err) {
            // Degrade silently — never block enrollment because of fingerprint.
            console.warn('Fingerprint init failed:', err);
            return null;
        }
    }

    return {
        initialize: function () {
            if (_initPromise) return _initPromise;
            _initPromise = _doInitialize();
            return _initPromise;
        },
        getVisitorId: function () {
            return _visitorId;
        }
    };
})();
