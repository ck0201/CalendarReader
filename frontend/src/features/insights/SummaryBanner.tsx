import React from 'react';
import type { CalendarInsightResult } from '../../core/types/models';

interface SummaryBannerProps {
  insights: CalendarInsightResult;
}

export const SummaryBanner: React.FC<SummaryBannerProps> = ({ insights }) => {
  const metaParts: string[] = [];
  if (insights.allDayEventsCount > 0) {
    metaParts.push(
      `${insights.allDayEventsCount} all-day event${insights.allDayEventsCount === 1 ? '' : 's'}`
    );
  }
  if (insights.recurringTime && insights.recurringTime.recurringEventCount > 0) {
    metaParts.push(`${insights.recurringTime.percentageOfScheduledTime}% recurring`);
  }

  const conflictsCount = insights.scheduleHealth?.totalConflicts || 0;
  const conflictDuration = insights.scheduleHealth?.formattedConflictDuration || '0m';

  return (
    <div className="summary-banner">
      <div className="summary-stat">
        <span className="summary-text">
          {insights.totalEvents} event{insights.totalEvents === 1 ? '' : 's'} ·{' '}
          {insights.formattedScheduledTime || '0m'} scheduled
        </span>
        {conflictsCount > 0 ? (
          <span className="health-pill warning">
            ⚠️ {conflictsCount} Conflict{conflictsCount === 1 ? '' : 's'} ({conflictDuration})
          </span>
        ) : (
          <span className="health-pill healthy">✓ Clash-Free</span>
        )}
      </div>
      <div className="summary-meta">{metaParts.join(' · ')}</div>
    </div>
  );
};
