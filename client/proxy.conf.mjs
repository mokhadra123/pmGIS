// Angular dev-server proxy.
//
// The app always calls the API on its own origin under `/api` (see APP_CONFIG.api.baseUrl),
// so no build-time API URL is baked into the client and no CORS preflight is involved
// during development.
//
// When the dev server is launched by the .NET Aspire AppHost, Aspire injects the API's
// resolved address as a service-discovery environment variable. We read it here so the
// proxy follows whatever port Aspire assigned to `api`. Run standalone
// (`npm start` / `ng serve`), the variable is absent and we fall back to the API's
// launchSettings.json http profile.
const target =
  process.env['services__api__https__0'] ??
  process.env['services__api__http__0'] ??
  'http://localhost:5055';

export default {
  '/api': {
    target,
    changeOrigin: true,
    // Aspire's https endpoint uses the ASP.NET Core dev certificate.
    secure: false,
  },
};
