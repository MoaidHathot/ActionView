import React from 'react';

/**
 * Highlights occurrences of `query` within `text` by wrapping matches
 * in <mark> elements. Returns a React fragment with mixed text/mark nodes.
 * If query is empty or not found, returns the original text.
 */
export function highlightText(text: string, query: string | undefined): React.ReactNode {
  if (!query || query.length === 0) return text;

  const escaped = query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const regex = new RegExp(`(${escaped})`, 'gi');
  const parts = text.split(regex);

  if (parts.length === 1) return text;

  return React.createElement(
    React.Fragment,
    null,
    ...parts.map((part, i) =>
      regex.test(part)
        ? React.createElement('mark', { key: i, className: 'search-highlight' }, part)
        : part
    )
  );
}
