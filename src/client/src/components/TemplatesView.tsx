import { useState, useEffect, useCallback } from 'react';
import { FileText, Trash2, Upload, Cpu, ChevronDown, ChevronRight } from 'lucide-react';
import type { EntryTemplate, ContentBlock, ContentTemplateBlock } from '../types';
import { api } from '../api/client';
import { BlockRenderer } from './content-blocks/BlockRenderer';

export function TemplatesView() {
  const [templates, setTemplates] = useState<EntryTemplate[]>([]);
  const [autoDiscoveredTypes, setAutoDiscoveredTypes] = useState<Set<string>>(new Set());
  const [selectedType, setSelectedType] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showPreview, setShowPreview] = useState(false);

  const loadTemplates = useCallback(async () => {
    setLoading(true);
    try {
      const [templatesData, adTypes] = await Promise.all([
        api.getTemplates(),
        api.getAutoDiscoveredTypes(),
      ]);
      setTemplates(templatesData);
      setAutoDiscoveredTypes(new Set(adTypes.map((t) => t.toLowerCase())));
    } catch (err) {
      console.error('Failed to load templates:', err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadTemplates();
  }, [loadTemplates]);

  const selectedTemplate = templates.find(
    (t) => t.type === selectedType,
  ) ?? null;

  const handleDelete = useCallback(async (type: string) => {
    if (!window.confirm(`Delete template "${type}"? This cannot be undone.`)) return;
    try {
      await api.deleteTemplate(type);
      setTemplates((prev) => prev.filter((t) => t.type !== type));
      if (selectedType === type) setSelectedType(null);
      setError(null);
    } catch (err) {
      setError(`Failed to delete template: ${err}`);
    }
  }, [selectedType]);

  const handleUpload = useCallback(async () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json';
    input.onchange = async () => {
      const file = input.files?.[0];
      if (!file) return;
      try {
        const text = await file.text();
        const parsed = JSON.parse(text) as EntryTemplate;
        if (!parsed.type) {
          setError('Invalid template: missing "type" field');
          return;
        }
        await api.createTemplate(parsed);
        setError(null);
        await loadTemplates();
        setSelectedType(parsed.type);
      } catch (err) {
        setError(`Failed to upload template: ${err}`);
      }
    };
    input.click();
  }, [loadTemplates]);

  const isAutoDiscovered = (type: string) =>
    autoDiscoveredTypes.has(type.toLowerCase());

  return (
    <div className="templates-view">
      <div className="template-list">
        <div className="template-list-header">
          <span className="template-list-title">Templates</span>
          <button
            className="template-upload-btn"
            onClick={handleUpload}
            title="Upload template JSON file"
          >
            <Upload size={14} />
            Upload
          </button>
        </div>

        {error && (
          <div className="template-error">
            {error}
            <button className="template-error-close" onClick={() => setError(null)}>&times;</button>
          </div>
        )}

        {loading ? (
          <div className="loading">Loading templates...</div>
        ) : templates.length === 0 ? (
          <div className="template-list-empty">
            <FileText size={32} />
            <p>No templates registered</p>
          </div>
        ) : (
          templates.map((template) => (
            <div
              key={template.type}
              className={`template-list-item ${selectedType === template.type ? 'selected' : ''}`}
              onClick={() => {
                setSelectedType(template.type);
                setShowPreview(false);
              }}
            >
              <div className="template-list-item-content">
                <div className="template-list-item-header">
                  <span className="template-list-item-type">{template.type}</span>
                  {isAutoDiscovered(template.type) && (
                    <span className="auto-discovered-badge" title="Auto-discovered from external templates directory">
                      <Cpu size={10} />
                      auto
                    </span>
                  )}
                </div>
                <div className="template-list-item-desc">
                  {template.description ?? 'No description'}
                </div>
              </div>
            </div>
          ))
        )}
      </div>

      <div className="template-detail-panel">
        {selectedTemplate ? (
          <TemplateDetail
            template={selectedTemplate}
            isAutoDiscovered={isAutoDiscovered(selectedTemplate.type)}
            onDelete={handleDelete}
            showPreview={showPreview}
            onTogglePreview={() => setShowPreview((p) => !p)}
          />
        ) : (
          <div className="no-selection">
            <FileText size={48} strokeWidth={1} />
            <p>Select a template to view</p>
          </div>
        )}
      </div>
    </div>
  );
}

// --- Template Detail ---

interface TemplateDetailProps {
  template: EntryTemplate;
  isAutoDiscovered: boolean;
  onDelete: (type: string) => void;
  showPreview: boolean;
  onTogglePreview: () => void;
}

