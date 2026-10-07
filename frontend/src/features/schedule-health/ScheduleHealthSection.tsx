import React from 'react';
import type { ScheduleHealthInfo } from '../../core/types/models';
import { HealthRibbon } from './HealthRibbon';
import { ConflictTimeline } from './ConflictTimeline';
import { ConflictCard } from './ConflictCard';
import { SuggestionCard } from './SuggestionCard';

interface ScheduleHealthSectionProps {
  health: ScheduleHealthInfo;
  isMultiAccount: boolean;
}

export const ScheduleHealthSection: React.FC<ScheduleHealthSectionProps> = ({
  health,
  isMultiAccount,
}) => {
  const totalConflicts = health?.totalConflicts || 0;
  const conflicts = health?.conflicts || [];
  const suggestions = health?.suggestions || [];

  return (
    <div className="health-section">
      <div className="health-header">
        <div className="section-badge-header no-margin">
          <svg
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <path d="M22 12h-4l-3 9L9 3l-3 9H2" />
          </svg>
          <h2>Schedule Health &amp; Conflicts</h2>
        </div>
        <span className="badge">
          {totalConflicts} conflict{totalConflicts === 1 ? '' : 's'}
        </span>
      </div>

      <HealthRibbon health={health} />

      {totalConflicts > 0 && <ConflictTimeline conflicts={conflicts} />}

      <div className="health-details-wrapper">
        {suggestions.map((sug, idx) => (
          <SuggestionCard key={idx} suggestion={sug} />
        ))}

        {conflicts.map((conflict) => (
          <ConflictCard
            key={conflict.id}
            conflict={conflict}
            showAccount={isMultiAccount}
          />
        ))}

        {totalConflicts === 0 && suggestions.length === 0 && (
          <div className="health-empty-state">
            <svg
              width="24"
              height="24"
              viewBox="0 0 24 24"
              fill="none"
              stroke="#10b981"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
              <polyline points="22 4 12 14.01 9 11.01" />
            </svg>
            <div>
              <div className="health-empty-title">All commitments are clash-free</div>
              <div className="health-empty-subtitle">
                No overlapping calendar events found in this period.
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
