import type { ContentBlock } from '../../types';

interface Props {
  block: ContentBlock;
}

interface DiffLine {
  type: 'add' | 'remove' | 'context' | 'header';
  content: string;
}

function parseDiff(raw: string): DiffLine[] {
  return raw.split('\n').map((line) => {
    if (line.startsWith('+++') || line.startsWith('---') || line.startsWith('@@')) {
      return { type: 'header', content: line };
    }
    if (line.startsWith('+')) {
      return { type: 'add', content: line };
    }
    if (line.startsWith('-')) {
      return { type: 'remove', content: line };
    }
    return { type: 'context', content: line };
  });
}

export function DiffBlock({ block }: Props) {
  const raw = typeof block.body === 'string' ? block.body : String(block.body ?? '');
  const lines = parseDiff(raw);

  return (
    <div className="block-diff">
      {(block.label || block.filename) && (
        <div className="diff-header">
          {block.filename && <span className="code-filename">{block.filename}</span>}
          {block.label && !block.filename && <span className="code-label">{block.label}</span>}
        </div>
      )}
      <pre className="diff-content">
        {lines.map((line, i) => (
          <div key={i} className={`diff-line diff-${line.type}`}>
            <span className="diff-marker">
              {line.type === 'add' ? '+' : line.type === 'remove' ? '-' : line.type === 'header' ? '' : ' '}
            </span>
            <span className="diff-text">{line.type === 'header' ? line.content : line.content.slice(1) || ''}</span>
          </div>
        ))}
      </pre>
    </div>
  );
}
