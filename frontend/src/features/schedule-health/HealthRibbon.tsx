import React from 'react';
import type { ScheduleHealthInfo } from '../../core/types/models';

interface HealthRibbonProps {
  health: ScheduleHealthInfo;
}

export const HealthRibbon: React.FC<HealthRibbonProps> = ({ health }) => {
  const totalConflicts = health.totalConflicts || 0;
  const conflictDurationStr = health.formattedConflictDuration || '0m';
  const affectedEvents = health.affectedEventsCount || 0;
  const mostClashingDay = health.mostConflictedDay || 'None';

  return (
    <div className="health-ribbon">
      <div className="health-stat-card">
        <span className="health-stat-label">Total Conflicts</span>
        <span className={`health-stat-value ${totalConflicts > 0 ? 'danger' : 'success'}`}>
          {totalConflicts}
        </span>
      </div>
      <div className="health-stat-card">
        <span className="health-stat-label">Conflict Time</span>
        <span className="health-stat-value">{conflictDurationStr}</span>
      </div>
      <div className="health-stat-card">
        <span className="health-stat-label">Affected Events</span>
        <span className="health-stat-value">{affectedEvents}</span>
      </div>
      <div className="health-stat-card">
        <span className="health-stat-label">Most Clashing Day</span>
        <span className="health-stat-value">{mostClashingDay}</span>
      </div>
    </div>
  );
};
