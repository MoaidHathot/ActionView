// Utilities for resolving image URLs that entries may reference.
//
// Background: browsers refuse to load file:// URLs from an http:// origin,
// so any markdown body or image block that points at a local file path can
// never load directly. ActionView's API exposes /api/files?path=... (gated
// by fileAccess.allowedRoots in actionview.json) to serve those files, and
// this helper rewrites file:// URLs to route through that endpoint.
//
// http(s) and data: URLs are returned unchanged. Anything we don't recognise
// is also returned unchanged so existing behaviour is preserved.

/**
 * Rewrite a single URL string so that file:// URLs go through the
 * /api/files proxy. http(s)://, data:, and relative URLs are unchanged.
 */
export function rewriteImageUrl(url: string | undefined): string {
  if (!url) return '';
  // file:// URI — needs to be proxied via /api/files
  if (/^file:\/\//i.test(url)) {
    let localPath: string;
    try {
      const parsed = new URL(url);
      // URL.pathname on a Windows file:// URL gives "/C:/foo/bar.jpg"; strip
      // the leading slash so the server receives a normal absolute path. On
      // Unix it gives "/var/foo/bar.jpg" which we want to keep verbatim.
      localPath = decodeURIComponent(parsed.pathname);
      if (/^\/[A-Za-z]:\//.test(localPath)) {
        localPath = localPath.slice(1);
      }
    } catch {
      // Couldn't parse — fall back to a naive prefix strip so we still try.
      localPath = url.replace(/^file:\/\/+/, '');
    }
    return `/api/files?path=${encodeURIComponent(localPath)}`;
  }
  // Bare Windows-looking absolute path that snuck through (e.g. an entry
  // author dropped C:\foo\bar.jpg straight into an image src). Treat it
  // the same way as a file:// URL.
  if (/^[A-Za-z]:[\\/]/.test(url)) {
    return `/api/files?path=${encodeURIComponent(url)}`;
  }
  return url;
}
