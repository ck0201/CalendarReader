import React from 'react';
import type { ScheduleSuggestion } from '../../core/types/models';

interface SuggestionCardProps {
  suggestion: ScheduleSuggestion;
}

export const SuggestionCard: React.FC<SuggestionCardProps> = ({ suggestion }) => {
  const renderIcon = () => {
    switch (suggestion.severity) {
      case 'warning':
        return (
          <svg className="suggestion-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#ef4444" strokeWidth="2">
            <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
            <line x1="12" y1="9" x2="12" y2="13" />
            <line x1="12" y1="17" x2="12.01" y2="17" />
          </svg>
        );
      case 'success':
        return (
          <svg className="suggestion-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#10b981" strokeWidth="2">
            <circle cx="12" cy="12" r="10" />
            <polyline points="9 11 12 14 22 4" />
          </svg>
        );
      default:
        return (
          <svg className="suggestion-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#6366f1" strokeWidth="2">
            <circle cx="12" cy="12" r="10" />
            <line x1="12" y1="16" x2="12" y2="12" />
            <line x1="12" y1="8" x2="12.01" y2="8" />
          </svg>
        );
    }
  };

  return (
    <div className={`suggestion-card ${suggestion.severity || 'info'}`}>
      {renderIcon()}
      <div className="suggestion-body">
        <span className="suggestion-title">{suggestion.title}</span>
        <span className="suggestion-desc">{suggestion.description}</span>
      </div>
    </div>
  );
};
