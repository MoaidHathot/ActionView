import { useCallback, useState } from 'react';

/**
 * useCopyToClipboard - thin wrapper around the Clipboard API that gives
 * the caller a transient "copied" flag for showing a 2-second checkmark.
 *
 * Falls back to a hidden <textarea> + document.execCommand for environments
 * where navigator.clipboard isn't available (older browsers, insecure
 * contexts like http://192.168.x.y).
 */
export function useCopyToClipboard(resetMs = 1800): {
  copy: (value: string) => Promise<boolean>;
  copied: boolean;
} {
  const [copied, setCopied] = useState(false);

  const copy = useCallback(
    async (value: string) => {
      const ok = await writeToClipboard(value);
      if (ok) {
        setCopied(true);
        window.setTimeout(() => setCopied(false), resetMs);
      }
      return ok;
    },
    [resetMs],
  );

  return { copy, copied };
}

async function writeToClipboard(value: string): Promise<boolean> {
  if (navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(value);
      return true;
    } catch {
      // fall through to legacy
    }
  }
  // Legacy fallback: a hidden textarea + document.execCommand('copy')
  try {
    const ta = document.createElement('textarea');
    ta.value = value;
    ta.style.position = 'fixed';
    ta.style.opacity = '0';
    document.body.appendChild(ta);
    ta.select();
    const ok = document.execCommand('copy');
    document.body.removeChild(ta);
    return ok;
  } catch {
    return false;
  }
}
