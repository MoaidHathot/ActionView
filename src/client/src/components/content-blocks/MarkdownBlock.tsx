import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import type { ContentBlock } from '../../types';

interface Props {
  block: ContentBlock;
}

export function MarkdownBlock({ block }: Props) {
  const content = typeof block.body === 'string' ? block.body : String(block.body ?? '');

  return (
    <div className="block-markdown">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      <ReactMarkdown remarkPlugins={[remarkGfm]}>{content}</ReactMarkdown>
    </div>
  );
}
