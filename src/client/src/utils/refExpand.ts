import type { ContentBlock, Entry } from '../types';

function blockText(b: ContentBlock | undefined): string {
  if (!b) return '';
  if (typeof b.body === 'string') return b.body;
  if (typeof b.value === 'string') return b.value;
  return b.title ?? b.label ?? '';
}

function findById(blocks: ContentBlock[] | undefined, id: string): ContentBlock | undefined {
  if (!blocks) return undefined;
  for (const b of blocks) {
    if (b.id === id) return b;
    const nested = findById(b.content, id);
    if (nested) return nested;
  }
  return undefined;
}

/**
 * Client-side expansion of {{content.*}} / {{entry.*}} references, used to
 * pre-fill an action's parameter form (and previews) from the current entry so
 * a `default` of "{{content.self}}" shows the actual comment text. The server
 * performs the authoritative expansion at execution time; this only mirrors it
 * for display. {{param.NAME}} and {{SECRET}} are intentionally left untouched.
 */
export function expandRefs(text: string, entry: Entry | undefined, self?: ContentBlock): string {
  if (!text || !entry) return text;
  return text.replace(/\{\{(content|entry)\.([A-Za-z0-9_-]+)\}\}/g, (m, ns, key) => {
    if (ns === 'content') {
      if (key === 'self') return blockText(self);
      return blockText(findById(entry.content, key));
    }
    switch (String(key).toLowerCase()) {
      case 'title': return entry.title;
      case 'subtitle': return entry.subtitle ?? '';
      case 'type': return entry.type;
      case 'id': return entry.id;
      case 'source': return entry.source;
      case 'severity': return entry.severity;
      case 'tags': return entry.tags.join(', ');
      default: return m;
    }
  });
}
