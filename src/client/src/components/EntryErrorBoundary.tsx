import { Component, type ErrorInfo, type ReactNode } from 'react';
import { AlertTriangle } from 'lucide-react';

interface Props {
  /** What we were rendering, used in the error UI. Defaults to "block". */
  label?: string;
  children: ReactNode;
}

interface State {
  error: Error | null;
}

/**
 * Error boundary that wraps a single content block (or the entry detail
 * pane) and catches render-time exceptions. Without this, a malformed
 * block payload would blank the whole entry; with it, the bad block
 * shows an inline error card and siblings keep rendering normally.
 *
 * Class component because React error boundaries can't be expressed as
 * hooks today.
 */
export class EntryErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // Surface the failure to the console so the consuming developer
    // can investigate; the UI shows a friendly summary.
    // eslint-disable-next-line no-console
    console.error('[ActionView] Block render failed', error, info);
  }

  render() {
    if (this.state.error) {
      const label = this.props.label ?? 'block';
      return (
        <div className="block-error">
          <div className="block-error-header">
            <AlertTriangle size={14} />
            <span>Failed to render {label}</span>
          </div>
          <div className="block-error-message">
            {this.state.error.message || String(this.state.error)}
          </div>
        </div>
      );
    }
    return this.props.children;
  }
}
