import React from 'react';
import type { AiBriefingResult } from '../../core/types/models';

interface AiBriefingCardProps {
  briefing: AiBriefingResult | null;
  loading: boolean;
  error: string | null;
  onRetry: () => void;
}

export const AiBriefingCard: React.FC<AiBriefingCardProps> = ({
  briefing,
  loading,
  error,
  onRetry,
}) => {
  if (loading) {
    return (
      <div className="briefing-card" aria-busy="true" aria-label="Loading schedule briefing">
        <div className="briefing-card-header">
          <div className="briefing-card-title-group">
            <span className="briefing-icon">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="m12 3-1.912 5.813a2 2 0 0 1-1.275 1.275L3 12l5.813 1.912a2 2 0 0 1 1.275 1.275L12 21l1.912-5.813a2 2 0 0 1 1.275-1.275L21 12l-5.813-1.912a2 2 0 0 1-1.275-1.275L12 3Z" />
              </svg>
            </span>
            <h2 className="briefing-title">Schedule Briefing</h2>
          </div>
        </div>
        <div className="briefing-loading">
          <div className="spinner" />
          <p>Analyzing schedule patterns and generating briefing...</p>
        </div>
        <div className="briefing-skeleton-line" style={{ width: '60%' }} />
        <div className="briefing-skeleton-line" style={{ width: '90%' }} />
        <div className="briefing-skeleton-line" style={{ width: '75%' }} />
      </div>
    );
  }

  if (error) {
    return (
      <div className="briefing-card">
        <div className="briefing-card-header">
          <div className="briefing-card-title-group">
            <span className="briefing-icon">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="m12 3-1.912 5.813a2 2 0 0 1-1.275 1.275L3 12l5.813 1.912a2 2 0 0 1 1.275 1.275L12 21l1.912-5.813a2 2 0 0 1 1.275-1.275L21 12l-5.813-1.912a2 2 0 0 1-1.275-1.275L12 3Z" />
              </svg>
            </span>
            <h2 className="briefing-title">Schedule Briefing</h2>
          </div>
        </div>
        <div className="briefing-error-container">
          <span>{error}</span>
          <button type="button" className="briefing-retry-btn" onClick={onRetry}>
            Retry
          </button>
        </div>
      </div>
    );
  }

  if (!briefing) {
    return null;
  }

  const isAi = !briefing.isFallback;
  const cardClassName = `briefing-card ${isAi ? 'ai-mode' : ''}`.trim();

  return (
    <div className={cardClassName}>
      <div className="briefing-card-header">
        <div className="briefing-card-title-group">
          <span className="briefing-icon">
            {isAi ? (
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="m12 3-1.912 5.813a2 2 0 0 1-1.275 1.275L3 12l5.813 1.912a2 2 0 0 1 1.275 1.275L12 21l1.912-5.813a2 2 0 0 1 1.275-1.275L21 12l-5.813-1.912a2 2 0 0 1-1.275-1.275L12 3Z" />
              </svg>
            ) : (
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect width="18" height="18" x="3" y="4" rx="2" ry="2" />
                <line x1="16" x2="16" y1="2" y2="6" />
                <line x1="8" x2="8" y1="2" y2="6" />
                <line x1="3" x2="21" y1="10" y2="10" />
              </svg>
            )}
          </span>
          <h2 className="briefing-title">{isAi ? 'AI Schedule Briefing' : 'Schedule Summary'}</h2>
        </div>

        <div className="briefing-badges">
          {isAi ? (
            <span className="briefing-badge ai">AI Briefing</span>
          ) : (
            <span className="briefing-badge fallback">Schedule Summary</span>
          )}

          {briefing.isCached && (
            <span className="briefing-badge cached" title="Delivered from bounded server cache">
              Cached
            </span>
          )}
        </div>
      </div>

      {briefing.headline && <h3 className="briefing-headline">{briefing.headline}</h3>}

      {briefing.energyProfile && (
        <div className="briefing-meta-row">
          <div className="briefing-energy-pill">
            <span>⚡</span>
            <span>{briefing.energyProfile}</span>
          </div>
        </div>
      )}

      {briefing.narrativeSummary && (
        <p className="briefing-narrative">{briefing.narrativeSummary}</p>
      )}

      <div className="briefing-sections-grid">
        {briefing.preparationPriorities && briefing.preparationPriorities.length > 0 && (
          <div className="briefing-subcard">
            <h4 className="briefing-subcard-title">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="9 11 12 14 22 4" />
                <path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11" />
              </svg>
              Preparation Priorities
            </h4>
            <ul className="briefing-list">
              {briefing.preparationPriorities.map((item, idx) => (
                <li key={idx} className="briefing-list-item">
                  <span className="briefing-list-item-bullet">✓</span>
                  <span>{item}</span>
                </li>
              ))}
            </ul>
          </div>
        )}

        {briefing.schedulingSuggestions && briefing.schedulingSuggestions.length > 0 && (
          <div className="briefing-subcard">
            <h4 className="briefing-subcard-title">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M15 14c.2-1 .7-1.7 1.5-2.5 1-.9 1.5-2.2 1.5-3.5A6 6 0 0 0 6 8c0 1 .2 2.2 1.5 3.5.7.7 1.3 1.5 1.5 2.5" />
                <path d="M9 18h6" />
                <path d="M10 22h4" />
              </svg>
              Scheduling Suggestions
            </h4>
            <ul className="briefing-list">
              {briefing.schedulingSuggestions.map((item, idx) => (
                <li key={idx} className="briefing-list-item">
                  <span className="briefing-list-item-bullet suggestion">💡</span>
                  <span>{item}</span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  );
};
