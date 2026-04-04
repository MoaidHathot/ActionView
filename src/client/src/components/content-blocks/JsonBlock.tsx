import { useState } from 'react';
import type { ContentBlock } from '../../types';

interface Props {
  block: ContentBlock;
}

export function JsonBlock({ block }: Props) {
  const [expanded, setExpanded] = useState(true);

  const data = block.body;
  const jsonString = typeof data === 'string' ? data : JSON.stringify(data, null, 2);

  return (
    <div className="block-json">
      {block.label && (
        <h4 className="block-label clickable" onClick={() => setExpanded(!expanded)}>
          {expanded ? '\u25BC' : '\u25B6'} {block.label}
        </h4>
      )}
      {expanded && (
        <pre className="json-content">
          <code>{jsonString}</code>
        </pre>
      )}
    </div>
  );
}