function TemplateDetail({ template, isAutoDiscovered, onDelete, showPreview, onTogglePreview }: TemplateDetailProps) {
  return (
    <div className="template-detail">
      <div className="template-detail-header">
        <div className="template-detail-title-row">
          <h2>{template.type}</h2>
          <div className="template-detail-actions">
            {isAutoDiscovered && (
              <span className="auto-discovered-badge" title="Auto-discovered from external templates directory">
                <Cpu size={10} />
                auto-discovered
              </span>
            )}
            <button
              className="action-btn action-danger-outline"
              onClick={() => onDelete(template.type)}
            >
              <Trash2 size={14} /> Delete
            </button>
          </div>
        </div>
        {template.description && (
          <p className="template-detail-description">{template.description}</p>
        )}
      </div>

      {/* Defaults */}
      {template.defaults && (
        <div className="template-section">
          <h3>Defaults</h3>
          <div className="template-defaults">
            {template.defaults.icon && (
              <div className="template-default-item">
                <span className="template-default-label">Icon</span>
                <span className="template-default-value">{template.defaults.icon}</span>
              </div>
            )}
            {template.defaults.severity && (
              <div className="template-default-item">
                <span className="template-default-label">Severity</span>
                <span className={`severity-badge severity-${template.defaults.severity}`}>
                  {template.defaults.severity}
                </span>
              </div>
            )}
            {template.defaults.tags && template.defaults.tags.length > 0 && (
              <div className="template-default-item">
                <span className="template-default-label">Tags</span>
                <span className="template-default-value">
                  {template.defaults.tags.map((tag) => (
                    <span key={tag} className="tag">{tag}</span>
                  ))}
                </span>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Content Template */}
      {template.contentTemplate && template.contentTemplate.length > 0 && (
        <div className="template-section">
          <h3>Content Blocks</h3>
          <table className="template-table">
            <thead>
              <tr>
                <th>Type</th>
                <th>Label / Title</th>
                <th>Required</th>
                <th>Details</th>
              </tr>
            </thead>
            <tbody>
              {template.contentTemplate.map((block, i) => (
                <tr key={i}>
                  <td><code>{block.type}</code></td>
                  <td>{block.label ?? block.title ?? '-'}</td>
                  <td>{block.required ? 'Yes' : 'No'}</td>
                  <td className="template-block-details">
                    {block.requiredKeys && block.requiredKeys.length > 0 && (
                      <span>Keys: {block.requiredKeys.join(', ')}</span>
                    )}
                    {block.titleAliases && block.titleAliases.length > 0 && (
                      <span>Aliases: {block.titleAliases.join(', ')}</span>
                    )}
                    {block.keyAliases && Object.keys(block.keyAliases).length > 0 && (
                      <span>{Object.keys(block.keyAliases).length} key aliases</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Expected Actions */}
      {template.expectedActions && template.expectedActions.length > 0 && (
        <div className="template-section">
          <h3>Expected Actions</h3>
          <div className="template-actions-list">
            {template.expectedActions.map((action, i) => (
              <span
                key={i}
                className={`action-btn action-${action.style ?? 'default'} template-action-preview`}
              >
                {action.label}
              </span>
            ))}
          </div>
        </div>
      )}

      {/* Preview toggle */}
      <div className="template-section">
        <button className="template-preview-toggle" onClick={onTogglePreview}>
          {showPreview ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
          <h3>Entry Preview</h3>
        </button>
        {showPreview && <TemplatePreview template={template} />}
      </div>
    </div>
  );
}

// --- Template Preview (fake entry rendering) ---

function prettifyType(type: string): string {
  return type
    .split(/[-_]/)
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ');
}

function buildFakeContentBlocks(blocks?: ContentTemplateBlock[]): ContentBlock[] {
  if (!blocks || blocks.length === 0) return [];

  return blocks.map((block): ContentBlock => {
    switch (block.type) {
      case 'keyValue': {
        const pairs: Record<string, string> = {};
        if (block.requiredKeys) {
          for (const key of block.requiredKeys) {
            pairs[key] = `(example ${key.toLowerCase()})`;
          }
        } else {
          pairs['Key'] = '(example value)';
        }
        return { type: 'keyValue', label: block.label, pairs };
      }
      case 'markdown':
        return {
          type: 'markdown',
          label: block.label,
          body: `*Example content for ${block.label ?? 'this section'}.*`,
        };
      case 'section':
        return {
          type: 'section',
          title: block.title ?? block.label ?? 'Section',
          content: [{
            type: 'markdown',
            body: `*Example content for the "${block.title ?? block.label}" section.*`,
          }],
        };
      case 'link':
        return {
          type: 'link',
          label: block.label ?? 'Link',
          url: 'https://example.com',
        };
      case 'code':
        return {
          type: 'code',
          label: block.label,
          body: '// Example code block\nconsole.log("hello");',
          language: 'javascript',
        };
      case 'table':
        return {
          type: 'table',
          label: block.label,
          columns: ['Column A', 'Column B'],
          rows: [['Value 1', 'Value 2']],
        };
      case 'alert':
        return {
          type: 'alert',
          label: block.label,
          level: 'info',
          body: `Example alert for ${block.label ?? 'this block'}.`,
        };
      default:
        return {
          type: 'markdown',
          label: block.label,
          body: `*(Placeholder for ${block.type} block)*`,
        };
    }
  });
}

interface TemplatePreviewProps {
  template: EntryTemplate;
}

function TemplatePreview({ template }: TemplatePreviewProps) {
  const fakeContent = buildFakeContentBlocks(template.contentTemplate);

  return (
    <div className="template-preview">
      <div className="template-preview-header">
        <div className="template-preview-title">
          {template.defaults?.icon && (
            <span className="template-preview-icon">{template.defaults.icon}</span>
          )}
          <h4>{prettifyType(template.type)}</h4>
        </div>
        <div className="template-preview-meta">
          {template.defaults?.severity && (
            <span className={`severity-badge severity-${template.defaults.severity}`}>
              {template.defaults.severity}
            </span>
          )}
          {template.defaults?.tags?.map((tag) => (
            <span key={tag} className="tag">{tag}</span>
          ))}
        </div>
      </div>

      <div className="template-preview-content">
        {fakeContent.map((block, i) => (
          <BlockRenderer key={i} block={block} entryId="preview" />
        ))}
      </div>

      {template.expectedActions && template.expectedActions.length > 0 && (
        <div className="template-preview-actions">
          {template.expectedActions.map((action, i) => (
            <span
              key={i}
              className={`action-btn action-${action.style ?? 'default'} template-action-preview`}
            >
              {action.label}
            </span>
          ))}
        </div>
      )}
    </div>
  );
}
