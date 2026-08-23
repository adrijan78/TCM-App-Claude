/**
 * Local development. The API runs on its own port under the `http` launch profile
 * (see server/TCM.Api/Properties/launchSettings.json), and CORS on the server is
 * configured to allow http://localhost:4200 via `Cors:AllowedOrigins` in user-secrets.
 */
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5102/api',
};
