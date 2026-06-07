import { useEffect, useState, useRef, useCallback } from 'react';
import { Search, X, ChevronUp, ChevronDown } from 'lucide-react';

interface Props {
  /**
   * The entry-detail DOM container we search inside. Searching is done
   * via DOM walking - this avoids having to re-render content blocks
   * with highlight wrappers (which would be expensive and fragile for
   * mermaid / charts / etc).
   */
  containerRef: React.RefObject<HTMLElement | null>;
  /** External open/close control so Ctrl+F can drive it from the parent. */
  open: boolean;
  onOpen: (open: boolean) => void;
}

const HIGHLIGHT_CLASS = 'av-search-hit';
const ACTIVE_CLASS = 'av-search-hit-active';

/**
 * In-entry search overlay. Walks the text nodes inside `containerRef`,
 * wraps matches in <mark> tags, and lets the user step through hits
 * with Enter / Shift+Enter or the up/down buttons.
 *
 * Activated by Ctrl+F / Cmd+F (intercepts the browser default so the
 * native find dialog doesn't take over).
 */
export function EntrySearch({ containerRef, open, onOpen }: Props) {
  const [query, setQuery] = useState('');
  const [hitIndex, setHitIndex] = useState(0);
  const [hitCount, setHitCount] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);

  // Ctrl+F to open
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      const isOpenShortcut = (e.ctrlKey || e.metaKey) && (e.key === 'f' || e.key === 'F');
      if (isOpenShortcut) {
        e.preventDefault();
        onOpen(true);
        // Defer focus until after render
        setTimeout(() => inputRef.current?.focus(), 0);
        return;
      }
      if (e.key === 'Escape' && open) {
        e.preventDefault();
        onOpen(false);
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [open, onOpen]);

  // Highlight matches whenever the query (or the container's contents) change
  useEffect(() => {
    if (!open) {
      clearHighlights(containerRef.current);
      setHitCount(0);
      return;
    }
    if (!query.trim()) {
      clearHighlights(containerRef.current);
      setHitCount(0);
      setHitIndex(0);
      return;
    }
    const hits = highlightAll(containerRef.current, query);
    setHitCount(hits);
    setHitIndex((prev) => {
      const next = hits === 0 ? 0 : Math.min(prev, hits - 1);
      activateHit(containerRef.current, next);
      return next;
    });
    return () => { /* highlights cleared by next run or close */ };
  }, [query, open, containerRef]);

  const jump = useCallback((delta: number) => {
    setHitIndex((prev) => {
      if (hitCount === 0) return 0;
      const next = (prev + delta + hitCount) % hitCount;
      activateHit(containerRef.current, next);
      return next;
    });
  }, [hitCount, containerRef]);

  if (!open) return null;

  return (
    <div className="entry-search-bar" role="search">
      <Search size={14} />
      <input
        ref={inputRef}
        type="search"
        placeholder="Find in entry"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            e.preventDefault();
            jump(e.shiftKey ? -1 : 1);
          }
        }}
        aria-label="Find in entry"
      />
      <span className="entry-search-count">
        {hitCount === 0 ? '0' : `${hitIndex + 1}/${hitCount}`}
      </span>
      <button type="button" className="entry-search-step" onClick={() => jump(-1)} disabled={hitCount === 0} title="Previous (Shift+Enter)">
        <ChevronUp size={14} />
      </button>
      <button type="button" className="entry-search-step" onClick={() => jump(1)} disabled={hitCount === 0} title="Next (Enter)">
        <ChevronDown size={14} />
      </button>
      <button type="button" className="entry-search-close" onClick={() => { onOpen(false); setQuery(''); }} title="Close (Esc)">
        <X size={14} />
      </button>
    </div>
  );
}

function clearHighlights(root: HTMLElement | null) {
  if (!root) return;
  const marks = root.querySelectorAll(`mark.${HIGHLIGHT_CLASS}`);
  marks.forEach((m) => {
    const parent = m.parentNode;
    if (!parent) return;
    while (m.firstChild) parent.insertBefore(m.firstChild, m);
    parent.removeChild(m);
    parent.normalize();
  });
}

function highlightAll(root: HTMLElement | null, query: string): number {
  if (!root) return 0;
  clearHighlights(root);
  const needle = query.trim().toLowerCase();
  if (!needle) return 0;

  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
    acceptNode(node) {
      const parent = (node as Text).parentElement;
      if (!parent) return NodeFilter.FILTER_REJECT;
      // Skip text inside scripts / styles / the search bar itself.
      const tag = parent.tagName.toLowerCase();
      if (tag === 'script' || tag === 'style' || tag === 'mark') return NodeFilter.FILTER_REJECT;
      if (parent.closest('.entry-search-bar')) return NodeFilter.FILTER_REJECT;
      if (parent.closest('.block-shell-actions')) return NodeFilter.FILTER_REJECT;
      if (!(node as Text).data.toLowerCase().includes(needle)) return NodeFilter.FILTER_REJECT;
      return NodeFilter.FILTER_ACCEPT;
    },
  } as NodeFilter);

  const textNodes: Text[] = [];
  let current = walker.nextNode();
  while (current) {
    textNodes.push(current as Text);
    current = walker.nextNode();
  }

  let count = 0;
  for (const node of textNodes) {
    const text = node.data;
    const lower = text.toLowerCase();
    let start = 0;
    const fragments: (string | HTMLElement)[] = [];
    let idx: number;
    while ((idx = lower.indexOf(needle, start)) !== -1) {
      if (idx > start) fragments.push(text.slice(start, idx));
      const mark = document.createElement('mark');
      mark.className = HIGHLIGHT_CLASS;
      mark.textContent = text.slice(idx, idx + needle.length);
      fragments.push(mark);
      count++;
      start = idx + needle.length;
    }
    if (start < text.length) fragments.push(text.slice(start));
    if (fragments.length > 1 || fragments[0] instanceof HTMLElement) {
      const parent = node.parentNode;
      if (!parent) continue;
      for (const frag of fragments) {
        if (typeof frag === 'string') parent.insertBefore(document.createTextNode(frag), node);
        else parent.insertBefore(frag, node);
      }
      parent.removeChild(node);
    }
  }

  return count;
}

function activateHit(root: HTMLElement | null, index: number) {
  if (!root) return;
  const marks = root.querySelectorAll(`mark.${HIGHLIGHT_CLASS}`);
  marks.forEach((m, i) => {
    m.classList.toggle(ACTIVE_CLASS, i === index);
  });
  const active = marks[index] as HTMLElement | undefined;
  if (active) active.scrollIntoView({ block: 'center', behavior: 'smooth' });
}
