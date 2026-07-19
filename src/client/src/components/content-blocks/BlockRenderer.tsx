import type { ContentBlock } from '../../types';
import type { DerivedMarkers } from '../../utils/markers';
import { MarkdownBlock } from './MarkdownBlock';
import { CodeBlock } from './CodeBlock';
import { JsonBlock } from './JsonBlock';
import { TableBlock } from './TableBlock';
import { KeyValueBlock } from './KeyValueBlock';
import { LinkBlock } from './LinkBlock';
import { SectionBlock } from './SectionBlock';
import { AlertBlock } from './AlertBlock';
import { ImageBlock } from './ImageBlock';
import { DiffBlock } from './DiffBlock';
import { VideoBlock } from './VideoBlock';
import { GalleryBlock } from './GalleryBlock';
import { TimelineBlock } from './TimelineBlock';
import { TabsBlock } from './TabsBlock';
import { StatBlock } from './StatBlock';
import { FileBlock } from './FileBlock';
import { ChartBlock } from './ChartBlock';
import { DiagramBlock } from './DiagramBlock';
import { BeforeAfterBlock } from './BeforeAfterBlock';
import { PluginBlockWrapper } from './PluginBlockWrapper';

interface BlockRendererProps {
  block: ContentBlock;
  entryId: string;
  /** Positional path to this block (indices into content/children at each level). */
  path?: number[];
  /** Stable key for blocks that persist UI state in localStorage (alert dismiss, etc). */
  blockKey?: string;
  /** Executes an action owned by a nested block, addressed by its full path. */
  onBlockAction?: (path: number[], actionIndex: number, parameters?: Record<string, string>) => void;
  /** Derived outcome markers so nested sections can show their last result. */
  markers?: DerivedMarkers;
}

export function BlockRenderer({ block, entryId, path = [], blockKey, onBlockAction, markers }: BlockRendererProps) {
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
          path={path}
          onBlockAction={onBlockAction}
          markers={markers}
        />
      );
    case 'divider':
      return <hr className="block-divider" />;
    case 'alert':
      return <AlertBlock block={block} entryId={entryId} blockKey={blockKey} />;
    case 'image':
      return <ImageBlock block={block} />;
    case 'diff':
      return <DiffBlock block={block} />;
    case 'video':
      return <VideoBlock block={block} />;
    case 'gallery':
      return <GalleryBlock block={block} />;
    case 'timeline':
      return <TimelineBlock block={block} />;
    case 'tabs':
      return <TabsBlock block={block} entryId={entryId} />;
    case 'stat':
      return <StatBlock block={block} />;
    case 'file':
      return <FileBlock block={block} />;
    case 'chart':
      return <ChartBlock block={block} />;
    case 'diagram':
      return <DiagramBlock block={block} />;
    case 'beforeAfter':
      return <BeforeAfterBlock block={block} />;
    default:
      // Unknown block type: delegate to the plugin system
      return <PluginBlockWrapper block={block} blockType={block.type} />;
  }
}
