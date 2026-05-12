import { useState } from 'react';
import type { ContentBlock } from '../../types';
import { BlockRenderer } from './BlockRenderer';
import { ActionButton } from '../ActionButton';

interface Props {
  block: ContentBlock;
  entryId: string;
  sectionIndex: number;
  onAction?: (sectionIndex: number, actionIndex: number, parameters?: Record<string, string>) => void;
}

export function SectionBlock({ block, entryId, sectionIndex, onAction }: Props) {
  const [expanded, setExpanded] = useState(true);

  return (
    <div className="block-section">
      <h4
        className="section-title clickable"
        onClick={() => setExpanded(!expanded)}
      >
        {expanded ? '\u25BC' : '\u25B6'} {block.title ?? block.label ?? 'Section'}
      </h4>
      {expanded && (
        <div className="section-content">
          {block.content?.map((child, i) => (
            <BlockRenderer key={i} block={child} entryId={entryId} />
          ))}
          {block.actions && block.actions.length > 0 && (
            <div className="section-actions">
              {block.actions.map((action, actionIdx) => (
                <ActionButton
                  key={actionIdx}
                  action={action}
                  draftKey={`${entryId}.s${sectionIndex}.${actionIdx}`}
                  onClick={(parameters) => onAction?.(sectionIndex, actionIdx, parameters)}
                />
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
