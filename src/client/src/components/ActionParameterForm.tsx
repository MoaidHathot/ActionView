import type { ActionParameter } from '../types';

interface Props {
  parameters: ActionParameter[];
  values: Record<string, string>;
  onChange: (name: string, value: string) => void;
  errors?: Record<string, string>;
  disabled?: boolean;
}

/**
 * Renders a stacked input field for each declared action parameter.
 * Layout mirrors EntryEditor for consistency. Values are controlled by the parent
 * (ActionButton) so the form can be persisted to localStorage as a draft.
 */
export function ActionParameterForm({
  parameters, values, onChange, errors, disabled,
}: Props) {
  return (
    <div className="action-param-form">
      {parameters.map((param) => {
        const value = values[param.name] ?? '';
        const error = errors?.[param.name];
        const fieldId = `param-${param.name}`;
        return (
          <div key={param.name} className="action-param-field">
            <label className="action-param-label" htmlFor={fieldId}>
              {param.label}
              {param.required && <span className="action-param-required" aria-label="required"> *</span>}
            </label>
            {renderInput(param, value, fieldId, disabled, (v) => onChange(param.name, v))}
            {param.helpText && <div className="action-param-help">{param.helpText}</div>}
            {error && <div className="action-param-error">{error}</div>}
          </div>
        );
      })}
    </div>
  );
}

function renderInput(
  param: ActionParameter,
  value: string,
  id: string,
  disabled: boolean | undefined,
  onChange: (v: string) => void,
) {
  switch (param.type) {
    case 'multiline':
      return (
        <textarea
          id={id}
          className="action-param-textarea"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={param.placeholder}
          disabled={disabled}
          rows={6}
        />
      );
    case 'select':
      return (
        <select
          id={id}
          className="action-param-select"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          disabled={disabled}
        >
          {!param.required && <option value="">{param.placeholder ?? '(none)'}</option>}
          {param.options?.map((opt) => (
            <option key={opt} value={opt}>{opt}</option>
          ))}
        </select>
      );
    case 'number':
      return (
        <input
          id={id}
          type="number"
          className="action-param-input"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={param.placeholder}
          disabled={disabled}
        />
      );
    case 'boolean':
      return (
        <label className="action-param-checkbox-label">
          <input
            id={id}
            type="checkbox"
            className="action-param-checkbox"
            checked={value === 'true'}
            onChange={(e) => onChange(e.target.checked ? 'true' : 'false')}
            disabled={disabled}
          />
          <span>{param.placeholder ?? 'Enabled'}</span>
        </label>
      );
    case 'text':
    default:
      return (
        <input
          id={id}
          type="text"
          className="action-param-input"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={param.placeholder}
          disabled={disabled}
        />
      );
  }
}
