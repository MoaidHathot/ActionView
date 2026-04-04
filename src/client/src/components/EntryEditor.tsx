import { useState, useCallback } from 'react';
import { Save, X } from 'lucide-react';
import type { Entry, Severity, EntryUpdateRequest } from '../types';
import { api } from '../api/client';

interface Props {
  entry: Entry;
  onSave: (updated: Entry) => void;
  onCancel: () => void;
}

export function EntryEditor({ entry, onSave, onCancel }: Props) {
  const [title, setTitle] = useState(entry.title);
  const [subtitle, setSubtitle] = useState(entry.subtitle ?? '');
  const [severity, setSeverity] = useState<Severity>(entry.severity);
  const [tagsText, setTagsText] = useState(entry.tags.join(', '));
  const [priority, setPriority] = useState(entry.priority);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSave = useCallback(async () => {
    setSaving(true);
    setError(null);
    try {
      const update: EntryUpdateRequest = {};
      if (title !== entry.title) update.title = title;
      if (subtitle !== (entry.subtitle ?? '')) update.subtitle = subtitle || undefined;
      if (severity !== entry.severity) update.severity = severity;
      if (priority !== entry.priority) update.priority = priority;

      const newTags = tagsText
        .split(',')
        .map((t) => t.trim())
        .filter(Boolean);
      const tagsChanged =
        newTags.length !== entry.tags.length ||
        newTags.some((t, i) => t !== entry.tags[i]);
      if (tagsChanged) update.tags = newTags;

      // Only send if something changed
      if (Object.keys(update).length === 0) {
        onCancel();
        return;
      }

      const updated = await api.updateEntry(entry.id, update);
      onSave(updated);
    } catch (err) {
      setError(String(err));
    } finally {
      setSaving(false);
    }
  }, [entry, title, subtitle, severity, tagsText, priority, onSave, onCancel]);

  return (
    <div className="entry-editor">
      <div className="editor-header">
        <h3>Edit Entry</h3>
        <button className="action-btn action-default" onClick={onCancel}>
          <X size={14} /> Cancel
        </button>
      </div>

      <div className="editor-fields">
        <div className="editor-field">
          <label className="editor-label">Title</label>
          <input
            className="editor-input"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
          />
        </div>

        <div className="editor-field">
          <label className="editor-label">Subtitle</label>
          <input
            className="editor-input"
            value={subtitle}
            onChange={(e) => setSubtitle(e.target.value)}
            placeholder="Optional subtitle"
          />
        </div>

        <div className="editor-row">
          <div className="editor-field">
            <label className="editor-label">Severity</label>
            <select
              className="editor-select"
              value={severity}
              onChange={(e) => setSeverity(e.target.value as Severity)}
            >
              <option value="low">Low</option>
              <option value="medium">Medium</option>
              <option value="high">High</option>
              <option value="critical">Critical</option>
            </select>
          </div>

          <div className="editor-field">
            <label className="editor-label">Priority</label>
            <input
              className="editor-input editor-input-narrow"
              type="number"
              value={priority}
              onChange={(e) => setPriority(parseInt(e.target.value) || 0)}
            />
          </div>
        </div>

        <div className="editor-field">
          <label className="editor-label">Tags (comma-separated)</label>
          <input
            className="editor-input"
            value={tagsText}
            onChange={(e) => setTagsText(e.target.value)}
            placeholder="tag1, tag2, tag3"
          />
        </div>
      </div>

      {error && (
        <div className="editor-error">{error}</div>
      )}

      <div className="editor-actions">
        <button
          className="action-btn action-primary"
          onClick={handleSave}
          disabled={saving || !title.trim()}
        >
          <Save size={14} /> {saving ? 'Saving...' : 'Save Changes'}
        </button>
      </div>
    </div>
  );
}
