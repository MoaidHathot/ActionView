import { useState, useCallback } from 'react';
import ReactMarkdown from 'react-markdown';
import type { Components } from 'react-markdown';
import remarkGfm from 'remark-gfm';
import remarkMath from 'remark-math';
import rehypeKatex from 'rehype-katex';
import type { ContentBlock } from '../../types';
import { ImageLightbox } from '../ImageLightbox';
import { rewriteImageUrl } from '../../utils/imageUrl';
import 'katex/dist/katex.min.css';

interface Props {
  block: ContentBlock;
}

interface LightboxState {
  src: string;
  alt: string;
}

/**
 * Markdown content with the full GitHub-flavoured pipeline plus math
 * (KaTeX). Embedded images render as click-to-enlarge thumbnails using
 * the shared ImageLightbox; file:// URLs are rewritten to /api/files so
 * they actually load in the browser.
 *
 * Extensions enabled:
 *   - GFM (tables, task lists `- [ ]`, strikethrough, autolinks)
 *   - Math (`$inline$` and `$$block$$` rendered via KaTeX)
 */
export function MarkdownBlock({ block }: Props) {
  const content = typeof block.body === 'string' ? block.body : String(block.body ?? '');
  const [lightbox, setLightbox] = useState<LightboxState | null>(null);

  const openLightbox = useCallback((src: string, alt: string) => {
    setLightbox({ src, alt });
  }, []);

  const components: Components = {
    img: ({ src, alt, title }) => {
      const rawSrc = typeof src === 'string' ? src : '';
      const resolved = rewriteImageUrl(rawSrc);
      const altText = alt ?? '';
      if (!resolved) {
        return <span className="markdown-image-missing">[image: missing src]</span>;
      }
      return (
        <button
          type="button"
          className="markdown-image-thumb"
          onClick={() => openLightbox(resolved, altText)}
          title={title ?? 'Click to enlarge'}
          aria-label={altText ? `Enlarge image: ${altText}` : 'Enlarge image'}
        >
          <img src={resolved} alt={altText} loading="lazy" />
        </button>
      );
    },
  };

  return (
    <div className="block-markdown">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      <ReactMarkdown
        remarkPlugins={[remarkGfm, remarkMath]}
        rehypePlugins={[rehypeKatex]}
        components={components}
      >
        {content}
      </ReactMarkdown>
      {lightbox && (
        <ImageLightbox
          src={lightbox.src}
          alt={lightbox.alt}
          visible={true}
          onClose={() => setLightbox(null)}
        />
      )}
    </div>
  );
}
