import { useState, useMemo } from 'react';
import { parsePatch, structuredPatch } from 'diff';
import type { ContentBlock } from '../../types';
import { CopyButton } from '../CopyButton';

interface Props {
  block: ContentBlock;
}

interface Hunk {
  oldStart: number;
  oldLines: number;
  newStart: number;
  newLines: number;
  header: string;
  lines: HunkLine[];
}

interface HunkLine {
  kind: 'add' | 'del' | 'context' | 'header';
  oldLine: number | null;
  newLine: number | null;
  text: string;
}

/**
 * Renders a unified-diff string with real add/remove/context gutters,
 * collapsible per-hunk, and a copy button.
 *
 * Two layouts:
 *   - "unified" (default): GitHub-style single column with line-number
 *     gutters for the old and new files side-by-side.
 *   - "split": two-column view with old on the left and new on the right.
 *
 * Input formats accepted in `block.body`:
 *   - A full unified diff with `--- a/...` / `+++ b/...` / `@@ ... @@` headers.
 *   - Just the hunk body (no headers); we'll fake a single hunk.
 */
export function DiffBlock({ block }: Props) {
  const raw = typeof block.body === 'string' ? block.body : String(block.body ?? '');
  const mode = block.mode === 'split' ? 'split' : 'unified';
  const [layout, setLayout] = useState<'unified' | 'split'>(mode);

  const hunks = useMemo(() => parseDiff(raw), [raw]);

  const addCount = hunks.reduce((n, h) => n + h.lines.filter(l => l.kind === 'add').length, 0);
  const delCount = hunks.reduce((n, h) => n + h.lines.filter(l => l.kind === 'del').length, 0);

  return (
    <div className="block-diff">
      <div className="diff-header">
        <div className="diff-header-left">
          {(block.oldFilename || block.newFilename || block.filename) && (
            <span className="diff-filename">
              {block.oldFilename && block.newFilename && block.oldFilename !== block.newFilename
                ? `${block.oldFilename} \u2192 ${block.newFilename}`
                : (block.newFilename ?? block.filename ?? block.oldFilename)}
            </span>
          )}
          {!block.filename && block.label && <span className="code-label">{block.label}</span>}
          <span className="diff-stats">
            <span className="diff-stat-add">+{addCount}</span>
            <span className="diff-stat-del">-{delCount}</span>
          </span>
        </div>
        <div className="diff-header-right">
          <div className="diff-mode-toggle" role="tablist">
            <button
              type="button"
              className={layout === 'unified' ? 'diff-mode-btn diff-mode-btn-active' : 'diff-mode-btn'}
              onClick={() => setLayout('unified')}
              role="tab"
              aria-selected={layout === 'unified'}
            >Unified</button>
            <button
              type="button"
              className={layout === 'split' ? 'diff-mode-btn diff-mode-btn-active' : 'diff-mode-btn'}
              onClick={() => setLayout('split')}
              role="tab"
              aria-selected={layout === 'split'}
            >Split</button>
          </div>
          <CopyButton value={raw} iconSize={13} />
        </div>
      </div>

      <div className="diff-body">
        {hunks.length === 0 && (
          <div className="diff-empty">No diff content to display.</div>
        )}
        {hunks.map((hunk, i) => (
          <HunkView key={i} hunk={hunk} layout={layout} />
        ))}
      </div>
    </div>
  );
}

function HunkView({ hunk, layout }: { hunk: Hunk; layout: 'unified' | 'split' }) {
  const [collapsed, setCollapsed] = useState(false);
  return (
    <div className={`diff-hunk ${collapsed ? 'diff-hunk-collapsed' : ''}`}>
      <button
        type="button"
        className="diff-hunk-header"
        onClick={() => setCollapsed(c => !c)}
        title={collapsed ? 'Expand hunk' : 'Collapse hunk'}
      >
        {hunk.header}
      </button>
      {!collapsed && (layout === 'unified' ? <UnifiedHunk hunk={hunk} /> : <SplitHunk hunk={hunk} />)}
    </div>
  );
}

