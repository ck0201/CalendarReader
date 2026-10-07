import React from 'react';
import type { ScheduleConflict } from '../../core/types/models';
import { formatConflictTime } from '../../shared/utils/dateUtils';

interface ConflictCardProps {
  conflict: ScheduleConflict;
  showAccount: boolean;
}

export const ConflictCard: React.FC<ConflictCardProps> = ({
  conflict,
  showAccount,
}) => {
  return (
    <div className="conflict-item-card">
      <div className="conflict-pair-info">
        <div className="conflict-pair-titles">
          <span>{conflict.eventA.title}</span>
          <span className="conflict-clash-arrow">&harr;</span>
          <span>{conflict.eventB.title}</span>
        </div>
        <div className="conflict-pair-times">
          <span>{formatConflictTime(conflict.eventA)}</span>
          {conflict.isCrossAccount && conflict.eventA.accountEmail && (
            <span className="tag tag-account tag-subtle">{conflict.eventA.accountEmail}</span>
          )}{' '}
          vs{' '}
          <span>{formatConflictTime(conflict.eventB)}</span>
          {conflict.isCrossAccount && conflict.eventB.accountEmail && (
            <span className="tag tag-account tag-subtle">{conflict.eventB.accountEmail}</span>
          )}
        </div>
      </div>
      <div className="conflict-badges-group">
        {conflict.isCrossAccount ? (
          <span className="tag tag-cross-account">Cross-Account</span>
        ) : (
          showAccount && conflict.accountEmail && (
            <span className="tag tag-account">{conflict.accountEmail}</span>
          )
        )}
        <span className="conflict-duration-pill">
          {conflict.formattedOverlapDuration} Overlap
        </span>
      </div>
    </div>
  );
};
