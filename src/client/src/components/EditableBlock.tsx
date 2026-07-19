import { useState, useCallback } from 'react';
import type { ReactNode } from 'react';
import { Pencil, X, Check, RotateCcw, GitCompare } from 'lucide-react';
import { diffLines } from 'diff';
import type { ContentBlock, Entry } from '../types';
import { api } from '../api/client';

interface Props {
  block: ContentBlock;
  entryId: string;
  path: number[];
  onEntryChanged?: (entry: Entry) => void;
  children: ReactNode;
}

function currentText(b: ContentBlock): string {
  if (typeof b.body === 'string') return b.body;
  if (typeof b.value === 'string') return b.value;
  return b.title ?? b.label ?? '';
}

/**
 * Wraps an editable content block with an inline editor: a pencil to edit the
 * text (persisted to the entry, capturing the original), an "edited" pill, a
 * diff of original → current, and a one-click revert. Edits flow into any
 * command that references the block via {{content.self}} / {{content.ID}}.
 */
export function EditableBlock({ block, entryId, path, onEntryChanged, children }: Props) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState('');
  const [saving, setSaving] = useState(false);
  const [showOriginal, setShowOriginal] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const startEdit = useCallback(() => {
    setDraft(currentText(block));
    setEditing(true);
    setError(null);
  }, [block]);

  const save = useCallback(async () => {
    setSaving(true);
    setError(null);
    try {
      const updated = await api.updateBlock(entryId, path, draft);
      onEntryChanged?.(updated);
      setEditing(false);
    } catch (e) {
      setError(String(e));
    } finally {
      setSaving(false);
    }
  }, [entryId, path, draft, onEntryChanged]);

  const revert = useCallback(async () => {
    if (!window.confirm('Revert this block to its original text?')) return;
    setError(null);
    try {
      const updated = await api.revertBlock(entryId, path);
      onEntryChanged?.(updated);
      setShowOriginal(false);
    } catch (e) {
      setError(String(e));
    }
  }, [entryId, path, onEntryChanged]);

  if (editing) {
    return (
      <div className="editable-block editable-editing">
        <textarea
          className="editable-textarea"
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          rows={8}
          autoFocus
        />
        {error && <div className="editable-error">{error}</div>}
        <div className="editable-actions">
          <button className="action-btn action-primary" onClick={save} disabled={saving}>
            <Check size={14} /> {saving ? 'Saving…' : 'Save'}
          </button>
          <button className="action-btn action-default" onClick={() => setEditing(false)} disabled={saving}>
            <X size={14} /> Cancel
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="editable-block">
      <div className="editable-toolbar">
        {block.edited && (
          <>
            <span className="edited-pill" title={`Edited ${block.edited.count}×`}>edited</span>
            <button className="editable-tool" onClick={() => setShowOriginal((v) => !v)} title="View original / diff">
              <GitCompare size={13} />
            </button>
            <button className="editable-tool" onClick={revert} title="Revert to original">
              <RotateCcw size={13} />
            </button>
          </>
        )}
        <button className="editable-tool" onClick={startEdit} title="Edit">
          <Pencil size={13} />
        </button>
      </div>

      {children}

      {showOriginal && block.edited && (
        <div className="editable-diff">
          <div className="editable-diff-head">Original → current</div>
          {diffLines(block.edited.originalText, currentText(block)).map((part, i) => (
            <pre
              key={i}
              className={`diff-line ${part.added ? 'diff-added' : part.removed ? 'diff-removed' : 'diff-ctx'}`}
            >
              {part.value.replace(/\n$/, '')}
            </pre>
          ))}
        </div>
      )}
      {error && <div className="editable-error">{error}</div>}
    </div>
  );
}