function UnifiedHunk({ hunk }: { hunk: Hunk }) {
  return (
    <table className="diff-table diff-table-unified">
      <tbody>
        {hunk.lines.map((line, i) => (
          <tr key={i} className={`diff-row diff-row-${line.kind}`}>
            <td className="diff-gutter diff-gutter-old">{line.oldLine ?? ''}</td>
            <td className="diff-gutter diff-gutter-new">{line.newLine ?? ''}</td>
            <td className="diff-marker">{line.kind === 'add' ? '+' : line.kind === 'del' ? '-' : ' '}</td>
            <td className="diff-line"><span className="diff-line-text">{line.text}</span></td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function SplitHunk({ hunk }: { hunk: Hunk }) {
  // Align added/removed pairs: when a deletion is immediately followed by an
  // addition we put them on the same row; otherwise we leave the other side
  // blank. Context lines occupy both sides.
  type Pair = { left: HunkLine | null; right: HunkLine | null };
  const pairs: Pair[] = [];
  let i = 0;
  while (i < hunk.lines.length) {
    const line = hunk.lines[i];
    if (line.kind === 'context') {
      pairs.push({ left: line, right: line });
      i++;
      continue;
    }
    if (line.kind === 'del') {
      const next = hunk.lines[i + 1];
      if (next && next.kind === 'add') {
        pairs.push({ left: line, right: next });
        i += 2;
        continue;
      }
      pairs.push({ left: line, right: null });
      i++;
      continue;
    }
    if (line.kind === 'add') {
      pairs.push({ left: null, right: line });
      i++;
      continue;
    }
    i++;
  }
  return (
    <table className="diff-table diff-table-split">
      <tbody>
        {pairs.map((pair, i) => (
          <tr key={i} className="diff-row">
            <td className={`diff-gutter diff-gutter-old ${pair.left?.kind === 'del' ? 'diff-cell-del' : ''}`}>
              {pair.left?.oldLine ?? ''}
            </td>
            <td className={`diff-line ${pair.left?.kind === 'del' ? 'diff-cell-del' : ''}`}>
              {pair.left && <span className="diff-line-text">{pair.left.text}</span>}
            </td>
            <td className={`diff-gutter diff-gutter-new ${pair.right?.kind === 'add' ? 'diff-cell-add' : ''}`}>
              {pair.right?.newLine ?? ''}
            </td>
            <td className={`diff-line ${pair.right?.kind === 'add' ? 'diff-cell-add' : ''}`}>
              {pair.right && <span className="diff-line-text">{pair.right.text}</span>}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function parseDiff(raw: string): Hunk[] {
  if (!raw.trim()) return [];

  // Try the standard parser first; it handles full unified diffs with file headers.
  try {
    const patches = parsePatch(raw);
    const result: Hunk[] = [];
    for (const patch of patches) {
      for (const h of patch.hunks ?? []) {
        result.push(materialiseHunk({
          oldStart: h.oldStart,
          oldLines: h.oldLines,
          newStart: h.newStart,
          newLines: h.newLines,
          lines: h.lines,
        }));
      }
    }
    if (result.length > 0) return result;
  } catch {
    /* fall through */
  }

  // If the input is just a bare hunk-body (lots of `+` / `-` / ` ` prefixed
  // lines with no header), wrap it in a synthetic patch and re-parse.
  const lines = raw.split('\n');
  const hunk = materialiseHunk({
    oldStart: 1,
    oldLines: lines.filter(l => l.startsWith(' ') || l.startsWith('-')).length,
    newStart: 1,
    newLines: lines.filter(l => l.startsWith(' ') || l.startsWith('+')).length,
    lines,
  });
  return [hunk];
}

function materialiseHunk(raw: {
  oldStart: number; oldLines: number; newStart: number; newLines: number; lines: string[];
}): Hunk {
  const header = `@@ -${raw.oldStart},${raw.oldLines} +${raw.newStart},${raw.newLines} @@`;
  let oldLine = raw.oldStart;
  let newLine = raw.newStart;
  const out: HunkLine[] = [];
  for (const line of raw.lines) {
    if (line.startsWith('+')) {
      out.push({ kind: 'add', oldLine: null, newLine, text: line.slice(1) });
      newLine++;
    } else if (line.startsWith('-')) {
      out.push({ kind: 'del', oldLine, newLine: null, text: line.slice(1) });
      oldLine++;
    } else if (line.startsWith('\\')) {
      // "\ No newline at end of file" - skip
    } else {
      const text = line.startsWith(' ') ? line.slice(1) : line;
      out.push({ kind: 'context', oldLine, newLine, text });
      oldLine++;
      newLine++;
    }
  }
  return {
    oldStart: raw.oldStart,
    oldLines: raw.oldLines,
    newStart: raw.newStart,
    newLines: raw.newLines,
    header,
    lines: out,
  };
}

// Re-exported so callers can build a diff inline if they have before/after
// strings rather than a pre-formatted unified diff (not used by the renderer
// but exposed for completeness / future server-side helpers).
export { structuredPatch };
