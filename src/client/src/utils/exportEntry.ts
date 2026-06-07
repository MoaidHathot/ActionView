import type { Entry, ContentBlock, RichCell, TimelineEvent, GalleryImage, LinkItem } from '../types';

/**
 * Serialises an entry to a Markdown document. Lossy for blocks that have
 * no Markdown equivalent (chart, diagram, before/after, video player) -
 * those degrade to a short "[unsupported block type: X]" placeholder
 * plus any plain-text fields we can preserve (label, caption).
 */
export function entryToMarkdown(entry: Entry): string {
  const out: string[] = [];
  out.push(`# ${entry.title}`);
  if (entry.subtitle) out.push(`*${entry.subtitle}*`);
  out.push('');
  out.push(`- **Type:** ${entry.type}`);
  out.push(`- **Source:** ${entry.source}`);
  out.push(`- **Severity:** ${entry.severity}`);
  out.push(`- **Created:** ${entry.createdAt}`);
  if (entry.tags?.length) out.push(`- **Tags:** ${entry.tags.join(', ')}`);
  out.push('');
  for (const block of entry.content ?? []) {
    out.push(blockToMarkdown(block));
    out.push('');
  }
  return out.join('\n').replace(/\n{3,}/g, '\n\n').trim() + '\n';
}

function escapeCell(s: string): string {
  return String(s).replace(/\|/g, '\\|').replace(/\n/g, ' ');
}

