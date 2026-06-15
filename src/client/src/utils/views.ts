import type { EntryFilters, SavedView } from '../types';

/** Stable id for the always-present synthetic "All" view (shows everything). */
export const ALL_VIEW_ID = '__all__';

/** Maps a saved view to the filter selection the API understands. */
export function viewToFilters(view: SavedView): EntryFilters {
  return {
    type: view.type || undefined,
    tags: view.tags && view.tags.length > 0 ? view.tags.join(',') : undefined,
  };
}

function normalizeTags(tags?: string[] | string): string[] {
  if (!tags) return [];
  const arr = Array.isArray(tags) ? tags : tags.split(',');
  return arr
    .map((t) => t.trim().toLowerCase())
    .filter(Boolean)
    .sort();
}

function sameTags(a?: string[] | string, b?: string[] | string): boolean {
  const x = normalizeTags(a);
  const y = normalizeTags(b);
  return x.length === y.length && x.every((v, i) => v === y[i]);
}

/**
 * Determines which view (if any) the current filters correspond to, comparing
 * only the view-defining dimensions (type + tags). Returns:
 *  - {@link ALL_VIEW_ID} when neither type nor tags constrain the feed,
 *  - the matching view id when type+tags equal a saved view,
 *  - null for an ad-hoc selection that matches no saved view.
 *
 * Other filters (severity/source/search) are treated as refinements and do not
 * affect which view is considered active.
 */
export function activeViewId(filters: EntryFilters, views: SavedView[]): string | null {
  const curType = filters.type || '';
  const curTags = normalizeTags(filters.tags);

  if (!curType && curTags.length === 0) return ALL_VIEW_ID;

  for (const view of views) {
    if ((view.type || '') === curType && sameTags(view.tags, filters.tags)) {
      return view.id;
    }
  }
  return null;
}
