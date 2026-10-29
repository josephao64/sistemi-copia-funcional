(() => {
    const heartbeatMilliseconds = 2000;
    const staleMilliseconds = 6500;
    const claimVerificationMilliseconds = 100;

    const createId = () => globalThis.crypto?.randomUUID?.()
        ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`;

    document.addEventListener("DOMContentLoaded", () => {
        const pos = document.querySelector("[data-pos-tab-lock]");
        const blocked = document.querySelector("[data-pos-tab-blocked]");
        if (!pos || !blocked) return;

        const storageKey = `sistemi:pos-tab:${encodeURIComponent(pos.dataset.posUserKey || "sesion")}`;
        const ownerId = createId();
        let heartbeatId = null;
        let ownsLock = false;
        let posInitialized = false;
        let acquisitionPending = false;

        const readLock = () => {
            try {
                const value = localStorage.getItem(storageKey);
                return value ? JSON.parse(value) : null;
            } catch {
                return null;
            }
        };

        const isFresh = lock => lock
            && typeof lock.updatedAt === "number"
            && Date.now() - lock.updatedAt < staleMilliseconds;

        const writeLock = () => {
            try {
                localStorage.setItem(storageKey, JSON.stringify({ ownerId, updatedAt: Date.now() }));
                return true;
            } catch {
                return false;
            }
        };

        const isCurrentOwner = () => readLock()?.ownerId === ownerId;

        const showBlocked = () => {
            pos.hidden = true;
            blocked.hidden = false;
        };

        const showPos = () => {
            blocked.hidden = true;
            pos.hidden = false;
            if (!posInitialized) {
                posInitialized = true;
                initializePos();
            }
        };

        const stopHeartbeat = () => {
            if (heartbeatId !== null) window.clearInterval(heartbeatId);
            heartbeatId = null;
        };

        const releaseLock = () => {
            stopHeartbeat();
            const canRelease = ownsLock && isCurrentOwner();
            ownsLock = false;
            if (!canRelease) return;
            try { localStorage.removeItem(storageKey); } catch { }
        };

        window.releaseSistemiPosTabLock = releaseLock;

        const loseLock = () => {
            ownsLock = false;
            stopHeartbeat();
            showBlocked();
        };

        const startHeartbeat = () => {
            stopHeartbeat();
            heartbeatId = window.setInterval(() => {
                const current = readLock();
                if (isFresh(current) && current.ownerId !== ownerId) {
                    loseLock();
                    return;
                }
                writeLock();
            }, heartbeatMilliseconds);
        };

        const acquireLock = async () => {
            if (acquisitionPending || ownsLock) return ownsLock;
            acquisitionPending = true;
            const current = readLock();
            if (isFresh(current) && current.ownerId !== ownerId) {
                acquisitionPending = false;
                showBlocked();
                return false;
            }

            if (!writeLock()) {
                acquisitionPending = false;
                showPos();
                return true;
            }

            await new Promise(resolve => window.setTimeout(resolve, claimVerificationMilliseconds));
            ownsLock = isCurrentOwner();
            acquisitionPending = false;
            if (!ownsLock) {
                showBlocked();
                return false;
            }

            showPos();
            startHeartbeat();
            return true;
        };

        window.addEventListener("storage", event => {
            if (event.key !== storageKey) return;
            const current = readLock();
            if (ownsLock && isFresh(current) && current.ownerId !== ownerId) {
                loseLock();
                return;
            }
            if (!ownsLock && !isFresh(current)) void acquireLock();
        });
        window.addEventListener("pagehide", releaseLock);
        window.addEventListener("beforeunload", releaseLock);
        window.addEventListener("pageshow", event => {
            if (event.persisted) void acquireLock();
        });

        void acquireLock();
    });
})();
