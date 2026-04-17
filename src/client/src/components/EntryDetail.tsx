import { useState, useCallback, useEffect } from 'react';
import { Trash2, X, Edit3, Pin, PinOff } from 'lucide-react';
import type { Entry } from '../types';
import type { UndoItem } from './UndoToast';
import { BlockRenderer } from './content-blocks/BlockRenderer';
import { ActionButton } from './ActionButton';
import { EntryEditor } from './EntryEditor';
import { api } from '../api/client';
import { createUndoItem } from './UndoToast';

interface Props {
  entry: Entry;
  onDismiss: (id: string) => void;
  onDelete: (id: string) => void;
  onActionExecuted: () => void;
  onEntryUpdated: (entry: Entry) => void;
  onUndoCreated?: (item: UndoItem) => void;
  defaultUndoWindow: number;
}

export function EntryDetail({
  entry, onDismiss, onDelete, onActionExecuted, onEntryUpdated, onUndoCreated, defaultUndoWindow,
}: Props) {
  const [actionResult, setActionResult] = useState<{ success: boolean; message: string } | null>(null);
  const [editing, setEditing] = useState(false);

  useEffect(() => {
    setActionResult(null);
    setEditing(false);
  }, [entry.id]);

  const handleAction = useCallback(async (actionIndex: number) => {
    try {
      const action = entry.actions[actionIndex];
      const result = await api.executeAction(entry.id, actionIndex);
      setActionResult({ success: result.success, message: result.message ?? '' });
      if (result.success) {
        // If the action has an undo command, show undo toast
        if (action.undoCommand && onUndoCreated) {
          const windowSec = action.undoWindowSeconds ?? defaultUndoWindow;
          onUndoCreated(
            createUndoItem(entry.id, entry.title, action.label, windowSec),
          );
        }
        onActionExecuted();
      }
    } catch (err) {
      setActionResult({ success: false, message: String(err) });
    }
  }, [entry, onActionExecuted, onUndoCreated, defaultUndoWindow]);

  const handleSectionAction = useCallback(async (sectionIndex: number, actionIndex: number) => {
    try {
      const result = await api.executeSectionAction(entry.id, sectionIndex, actionIndex);
      setActionResult({ success: result.success, message: result.message ?? '' });
    } catch (err) {
      setActionResult({ success: false, message: String(err) });
    }
  }, [entry.id]);

  const handleDismiss = useCallback(async () => {
    try {
      await api.dismissEntry(entry.id);
      onDismiss(entry.id);
    } catch (err) {
      setActionResult({ success: false, message: `Dismiss failed: ${err}` });
    }
  }, [entry.id, onDismiss]);

  const handleDelete = useCallback(async () => {
    if (!window.confirm('Permanently delete this entry?')) return;
    try {
      await api.deleteEntry(entry.id);
      onDelete(entry.id);
    } catch (err) {
      setActionResult({ success: false, message: `Delete failed: ${err}` });
    }
  }, [entry.id, onDelete]);

  const handlePin = useCallback(async () => {
    try {
      const updated = await api.pinEntry(entry.id);
      onEntryUpdated(updated);
    } catch (err) {
      setActionResult({ success: false, message: `Pin toggle failed: ${err}` });
    }
  }, [entry.id, onEntryUpdated]);

  const handleEditorSave = useCallback((updated: Entry) => {
    onEntryUpdated(updated);
    setEditing(false);
  }, [onEntryUpdated]);

  // Track section indices for content blocks of type "section"
  let sectionCounter = 0;

  if (editing) {
    return (
      <div className="entry-detail">
        <EntryEditor
          entry={entry}
          onSave={handleEditorSave}
          onCancel={() => setEditing(false)}
        />
      </div>
    );
  }

  return (
    <div className="entry-detail">
      <div className="entry-detail-header">
        <div className="entry-detail-title-row">
          <h2>{entry.title}</h2>
          <div className="entry-detail-title-actions">
            <button
              className={`icon-btn ${entry.pinned ? 'active' : ''}`}
              onClick={handlePin}
              title={entry.pinned ? 'Unpin' : 'Pin to top'}
            >
              {entry.pinned ? <PinOff size={16} /> : <Pin size={16} />}
            </button>
            <button className="icon-btn" onClick={() => setEditing(true)} title="Edit entry">
              <Edit3 size={16} />
            </button>
          </div>
        </div>
        {entry.subtitle && <p className="entry-detail-subtitle">{entry.subtitle}</p>}
        <div className="entry-detail-meta-row">
          {entry.type && (
            <span className="entry-type-badge">{entry.type}</span>
          )}
          <div className="entry-detail-tags">
            {entry.tags.map((tag) => (
              <span key={tag} className="tag">{tag}</span>
            ))}
          </div>
          {entry.priority > 0 && (
            <span className="entry-priority-badge">Priority {entry.priority}</span>
          )}
        </div>
      </div>

      <div className="entry-detail-content">
        {entry.content.map((block, i) => {
          const currentSectionIndex = block.type === 'section' ? sectionCounter++ : undefined;
          return (
            <BlockRenderer
              key={i}
              block={block}
              entryId={entry.id}
              sectionIndex={currentSectionIndex}
              onSectionAction={handleSectionAction}
            />
          );
        })}
      </div>

      {actionResult && (
        <div className={`action-result ${actionResult.success ? 'success' : 'error'}`}>
          {actionResult.message}
          <button className="close-result" onClick={() => setActionResult(null)}>
            <X size={14} />
          </button>
        </div>
      )}

      <div className="entry-detail-actions">
        <div className="entry-actions-custom">
          {entry.actions.map((action, i) => (
            <ActionButton
              key={i}
              action={action}
              onClick={() => handleAction(i)}
            />
          ))}
        </div>
        <div className="entry-actions-system">
          <button className="action-btn action-default" onClick={handleDismiss}>
            <X size={14} /> Dismiss
          </button>
          <button className="action-btn action-danger-outline" onClick={handleDelete}>
            <Trash2 size={14} /> Delete
          </button>
        </div>
      </div>
    </div>
  );
}
