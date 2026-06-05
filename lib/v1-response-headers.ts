const V1_RESPONSE_HEADERS = [
  "Retry-After",
  "X-RateLimit-Limit",
  "X-RateLimit-Remaining",
  "X-RateLimit-Reset",
] as const;

export function copyV1ResponseHeaders(source: Headers, target: Headers) {
  for (const name of V1_RESPONSE_HEADERS) {
    const value = source.get(name);
    if (value !== null) {
      target.set(name, value);
    }
  }
}
