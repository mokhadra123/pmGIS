export const APP_CONFIG = {
  api: {
    // Same-origin: the dev server proxies /api, so no API URL is baked into the build.
    baseUrl: '/api',
  },
  arcgis: {
    projectLayerURL:
      'https://services3.arcgis.com/GVgbJbqm8hXASVYi/ArcGIS/rest/services/my_points/FeatureServer/0',
    searchField: 'name',

    // SOURCEID namespaces this application's features inside the shared sample
    // layer. The server allocates ids from the same base (see ProjectFeatureSync).
    sourceIdBase: 900_000,

    // Anonymous reverse geocoding for read-only address context on the picked point.
    // Failure here is never fatal: the address is decoration, not data we store.
    reverseGeocodeURL: 'https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer',
  },
  map: {
    initialCenter: [31.2357, 30.0444],
    initialZoom: 10,
    clusterMaxScale: 250_000,
    projectZoom: 15,
  },
  list: {
    pageSize: 25,
    searchDebounceMS: 300,
  },
  form: {
    autosaveIntervalMS: 30_000,
    draftStorageKey: 'pmgis.project-form.draft',
    descriptionMaxLength: 500,
  },
  boundary: {
    name: 'Egypt',
    extent: [24.7, 21.7, 36.9, 31.7] as [number, number, number, number],
  },
} as const;
