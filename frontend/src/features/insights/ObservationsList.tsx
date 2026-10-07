import React from 'react';

interface ObservationsListProps {
  observations: string[];
}

export const ObservationsList: React.FC<ObservationsListProps> = ({
  observations,
}) => {
  return (
    <div className="observations-section">
      <div className="section-badge-header">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z" />
          <path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z" />
        </svg>
        <h2>What your calendar says</h2>
      </div>

      <div className="observations-container">
        {observations && observations.length > 0 ? (
          observations.map((obs, idx) => (
            <div key={idx} className="observation-item">
              <div className="observation-dot" />
              <div>{obs}</div>
            </div>
          ))
        ) : (
          <div className="empty-state">No observations available.</div>
        )}
      </div>
    </div>
  );
};