function escapeHtml(s: string): string {
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

/**
 * Wraps a Markdown export in a minimal standalone HTML document.
 * Suitable for printing or attaching to a postmortem.
 *
 * Renders the Markdown as a <pre>-formatted block. For a properly
 * styled HTML version, callers should render the entry via ReactMarkdown
 * and use document.body.innerHTML or a server-side renderer.
 */
export function entryToHtml(entry: Entry, markdown: string): string {
  const safeTitle = escapeHtml(entry.title);
  const safeBody = escapeHtml(markdown);
  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>${safeTitle}</title>
<style>
  body { font-family: -apple-system, system-ui, "Segoe UI", sans-serif; max-width: 880px; margin: 24px auto; padding: 0 16px; color: #1f2937; line-height: 1.55; }
  pre.av-export { white-space: pre-wrap; word-wrap: break-word; background: #fafafa; border: 1px solid #e5e7eb; border-radius: 6px; padding: 16px; font: 13px/1.55 ui-monospace, SFMono-Regular, Menlo, monospace; }
  h1 { color: #111827; }
</style>
</head>
<body>
<h1>${safeTitle}</h1>
<pre class="av-export">${safeBody}</pre>
</body>
</html>
`;
}

/**
 * Triggers a browser download of `content` as `filename` with the given
 * MIME type. Used by the Export menu.
 */
export function downloadFile(filename: string, content: string, mime: string): void {
  const blob = new Blob([content], { type: mime });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

function blockToMarkdown(block: ContentBlock, depth = 0): string {
  const headingPrefix = '#'.repeat(Math.min(2 + depth, 6));
  const label = block.label ? `${headingPrefix} ${block.label}\n\n` : '';
  switch (block.type) {
    case 'markdown':
      return label + (typeof block.body === 'string' ? block.body : '');
    case 'code': {
      const code = typeof block.body === 'string' ? block.body : String(block.body ?? '');
      const lang = block.language ?? '';
      const filename = block.filename ? `**${block.filename}**\n\n` : '';
      return label + filename + '```' + lang + '\n' + code + '\n```';
    }
    case 'json': {
      const pretty = JSON.stringify(block.body, null, 2);
      return label + '```json\n' + pretty + '\n```';
    }
    case 'table': {
      const cols = block.columns ?? [];
      const rows = block.rows ?? [];
      if (cols.length === 0) return label + '*(empty table)*';
      const header = `| ${cols.join(' | ')} |`;
      const sep = `| ${cols.map(() => '---').join(' | ')} |`;
      const body = rows.map(r => `| ${r.map(cellToMarkdown).join(' | ')} |`).join('\n');
      return label + [header, sep, body].join('\n');
    }
    case 'keyValue': {
      const pairs = block.pairs ?? {};
      const lines = Object.entries(pairs).map(([k, v]) => `- **${k}:** ${cellToMarkdown(v as RichCell)}`);
      return label + lines.join('\n');
    }
    case 'link': {
      const items: LinkItem[] = block.links
        ?? (block.url ? [{ url: block.url, label: block.label, body: typeof block.body === 'string' ? block.body : undefined }] : []);
      return label + items.map(i => {
        const text = i.label ?? i.url;
        const body = i.body ? `  \n  ${i.body}` : '';
        return `- [${text}](${i.url})${body}`;
      }).join('\n');
    }
    case 'image': {
      const url = block.url ?? (typeof block.body === 'string' ? block.body : '');
      const alt = block.alt ?? block.label ?? '';
      const caption = block.caption ? `\n*${block.caption}*` : '';
      return label + `![${alt}](${url})${caption}`;
    }
    case 'gallery': {
      const images: GalleryImage[] = block.images ?? [];
      return label + images.map(img => `![${img.alt ?? ''}](${img.url})${img.caption ? `  \n*${img.caption}*` : ''}`).join('\n\n');
    }
    case 'video': {
      const url = block.url ?? (typeof block.body === 'string' ? block.body : '');
      return label + `**Video:** [${block.label ?? url}](${url})${block.caption ? `\n\n*${block.caption}*` : ''}`;
    }
    case 'file': {
      const url = block.url ?? '';
      const name = block.filename ?? url;
      return label + `**File:** [${name}](${url})`;
    }
    case 'diff': {
      const diff = typeof block.body === 'string' ? block.body : '';
      const name = block.newFilename ?? block.filename ?? '';
      return label + (name ? `**${name}**\n\n` : '') + '```diff\n' + diff + '\n```';
    }
    case 'diagram': {
      const src = typeof block.body === 'string' ? block.body : '';
      return label + '```mermaid\n' + src + '\n```';
    }
    case 'timeline': {
      const events: TimelineEvent[] = block.events ?? [];
      return label + events.map(e => `- **${e.at}** \u2014 ${e.label}${e.body ? `  \n  ${e.body.replace(/\n/g, '\n  ')}` : ''}`).join('\n');
    }
    case 'tabs': {
      const tabs = block.tabs ?? [];
      return label + tabs.map(t => {
        const inner = (t.content ?? []).map(c => blockToMarkdown(c, depth + 1)).join('\n\n');
        return `${headingPrefix}# ${t.label}\n\n${inner}`;
      }).join('\n\n');
    }
    case 'stat': {
      const v = block.value ?? '';
      const unit = block.unit ? ` ${block.unit}` : '';
      const delta = block.delta ? ` (${block.delta})` : '';
      return label + `**${v}${unit}**${delta}` + (block.caption ? `  \n${block.caption}` : '');
    }
    case 'alert': {
      const level = block.level ?? 'info';
      const body = typeof block.body === 'string' ? block.body : '';
      return `> [!${level.toUpperCase()}] ${block.label ? `**${block.label}**  \n> ` : ''}${body.split('\n').join('\n> ')}`;
    }
    case 'section': {
      const inner = (block.content ?? []).map(c => blockToMarkdown(c, depth + 1)).join('\n\n');
      return `${headingPrefix} ${block.title ?? block.label ?? 'Section'}\n\n${inner}`;
    }
    case 'beforeAfter':
      return label + `**Before:** ${block.beforeUrl ?? ''}\n\n**After:** ${block.afterUrl ?? ''}`;
    case 'chart':
      return label + `*[chart: ${block.chartType ?? 'line'}, ${(block.series ?? []).length} series]*${block.caption ? `\n\n${block.caption}` : ''}`;
    case 'divider':
      return '---';
    default:
      return label + `*[unsupported block type: ${block.type}]*`;
  }
}

function cellToMarkdown(cell: RichCell): string {
  if (typeof cell === 'string') return escapeCell(cell);
  if (!cell || typeof cell !== 'object') return escapeCell(String(cell ?? ''));
  switch (cell.type) {
    case 'text': return cell.mono ? '`' + cell.value + '`' : escapeCell(cell.value);
    case 'link': return `[${escapeCell(cell.label ?? cell.url)}](${cell.url})`;
    case 'status': return `**${cell.level}: ${escapeCell(cell.label)}**`;
    case 'badge': return `\`${escapeCell(cell.label)}\``;
    case 'code': return '`' + cell.value.replace(/`/g, '\\`') + '`';
    case 'copy': return '`' + (cell.display ?? cell.value) + '`';
    case 'markdown': return cell.value.replace(/\|/g, '\\|').replace(/\n/g, ' ');
    case 'image': return `![${cell.alt ?? ''}](${cell.url})`;
    default: return JSON.stringify(cell);
  }
}
