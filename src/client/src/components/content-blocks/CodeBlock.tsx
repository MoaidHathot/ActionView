import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { oneDark } from 'react-syntax-highlighter/dist/esm/styles/prism';
import type { ContentBlock } from '../../types';

interface Props {
  block: ContentBlock;
}

export function CodeBlock({ block }: Props) {
  const code = typeof block.body === 'string' ? block.body : String(block.body ?? '');
  const language = block.language ?? 'text';

  return (
    <div className="block-code">
      {(block.label || block.filename) && (
        <div className="code-header">
          {block.filename && <span className="code-filename">{block.filename}</span>}
          {block.label && !block.filename && <span className="code-label">{block.label}</span>}
        </div>
      )}
      <SyntaxHighlighter
        language={language}
        style={oneDark}
        showLineNumbers
        wrapLines
        lineProps={(lineNumber: number) => {
          const style: React.CSSProperties = {};
          if (block.highlight?.includes(lineNumber)) {
            style.backgroundColor = 'rgba(255, 255, 0, 0.15)';
            style.borderLeft = '3px solid #ffd700';
            style.paddingLeft = '0.5em';
          }
          return { style };
        }}
      >
        {code}
      </SyntaxHighlighter>
    </div>
  );
}
