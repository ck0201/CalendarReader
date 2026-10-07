import React from 'react';
import type { CalendarInsightResult } from '../../core/types/models';
import { MetricCard } from '../../shared/components/MetricCard';

interface MetricsGridProps {
  insights: CalendarInsightResult;
}

export const MetricsGrid: React.FC<MetricsGridProps> = ({ insights }) => {
  // Meeting sub-caption
  let meetingSub = 'Collaborative sessions';
  if (insights.recurringTime && insights.recurringTime.percentageOfScheduledTime > 0) {
    meetingSub = `${insights.recurringTime.percentageOfScheduledTime}% recurring time`;
  }

  // Focus sub-caption
  let focusVal = '0m';
  let focusSub = 'No daytime focus block';
  if (
    insights.longestFocusBlock &&
    insights.longestFocusBlock.duration !== '00:00:00' &&
    insights.longestFocusBlock.formattedDuration !== '0m'
  ) {
    focusVal = insights.longestFocusBlock.formattedDuration;
    focusSub = insights.longestFocusBlock.dayOfWeek;
    if (insights.longestFocusBlock.start && insights.longestFocusBlock.end) {
      const sTime = new Date(insights.longestFocusBlock.start).toLocaleTimeString([], {
        hour: 'numeric',
        minute: '2-digit',
        hour12: true,
      });
      const eTime = new Date(insights.longestFocusBlock.end).toLocaleTimeString([], {
        hour: 'numeric',
        minute: '2-digit',
        hour12: true,
      });
      focusSub += ` (${sTime} – ${eTime})`;
    }
  }

  // Busiest day sub-caption
  let busiestVal = '-';
  let busiestSub = 'No events';
  if (insights.busiestDay && insights.busiestDay.dayOfWeek !== 'None') {
    busiestVal = insights.busiestDay.dayOfWeek;
    busiestSub = `${insights.busiestDay.formattedScheduledTime} scheduled (${insights.busiestDay.eventCount} event${insights.busiestDay.eventCount === 1 ? '' : 's'})`;
  }

  return (
    <div className="metrics-grid">
      <MetricCard
        label="Scheduled Time"
        value={insights.formattedScheduledTime || '0m'}
        caption="Total timed commitments"
        iconType="scheduled"
      />
      <MetricCard
        label="Meeting Time"
        value={insights.formattedMeetingTime || '0m'}
        caption={meetingSub}
        iconType="meetings"
      />
      <MetricCard
        label="Longest Focus Block"
        value={focusVal}
        caption={focusSub}
        iconType="focus"
      />
      <MetricCard
        label="Busiest Day"
        value={busiestVal}
        caption={busiestSub}
        iconType="busy"
      />
    </div>
  );
};
