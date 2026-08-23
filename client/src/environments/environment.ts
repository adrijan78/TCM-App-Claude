/**
 * Production configuration. Values are replaced at build time via the `fileReplacements`
 * entry in angular.json, so nothing environment-specific is compiled into the source
 * (SPEC section 9 — the hosting target is deliberately undecided).
 *
 * `apiUrl` is left empty on purpose: a production build is expected to be served from the
 * same origin as the API, or to have this file replaced during deployment. An empty value
 * makes requests relative, which is the correct same-origin default.
 */
export const environment = {
  production: true,
  apiUrl: '/api',
};
