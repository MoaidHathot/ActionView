import { useState, useMemo } from 'react';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { oneDark } from 'react-syntax-highlighter/dist/esm/styles/prism';
import { WrapText, ListOrdered, Info, AlertTriangle, AlertCircle, CheckCircle } from 'lucide-react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import type { ContentBlock, CodeAnnotation, AlertLevel } from '../../types';
import { CopyButton } from '../CopyButton';
import { allowEntryImageUrl } from '../../utils/imageUrl';

interface Props {
  block: ContentBlock;
}

/**
 * Renders source code with:
 *   - Prism syntax highlighting (language from block.language)
 *   - Optional filename header
 *   - Copy-to-clipboard button
 *   - Toggle for line numbers (block.showLineNumbers default true)
 *   - Toggle for soft wrap (block.wordWrap default true)
 *   - Per-line review-style annotations (block.annotations[])
 *
 * Annotations are rendered as inline cards beneath the line they target,
 * matching the pattern PR reviewers expect. Each annotation can carry a
 * level (info/warning/error/success), an author label, and markdown body.
 */
export function CodeBlock({ block }: Props) {
  const code = typeof block.body === 'string' ? block.body : String(block.body ?? '');
  const language = block.language ?? 'text';

  const [showLineNumbers, setShowLineNumbers] = useState(block.showLineNumbers ?? true);
  const [wrap, setWrap] = useState(block.wordWrap ?? true);

  // Group annotations by line so we can splice them into the right place.
  const annotationsByLine = useMemo(() => {
    const map = new Map<number, CodeAnnotation[]>();
    for (const a of block.annotations ?? []) {
      const list = map.get(a.line) ?? [];
      list.push(a);
      map.set(a.line, list);
    }
    return map;
  }, [block.annotations]);

  const lineProps = (lineNumber: number) => {
    const style: React.CSSProperties = { display: 'block' };
    if (block.highlight?.includes(lineNumber)) {
      style.backgroundColor = 'rgba(251, 191, 36, 0.12)';
      style.borderLeft = '3px solid var(--warning)';
      style.paddingLeft = 8;
    }
    return { style };
  };

  return (
    <div className="block-code">
      <div className="code-header">
        <div className="code-header-left">
          {block.filename && <span className="code-filename">{block.filename}</span>}
          {!block.filename && block.label && <span className="code-label">{block.label}</span>}
          {block.language && (
            <span className="code-language">{block.language}</span>
          )}
        </div>
        <div className="code-header-right">
          <button
            type="button"
            className={`code-toggle ${showLineNumbers ? 'code-toggle-on' : ''}`}
            onClick={() => setShowLineNumbers(v => !v)}
            title={showLineNumbers ? 'Hide line numbers' : 'Show line numbers'}
            aria-label="Toggle line numbers"
            aria-pressed={showLineNumbers}
          >
            <ListOrdered size={13} />
          </button>
          <button
            type="button"
            className={`code-toggle ${wrap ? 'code-toggle-on' : ''}`}
            onClick={() => setWrap(v => !v)}
            title={wrap ? 'No line wrap' : 'Wrap lines'}
            aria-label="Toggle line wrap"
            aria-pressed={wrap}
          >
            <WrapText size={13} />
          </button>
          <CopyButton value={code} iconSize={13} />
        </div>
      </div>

      <div className="code-body-wrap">
        <SyntaxHighlighter
          language={language}
          style={oneDark}
          showLineNumbers={showLineNumbers}
          wrapLines={true}
          wrapLongLines={wrap}
          customStyle={{ margin: 0, fontSize: 13 }}
          lineProps={lineProps}
        >
          {code}
        </SyntaxHighlighter>

        {annotationsByLine.size > 0 && (
          <CodeAnnotationsOverlay
            annotations={annotationsByLine}
            code={code}
            showLineNumbers={showLineNumbers}
          />
        )}
      </div>
    </div>
  );
}

/**
 * Inline annotations rendered as a stacked list under the code block.
 * Each annotation references "Line N" so the reader can scroll up to find it.
 * (A truly inline overlay is fragile across line-wrap + highlight changes,
 * so we render annotations as a side panel + line-number cross-references.)
 */
function CodeAnnotationsOverlay({
  annotations, code: _code, showLineNumbers: _showLineNumbers,
}: {
  annotations: Map<number, CodeAnnotation[]>;
  code: string;
  showLineNumbers: boolean;
}) {
  const sorted = [...annotations.entries()].sort(([a], [b]) => a - b);
  return (
    <div className="code-annotations">
      {sorted.map(([line, list]) =>
        list.map((a, i) => (
          <div key={`${line}-${i}`} className={`code-annotation code-annotation-${a.level ?? 'info'}`}>
            <div className="code-annotation-header">
              <AnnotationIcon level={a.level ?? 'info'} />
              <span className="code-annotation-line">Line {line}</span>
              {a.author && <span className="code-annotation-author">{a.author}</span>}
            </div>
            <div className="code-annotation-body">
              <ReactMarkdown remarkPlugins={[remarkGfm]} urlTransform={allowEntryImageUrl}>{a.body}</ReactMarkdown>
            </div>
          </div>
        )),
      )}
    </div>
  );
}

function AnnotationIcon({ level }: { level: AlertLevel }) {
  const Icon = level === 'warning' ? AlertTriangle
    : level === 'error' ? AlertCircle
    : level === 'success' ? CheckCircle
    : Info;
  return <Icon size={14} />;
}
