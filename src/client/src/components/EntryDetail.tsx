import { useState, useCallback } from 'react';
import { Trash2, X, Edit3, Pin, PinOff, Clock, CheckCircle, XCircle, ChevronDown, ChevronRight, ExternalLink } from 'lucide-react';
import type { Entry, ActionExecutionResult } from '../types';
import type { UndoItem } from './UndoToast';
import { BlockRenderer } from './content-blocks/BlockRenderer';
import { ActionButton } from './ActionButton';
import { EntryEditor } from './EntryEditor';
import { api } from '../api/client';
import { createUndoItem } from './UndoToast';
import { formatDistanceToNow } from '../utils/time';

interface Props {
  entry: Entry;
  onDismiss: (id: string) => void;
  onDelete: (id: string) => void;
  onActionExecuted: () => void;
  onEntryUpdated: (entry: Entry) => void;
  onUndoCreated?: (item: UndoItem) => void;
  defaultUndoWindow: number;
  /** Template-based display config for this entry type */
  templateDescription?: string;
}

export function EntryDetail({
  entry, onDismiss, onDelete, onActionExecuted, onEntryUpdated, onUndoCreated, defaultUndoWindow,
  templateDescription,
}: Props) {
  const [actionResult, setActionResult] = useState<ActionExecutionResult | null>(null);
  const [editing, setEditing] = useState(false);
  const [showResultDetail, setShowResultDetail] = useState(false);
  const [showMetadata, setShowMetadata] = useState(false);

  const handleAction = useCallback(async (actionIndex: number) => {
    try {
      const action = entry.actions[actionIndex];
      const result = await api.executeAction(entry.id, actionIndex);
      setActionResult(result);
      setShowResultDetail(false);
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
      setActionResult(result);
      setShowResultDetail(false);
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

  const hasMetadata = entry.metadata && Object.keys(entry.metadata).length > 0;

  return (
    <div className="entry-detail">
      <div className="entry-detail-header">
        {entry.pinned && (
          <div className="entry-pinned-banner">
            <Pin size={12} />
            <span>Pinned to top</span>
          </div>
        )}
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
          <div className="entry-detail-meta-info">
            <span className="entry-type-badge">{entry.type}</span>
            <span className="entry-source-badge">{entry.source}</span>
            <span className="entry-time-badge">
              <Clock size={11} />
              {formatDistanceToNow(entry.createdAt)}
            </span>
          </div>
          {entry.priority > 0 && (
            <span className="entry-priority-badge">Priority {entry.priority}</span>
          )}
        </div>
        <div className="entry-detail-meta-row">
          <div className="entry-detail-tags">
            {entry.tags.map((tag) => (
              <span key={tag} className="tag">{tag}</span>
            ))}
          </div>
        </div>
        {templateDescription && (
          <div className="entry-template-desc">{templateDescription}</div>
        )}
      </div>

      {/* Provenance / Metadata (Feature 12) */}
      {hasMetadata && (
        <div className="entry-provenance">
          <div
            className="entry-provenance-header clickable"
            onClick={() => setShowMetadata(!showMetadata)}
          >
            {showMetadata ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
            <span>Provenance</span>
            <span className="provenance-count">{Object.keys(entry.metadata!).length} fields</span>
          </div>
          {showMetadata && (
            <div className="entry-provenance-body">
              {Object.entries(entry.metadata!).map(([key, value]) => (
                <div key={key} className="provenance-item">
                  <span className="provenance-key">{key}</span>
                  <span className="provenance-value">
                    {value.startsWith('http://') || value.startsWith('https://') ? (
                      <a href={value} target="_blank" rel="noopener noreferrer">
                        {value} <ExternalLink size={10} />
                      </a>
                    ) : value}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

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

      {/* Action Result Banner (Feature 3 - enhanced) */}
      {actionResult && (
        <div className={`action-result ${actionResult.success ? 'success' : 'error'}`}>
          <div className="action-result-main">
            <div className="action-result-icon">
              {actionResult.success ? <CheckCircle size={16} /> : <XCircle size={16} />}
            </div>
            <div className="action-result-body">
              <div className="action-result-message">
                {actionResult.message}
                {actionResult.statusCode !== undefined && actionResult.statusCode !== null && (
                  <span className="action-result-status">HTTP {actionResult.statusCode}</span>
                )}
                {actionResult.durationMs !== undefined && actionResult.durationMs !== null && (
                  <span className="action-result-duration">{actionResult.durationMs}ms</span>
                )}
              </div>
              {actionResult.output && (
                <button
                  className="action-result-toggle"
                  onClick={() => setShowResultDetail(!showResultDetail)}
                >
                  {showResultDetail ? 'Hide output' : 'Show output'}
                </button>
              )}
            </div>
            <button className="close-result" onClick={() => setActionResult(null)}>
              <X size={14} />
            </button>
          </div>
          {showResultDetail && actionResult.output && (
            <pre className="action-result-output">{actionResult.output}</pre>
          )}
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
