import { useState, useEffect, useRef } from 'react';
import type { ContentBlock } from '../../types';

interface Props {
  block: ContentBlock;
  /** The unknown block type string */
  blockType: string;
}

/**
 * PluginBlockWrapper renders custom content block types registered via the plugin system.
 *
 * Plugin scripts register themselves by calling:
 *   window.__actionview_plugins.register('my-block-type', (container, block) => { ... })
 *
 * If no plugin is registered for the block type, a fallback is shown.
 */
export function PluginBlockWrapper({ block, blockType }: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [hasPlugin, setHasPlugin] = useState(false);

  useEffect(() => {
    const plugins = (window as PluginWindow).__actionview_plugins;
    if (plugins && typeof plugins.render === 'function') {
      const rendered = plugins.render(containerRef.current!, blockType, block);
      setHasPlugin(!!rendered);
    }
  }, [block, blockType]);

  if (!hasPlugin) {
    return (
      <div className="block-plugin-fallback">
        <div className="block-plugin-type">Plugin: {blockType}</div>
        {block.label && <div className="block-label">{block.label}</div>}
        <pre className="json-content">{JSON.stringify(block.body ?? block, null, 2)}</pre>
      </div>
    );
  }

  return <div ref={containerRef} className="block-plugin" />;
}

interface PluginRegistry {
  render: (container: HTMLElement, type: string, block: ContentBlock) => boolean;
  register: (type: string, renderer: (container: HTMLElement, block: ContentBlock) => void) => void;
}

interface PluginWindow extends Window {
  __actionview_plugins?: PluginRegistry;
}

// Initialize the global plugin registry
if (typeof window !== 'undefined') {
  const win = window as PluginWindow;
  if (!win.__actionview_plugins) {
    const renderers = new Map<string, (container: HTMLElement, block: ContentBlock) => void>();
    win.__actionview_plugins = {
      register(type, renderer) {
        renderers.set(type, renderer);
      },
      render(container, type, block) {
        const renderer = renderers.get(type);
        if (!renderer) return false;
        renderer(container, block);
        return true;
      },
    };
  }
}
