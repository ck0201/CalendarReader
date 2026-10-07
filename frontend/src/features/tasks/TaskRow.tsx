import React from 'react';
import type { CalendarTask } from '../../core/types/models';
import { formatTaskDue } from '../../shared/utils/dateUtils';

interface TaskRowProps {
  task: CalendarTask;
  isMultiAccount: boolean;
}

export const TaskRow: React.FC<TaskRowProps> = ({ task, isMultiAccount }) => {
  const formattedDue = formatTaskDue(task);

  return (
    <div className="task-row">
      <div className="task-info">
        <div
          className={`task-checkbox ${task.isCompleted ? 'completed' : ''}`}
          title={task.isCompleted ? 'Completed' : 'Pending'}
        >
          {task.isCompleted && (
            <svg
              width="12"
              height="12"
              viewBox="0 0 24 24"
              fill="none"
              stroke="#ffffff"
              strokeWidth="3"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <polyline points="20 6 9 17 4 12" />
            </svg>
          )}
        </div>
        <span className={`task-title ${task.isCompleted ? 'completed' : ''}`}>
          {task.title}
        </span>
        {isMultiAccount && task.accountEmail && (
          <span className="task-account">{task.accountEmail}</span>
        )}
      </div>

      {formattedDue && (
        <div className="task-meta">
          <span className="task-due">{formattedDue}</span>
        </div>
      )}
    </div>
  );
};
