import { useState, useEffect } from 'react';
import { AlertCircle, AlertTriangle, Info, CheckCircle, X } from 'lucide-react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import type { ContentBlock } from '../../types';
import { ActionButton } from '../ActionButton';
import { allowEntryImageUrl } from '../../utils/imageUrl';

interface Props {
  block: ContentBlock;
  entryId?: string;
  /** Stable key for persisting the dismissed state in localStorage. */
  blockKey?: string;
  /** Optional callback when an action inside the alert is clicked. */
  onAction?: (actionIndex: number, parameters?: Record<string, string>) => void;
}

const icons = {
  info: Info,
  warning: AlertTriangle,
  error: AlertCircle,
  success: CheckCircle,
} as const;

/**
 * Colored callout box. Supports:
 *   - Four severity levels with matching icon + colors
 *   - Markdown body (block.body / block.label is now a heading, not a clobber)
 *   - Dismissible state persisted in localStorage (block.dismissible)
 *   - Action buttons rendered beneath the body (block.actions[])
 */
export function AlertBlock({ block, entryId, blockKey, onAction }: Props) {
  const level = block.level ?? 'info';
  const Icon = icons[level] ?? Info;
  const heading = block.label;
  const message = typeof block.body === 'string' ? block.body : String(block.body ?? '');

  // Persisted dismiss state - per-block, scoped to entry id so a re-issued
  // entry with the same id+block-position keeps its dismissed state.
  const storageKey = blockKey ? `actionview.alert-dismissed.${blockKey}` : null;
  const [dismissed, setDismissed] = useState<boolean>(() => {
    if (!storageKey || !block.dismissible) return false;
    try { return localStorage.getItem(storageKey) === '1'; } catch { return false; }
  });

  useEffect(() => {
    if (!storageKey) return;
    try {
      if (dismissed) localStorage.setItem(storageKey, '1');
      else localStorage.removeItem(storageKey);
    } catch { /* localStorage unavailable */ }
  }, [storageKey, dismissed]);

  if (dismissed) return null;

  return (
    <div className={`block-alert block-alert-${level}`}>
      <Icon size={16} className="alert-icon" />
      <div className="alert-content">
        {heading && <div className="alert-heading">{heading}</div>}
        {message && (
          <div className="alert-message">
            <ReactMarkdown remarkPlugins={[remarkGfm]} urlTransform={allowEntryImageUrl}>{message}</ReactMarkdown>
          </div>
        )}
        {block.actions && block.actions.length > 0 && (
          <div className="alert-actions">
            {block.actions.map((action, i) => (
              <ActionButton
                key={i}
                action={action}
                draftKey={`${entryId ?? 'alert'}.${blockKey ?? 'alert'}.${i}`}
                onClick={(parameters) => onAction?.(i, parameters)}
              />
            ))}
          </div>
        )}
      </div>
      {block.dismissible && (
        <button
          type="button"
          className="alert-dismiss"
          onClick={() => setDismissed(true)}
          title="Dismiss"
          aria-label="Dismiss alert"
        >
          <X size={14} />
        </button>
      )}
    </div>
  );
}
