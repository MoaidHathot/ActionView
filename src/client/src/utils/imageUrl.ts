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

/**
 * urlTransform for ReactMarkdown that allows the schemes an ActionView
 * entry might legitimately use for images and links:
 *
 *   - http, https        — public web URLs (default-allowed)
 *   - mailto, tel        — common link schemes (default-allowed)
 *   - data:              — inline base64 / svg images (entries from
 *                          orchestrators that embed thumbnails directly)
 *   - file:              — local files, rewritten by rewriteImageUrl
 *                          downstream to go through /api/files
 *   - relative paths     — anything without a scheme is left as-is
 *
 * Explicitly blocks `javascript:` (XSS) and any other scheme we don't
 * recognise. The default ReactMarkdown urlTransform strips `data:` and
 * `file:` URLs to empty string before our custom `img` component sees
 * them — that's why entries with inline-base64 or file:// images were
 * rendering as "[image: missing src]" in 0.16.x.
 *
 * See: https://github.com/remarkjs/react-markdown - urlTransform option.
 */
export function allowEntryImageUrl(value: string): string {
  // No scheme? Treat as relative. Same heuristic ReactMarkdown's default
  // uses: a colon before any `?`, `#`, or `/` means it's a scheme.
  const colon = value.indexOf(':');
  if (colon < 0) return value;
  const slash = value.indexOf('/');
  const question = value.indexOf('?');
  const hash = value.indexOf('#');
  if (
    (slash > -1 && colon > slash) ||
    (question > -1 && colon > question) ||
    (hash > -1 && colon > hash)
  ) {
    return value;
  }
  const scheme = value.slice(0, colon).toLowerCase();
  if (ALLOWED_SCHEMES.has(scheme)) return value;
  return '';
}

const ALLOWED_SCHEMES = new Set([
  'http', 'https', 'mailto', 'tel', 'data', 'file', 'irc', 'ircs', 'xmpp',
]);

