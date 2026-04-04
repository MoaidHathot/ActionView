import { ExternalLink } from 'lucide-react';
import type { ContentBlock } from '../../types';

interface Props {
  block: ContentBlock;
}

export function LinkBlock({ block }: Props) {
  const url = block.url ?? (typeof block.body === 'string' ? block.body : '');
  const label = block.label ?? url;

  return (
    <div className="block-link">
      <a href={url} target="_blank" rel="noopener noreferrer">
        <ExternalLink size={14} />
        {label}
      </a>
    </div>
  );
}
