import { type ReactNode } from 'react';
import { Pin, PinOff, EyeOff, Link2 } from 'lucide-react';
import { useCopyToClipboard } from '../hooks/useCopyToClipboard';

interface Props {
  /** Stable key for the block within its entry (e.g. "0", "3.tab.1"). */
  blockKey: string;
  /** Anchor id used by deep-links and the in-entry search. */
  anchorId: string;
  /** Whether the block is pinned (renders inline indicator + flips pin icon). */
  pinned: boolean;
  /** Whether the block is hidden (renders a stub the user can click to restore). */
  hidden: boolean;
  /** Block type for the stub label when hidden. */
  blockType: string;
  /** Optional label for the stub. */
  label?: string;
  /** Hover-action callbacks. */
  onTogglePin: () => void;
  onToggleHide: () => void;
  /** Underlying block content. */
  children: ReactNode;
}

/**
 * Wraps each top-level content block with hover-revealed actions and a
 * stable anchor id. Hover actions:
 *   - copy-link: copies a URL with #anchorId to the clipboard
 *   - pin: bubbles the block to the top of the entry
 *   - hide: stashes the block into a "hidden blocks" tray
 *
 * When the block is hidden we don't render its content - just a small
 * stub the user can click to restore it.
 */
export function BlockShell({
  blockKey, anchorId, pinned, hidden, blockType, label, onTogglePin, onToggleHide, children,
}: Props) {
  const { copy, copied } = useCopyToClipboard();

  if (hidden) {
    return (
      <div id={anchorId} className="block-shell block-shell-hidden" data-block-key={blockKey}>
        <button
          type="button"
          className="block-shell-hidden-stub"
          onClick={onToggleHide}
          title="Show this block"
        >
          <EyeOff size={12} />
          <span>Hidden {blockType} block{label ? `: ${label}` : ''} — click to show</span>
        </button>
      </div>
    );
  }

  const copyLink = () => {
    const url = `${window.location.origin}${window.location.pathname}${window.location.search}#${anchorId}`;
    void copy(url);
  };

  return (
    <div
      id={anchorId}
      className={`block-shell ${pinned ? 'block-shell-pinned' : ''}`}
      data-block-key={blockKey}
    >
      <div className="block-shell-actions" aria-hidden="false">
        <button
          type="button"
          className="block-shell-action"
          onClick={copyLink}
          title={copied ? 'Link copied!' : 'Copy link to this block'}
          aria-label="Copy link to this block"
        >
          <Link2 size={12} />
        </button>
        <button
          type="button"
          className={`block-shell-action ${pinned ? 'block-shell-action-active' : ''}`}
          onClick={onTogglePin}
          title={pinned ? 'Unpin block' : 'Pin block to top'}
          aria-label={pinned ? 'Unpin block' : 'Pin block'}
        >
          {pinned ? <PinOff size={12} /> : <Pin size={12} />}
        </button>
        <button
          type="button"
          className="block-shell-action"
          onClick={onToggleHide}
          title="Hide block"
          aria-label="Hide block"
        >
          <EyeOff size={12} />
        </button>
      </div>
      {children}
    </div>
  );
}
