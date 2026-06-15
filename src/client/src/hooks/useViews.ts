import { useCallback, useEffect, useMemo, useState } from 'react';
import type { Dispatch, SetStateAction } from 'react';
import type { EntryFilters, SavedView, TagMatchMode } from '../types';
import { api } from '../api/client';
import { activeViewId, viewToFilters } from '../utils/views';

export interface NewView {
  name: string;
  icon?: string;
  type?: string;
  tags?: string[];
  tagMatch?: TagMatchMode;
}

/**
 * Owns the saved-views collection and its persistence. Intended to be created
 * once near the app root and shared, so Active and History stay in sync.
 */
export function useViews() {
  const [views, setViews] = useState<SavedView[]>([]);

  useEffect(() => {
    api.getViews()
      .then(setViews)
      .catch((err) => console.error('Failed to load views:', err));
  }, []);

  const createView = useCallback(
    async (partial: NewView): Promise<SavedView | undefined> => {
      const draft: SavedView = {
        id: '',
        name: partial.name,
        icon: partial.icon,
        type: partial.type,
        tags: partial.tags ?? [],
        tagMatch: partial.tagMatch,
      };
      try {
        const saved = await api.saveViews([...views, draft]);
        setViews(saved);
        return (
          saved.find((v) => v.name === partial.name && (v.type || '') === (partial.type || '')) ??
          saved[saved.length - 1]
        );
      } catch (err) {
        console.error('Failed to save view:', err);
        return undefined;
      }
    },
    [views],
  );

  const deleteView = useCallback(
    async (id: string) => {
      try {
        const saved = await api.saveViews(views.filter((v) => v.id !== id));
        setViews(saved);
      } catch (err) {
        console.error('Failed to delete view:', err);
      }
    },
    [views],
  );

  return { views, createView, deleteView };
}

/**
 * Binds a views collection to a specific filter state, producing the handler
 * set a {@link ViewBar} needs. Reused by Active and History so the wiring lives
 * in one place.
 */
export function useViewBinding(
  filters: EntryFilters,
  setFilters: Dispatch<SetStateAction<EntryFilters>>,
  views: SavedView[],
  createView: (partial: NewView) => Promise<SavedView | undefined>,
  deleteView: (id: string) => Promise<void>,
) {
  const currentViewId = useMemo(() => activeViewId(filters, views), [filters, views]);

  const onApplyAll = useCallback(() => setFilters({}), [setFilters]);

  const onApplyView = useCallback(
    (view: SavedView) => setFilters(viewToFilters(view)),
    [setFilters],
  );

  const onCreate = useCallback(
    async (partial: NewView) => {
      const created = await createView(partial);
      if (created) setFilters(viewToFilters(created));
    },
    [createView, setFilters],
  );

  const onDelete = useCallback(
    async (id: string) => {
      const wasActive = activeViewId(filters, views) === id;
      await deleteView(id);
      if (wasActive) setFilters({});
    },
    [deleteView, filters, views, setFilters],
  );

  const onTagClick = useCallback(
    (tag: string) => setFilters((prev) => ({ ...prev, tags: tag })),
    [setFilters],
  );

  return { currentViewId, onApplyAll, onApplyView, onCreate, onDelete, onTagClick };
}
