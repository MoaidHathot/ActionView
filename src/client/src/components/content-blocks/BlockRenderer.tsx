import type { ContentBlock } from '../../types';
import { MarkdownBlock } from './MarkdownBlock';
import { CodeBlock } from './CodeBlock';
import { JsonBlock } from './JsonBlock';
import { TableBlock } from './TableBlock';
import { KeyValueBlock } from './KeyValueBlock';
import { LinkBlock } from './LinkBlock';
import { SectionBlock } from './SectionBlock';
import { AlertBlock } from './AlertBlock';
import { PluginBlockWrapper } from './PluginBlockWrapper';

interface BlockRendererProps {
  block: ContentBlock;
  entryId: string;
  sectionIndex?: number;
  onSectionAction?: (sectionIndex: number, actionIndex: number) => void;
}

export function BlockRenderer({ block, entryId, sectionIndex, onSectionAction }: BlockRendererProps) {
  switch (block.type) {
    case 'markdown':
      return <MarkdownBlock block={block} />;
    case 'code':
      return <CodeBlock block={block} />;
    case 'json':
      return <JsonBlock block={block} />;
    case 'table':
      return <TableBlock block={block} />;
    case 'keyValue':
      return <KeyValueBlock block={block} />;
    case 'link':
      return <LinkBlock block={block} />;
    case 'section':
      return (
        <SectionBlock
          block={block}
          entryId={entryId}
          sectionIndex={sectionIndex ?? 0}
          onAction={onSectionAction}
        />
      );
    case 'divider':
      return <hr className="block-divider" />;
    case 'alert':
      return <AlertBlock block={block} />;
    default:
      // Unknown block type: delegate to the plugin system
      return <PluginBlockWrapper block={block} blockType={block.type} />;
  }
}
