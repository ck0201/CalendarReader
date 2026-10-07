import React from 'react';
import type { CalendarTask } from '../../core/types/models';
import { TaskRow } from './TaskRow';

interface TasksSectionProps {
  tasks: CalendarTask[];
  isMultiAccount: boolean;
}

export const TasksSection: React.FC<TasksSectionProps> = ({
  tasks,
  isMultiAccount,
}) => {
  return (
    <div className="tasks-section">
      <div className="tasks-header">
        <div className="tasks-title-group">
          <div className="section-badge-header no-margin">
            <svg
              width="18"
              height="18"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <polyline points="9 11 12 14 22 4" />
              <path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11" />
            </svg>
            <h2>Tasks in this Period</h2>
          </div>
          <span className="badge">
            {tasks.length} task{tasks.length === 1 ? '' : 's'}
          </span>
        </div>
      </div>

      <div className="tasks-list">
        {tasks.length > 0 ? (
          tasks.map((task) => (
            <TaskRow
              key={task.id}
              task={task}
              isMultiAccount={isMultiAccount}
            />
          ))
        ) : (
          <div className="empty-state">No tasks scheduled in this period.</div>
        )}
      </div>
    </div>
  );
};
