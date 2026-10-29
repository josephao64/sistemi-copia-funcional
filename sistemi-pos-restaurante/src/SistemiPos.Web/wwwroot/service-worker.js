const STATIC_CACHE = "sistemi-pos-restaurante-static-v2-pos-cash";
const CACHE_PREFIX = "sistemi-pos-restaurante-";
const DATABASE_NAME = "sistemi-pos-restaurante";
const DATABASE_VERSION = 1;
const STATIC_ASSETS = [
  "/Pos/Offline",
  "/css/site.css",
  "/js/site.js",
  "/js/product-pricing.js",
  "/js/pos-sync.js",
  "/js/pos-offline.js",
  "/js/pos-cash.js",
  "/js/checkout.js",
  "/js/pos-tab-lock.js",
  "/manifest.webmanifest",
  "/images/brand/sistemi-logo.png"
];

self.addEventListener("install", (event) => {
  event.waitUntil(caches.open(STATIC_CACHE).then((cache) => cache.addAll(STATIC_ASSETS)));
  self.skipWaiting();
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((key) => key.startsWith(CACHE_PREFIX) && key !== STATIC_CACHE).map((key) => caches.delete(key)))
    )
  );
  self.clients.claim();
});

self.addEventListener("fetch", (event) => {
  if (event.request.method !== "GET") {
    return;
  }

  const url = new URL(event.request.url);
  if (url.origin !== self.location.origin) {
    return;
  }

  if (event.request.mode === "navigate") {
    event.respondWith(handleNavigation(event.request));
    return;
  }

  if (!isOfflineStaticAsset(url.pathname)) {
    return;
  }

  event.respondWith(
    fetch(event.request).then((response) => {
      if (response.ok) {
        const copy = response.clone();
        caches.open(STATIC_CACHE).then((cache) => cache.put(event.request, copy));
      }
      return response;
    }).catch(() => caches.match(event.request))
  );
});

function isOfflineStaticAsset(pathname) {
  return pathname.startsWith("/css/")
    || pathname.startsWith("/js/")
    || pathname.startsWith("/images/")
    || pathname === "/manifest.webmanifest";
}

self.addEventListener("sync", (event) => {
  if (event.tag === "sistemi-pos-restaurante-sync") {
    event.waitUntil(syncPendingSalesInBackground());
  }
});

async function handleNavigation(request) {
  const url = new URL(request.url);
  let response;
  try {
    response = await fetch(request);
  } catch {
    if (url.pathname === "/Pos/Offline") {
      return (await caches.match("/Pos/Offline"));
    }
    return new Response("No se pudo conectar con el servidor. Verifique la conexión e intente nuevamente.", {
      status: 503,
      headers: { "Content-Type": "text/plain; charset=utf-8" }
    });
  }

  if (response.ok && url.pathname === "/Pos/Offline" && !response.headers.get("Cache-Control")?.includes("no-store")) {
    try {
      const cache = await caches.open(STATIC_CACHE);
      await cache.put(url.pathname, response.clone());
    } catch {
      // La respuesta en línea sigue siendo válida aunque el navegador no permita actualizar la caché offline.
    }
  }

  return response;
}

async function syncPendingSalesInBackground() {
  const sales = (await getAllPendingSales()).filter((sale) =>
    sale.syncStatus === "Pending" || sale.syncStatus === "Error" || sale.syncStatus === "Syncing");
  if (sales.length === 0) {
    await notifyClients();
    return;
  }

  for (const sale of sales) {
    sale.syncStatus = "Syncing";
    sale.error = null;
    await putPendingSale(sale);
  }

  try {
    const response = await fetch("/api/offline-pos/sync", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: JSON.stringify({ sales: sales.map(toSyncRequest) })
    });
    if (!response.ok) {
      throw new Error("El servidor rechazó la sincronización.");
    }

    const payload = await response.json();
    for (const result of payload.results) {
      const sale = await getPendingSale(result.clientOperationId);
      if (!sale) {
        continue;
      }
      sale.syncStatus = result.status;
      sale.finalSaleNumber = result.saleNumber;
      sale.error = result.error;
      await putPendingSale(sale);
    }
  } catch (error) {
    for (const sale of sales) {
      sale.syncStatus = "Error";
      sale.error = error.message || "Falló la sincronización con el servidor.";
      await putPendingSale(sale);
    }
    throw error;
  } finally {
    await notifyClients();
  }
}

function toSyncRequest(sale) {
  return {
    clientOperationId: sale.clientOperationId,
    localCreatedAtUtc: sale.localCreatedAtUtc,
    cashSessionId: sale.cashSessionId,
    customerId: sale.customerId,
    customerNit: sale.customerNit,
    customerName: sale.customerName,
    customerAddress: sale.customerAddress,
    customerPhone: sale.customerPhone,
    customerVisitOrigin: sale.customerVisitOrigin,
    paymentMethod: sale.paymentMethod,
    receivedAmount: sale.receivedAmount,
    transferSourceBank: sale.transferSourceBank,
    transferSenderName: sale.transferSenderName,
    transferBankAccountId: sale.transferBankAccountId,
    transferReference: sale.transferReference,
    transferAmount: sale.transferAmount,
    transferDateUtc: sale.transferDateUtc,
    transferStatus: sale.transferStatus,
    cardType: sale.cardType,
    cardLast4: sale.cardLast4,
    cardAuthorizationNumber: sale.cardAuthorizationNumber,
    cardVoucherReference: sale.cardVoucherReference,
    cardAmount: sale.cardAmount,
    cardStatus: sale.cardStatus,
    posDeviceId: sale.posDeviceId,
    items: sale.items.map((item) => ({
      productId: item.productId,
      quantity: item.quantity,
      discountPercent: item.discountPercent
    }))
  };
}

async function notifyClients() {
  const clients = await self.clients.matchAll({ includeUncontrolled: true, type: "window" });
  for (const client of clients) {
    client.postMessage({ type: "sistemi-pos-restaurante-sync-complete" });
  }
}

function openDatabase() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DATABASE_NAME, DATABASE_VERSION);
    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains("pendingSales")) {
        db.createObjectStore("pendingSales", { keyPath: "clientOperationId" });
      }
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function executePendingStore(mode, action) {
  const db = await openDatabase();
  return new Promise((resolve, reject) => {
    const transaction = db.transaction("pendingSales", mode);
    const request = action(transaction.objectStore("pendingSales"));
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
    transaction.onerror = () => reject(transaction.error);
  });
}

function getAllPendingSales() {
  return executePendingStore("readonly", (store) => store.getAll());
}

function getPendingSale(clientOperationId) {
  return executePendingStore("readonly", (store) => store.get(clientOperationId));
}

function putPendingSale(sale) {
  return executePendingStore("readwrite", (store) => store.put(sale));
}
