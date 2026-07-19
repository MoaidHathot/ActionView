import type { ActionCommand } from '../types';

/**
 * A human-readable, single-line preview of what an action will run — for the
 * "what does this button do?" disclosure. Safe to show: `{{SECRET}}` and
 * `{{param.NAME}}` placeholders are left unresolved (so no credentials leak),
 * and HTTP headers/body are intentionally omitted.
 */
export function commandPreview(cmd: ActionCommand | undefined): string {
  if (!cmd) return '';
  if (cmd.type === 'cli') {
    return [cmd.program ?? '', ...(cmd.args ?? [])]
      .filter((s) => s !== '')
      .map((s) => (/\s/.test(s) ? `"${s}"` : s))
      .join(' ');
  }
  const method = (cmd.method ?? 'POST').toUpperCase();
  return `${method} ${cmd.url ?? ''}`.trim();
}

/** Short label for the command kind, e.g. "CLI" / "HTTP". */
export function commandKind(cmd: ActionCommand | undefined): string {
  if (!cmd) return '';
  return cmd.type === 'cli' ? 'CLI' : 'HTTP';
}
