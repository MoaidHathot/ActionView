import { useState } from 'react';
import { X, ArrowUp, ArrowDown, Trash2, Plus } from 'lucide-react';
import type { SavedView, TagMatchMode } from '../types';
import { renderIcon, VIEW_ICON_NAMES } from '../utils/icons';

interface Props {
  views: SavedView[];
  onClose: () => void;
  onSave: (views: SavedView[]) => Promise<unknown> | void;
}

/** Editable row model (tags held as raw text for comfortable editing). */
interface ViewDraft {
  id: string;
  name: string;
  icon: string;
  type: string;
  tagsText: string;
  tagMatch: '' | TagMatchMode;
}

function toDraft(view: SavedView): ViewDraft {
  return {
    id: view.id,
    name: view.name,
    icon: view.icon ?? '',
    type: view.type ?? '',
    tagsText: (view.tags ?? []).join(', '),
    tagMatch: view.tagMatch ?? '',
  };
}

function fromDraft(draft: ViewDraft): SavedView {
  return {
    id: draft.id,
    name: draft.name.trim(),
    icon: draft.icon || undefined,
    type: draft.type.trim() || undefined,
    tags: draft.tagsText.split(',').map((t) => t.trim()).filter(Boolean),
    tagMatch: draft.tagMatch || undefined,
  };
}

const emptyDraft = (): ViewDraft => ({ id: '', name: '', icon: '', type: '', tagsText: '', tagMatch: '' });

/**
 * Modal for managing saved views: rename, change icon/type/tags/match, reorder
 * via up/down, delete, and add. Saving replaces the whole list (PUT /api/views,
 * server-normalized).
 */
export function ManageViewsModal({ views, onClose, onSave }: Props) {
  const [drafts, setDrafts] = useState<ViewDraft[]>(() => views.map(toDraft));
  const [saving, setSaving] = useState(false);

  const update = (index: number, partial: Partial<ViewDraft>) => {
    setDrafts((prev) => prev.map((d, i) => (i === index ? { ...d, ...partial } : d)));
  };

  const move = (index: number, delta: number) => {
    setDrafts((prev) => {
      const next = [...prev];
      const target = index + delta;
      if (target < 0 || target >= next.length) return prev;
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  };

  const remove = (index: number) => {
    setDrafts((prev) => prev.filter((_, i) => i !== index));
  };

  const addRow = () => setDrafts((prev) => [...prev, emptyDraft()]);

  const handleSave = async () => {
    setSaving(true);
    try {
      const payload = drafts.filter((d) => d.name.trim()).map(fromDraft);
      await onSave(payload);
      onClose();
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="manage-views-backdrop" onClick={onClose}>
      <div className="manage-views-modal" onClick={(e) => e.stopPropagation()}>
        <div className="manage-views-header">
          <h2>Manage views</h2>
          <button className="icon-btn" onClick={onClose} aria-label="Close">
            <X size={16} />
          </button>
        </div>

        <div className="manage-views-body">
          {drafts.length === 0 && (
            <p className="manage-views-empty">No views yet. Add one below.</p>
          )}
          {drafts.map((draft, index) => (
            <div className="manage-view-row" key={draft.id || `new-${index}`}>
              <div className="manage-view-reorder">
                <button
                  className="icon-btn small"
                  onClick={() => move(index, -1)}
                  disabled={index === 0}
                  title="Move up"
                  aria-label="Move up"
                >
                  <ArrowUp size={13} />
                </button>
                <button
                  className="icon-btn small"
                  onClick={() => move(index, 1)}
                  disabled={index === drafts.length - 1}
                  title="Move down"
                  aria-label="Move down"
                >
                  <ArrowDown size={13} />
                </button>
              </div>

              <span className="manage-view-icon-preview">{renderIcon(draft.icon, 16)}</span>

              <input
                className="manage-view-input name"
                placeholder="Name"
                value={draft.name}
                onChange={(e) => update(index, { name: e.target.value })}
              />
              <select
                className="manage-view-input icon"
                value={draft.icon}
                onChange={(e) => update(index, { icon: e.target.value })}
                title="Icon"
                aria-label="Icon"
              >
                <option value="">no icon</option>
                {VIEW_ICON_NAMES.map((n) => (
                  <option key={n} value={n}>{n}</option>
                ))}
              </select>
              <input
                className="manage-view-input type"
                placeholder="type (optional)"
                value={draft.type}
                onChange={(e) => update(index, { type: e.target.value })}
              />
              <input
                className="manage-view-input tags"
                placeholder="tags (comma-separated)"
                value={draft.tagsText}
                onChange={(e) => update(index, { tagsText: e.target.value })}
              />
              <select
                className="manage-view-input match"
                value={draft.tagMatch}
                onChange={(e) => update(index, { tagMatch: e.target.value as ViewDraft['tagMatch'] })}
                title="Tag match mode"
                aria-label="Tag match mode"
              >
                <option value="">match: default</option>
                <option value="any">match: any</option>
                <option value="all">match: all</option>
              </select>

              <button
                className="icon-btn small danger"
                onClick={() => remove(index)}
                title="Delete view"
                aria-label="Delete view"
              >
                <Trash2 size={14} />
              </button>
            </div>
          ))}

          <button className="manage-views-add" onClick={addRow}>
            <Plus size={14} /> Add view
          </button>
        </div>

        <div className="manage-views-footer">
          <button className="manage-views-cancel" onClick={onClose} disabled={saving}>
            Cancel
          </button>
          <button className="manage-views-save" onClick={handleSave} disabled={saving}>
            {saving ? 'Saving\u2026' : 'Save'}
          </button>
        </div>
      </div>
    </div>
  );
}
