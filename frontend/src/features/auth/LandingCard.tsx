import React from 'react';

interface LandingCardProps {
  isConfigured: boolean;
  onDisabledClick: () => void;
}

export const LandingCard: React.FC<LandingCardProps> = ({
  isConfigured,
  onDisabledClick,
}) => {
  return (
    <section className="landing-section">
      <div className="landing-card">
        <div className="landing-badge">Calendar Intelligence</div>
        <h1 className="landing-title">Understand your calendar.</h1>
        <p className="landing-subtitle">
          Connect Google Calendar and discover where your time is going.
        </p>

        <div className="landing-actions">
          {isConfigured ? (
            <a href="/api/auth/login" className="btn btn-primary btn-large">
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
                <path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4" />
                <polyline points="10 17 15 12 10 7" />
                <line x1="15" y1="12" x2="3" y2="12" />
              </svg>
              Connect Google Calendar
            </a>
          ) : (
            <button
              type="button"
              className="btn btn-primary btn-large disabled"
              onClick={onDisabledClick}
            >
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
                <path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4" />
                <polyline points="10 17 15 12 10 7" />
                <line x1="15" y1="12" x2="3" y2="12" />
              </svg>
              Connect Google Calendar
            </button>
          )}
        </div>

        <div className="landing-footer-notes">
          <div className="privacy-note">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
              <path d="M7 11V7a5 5 0 0 1 10 0v4" />
            </svg>
            <span>Read-only calendar access. Never modifies your events. In-memory session.</span>
          </div>
        </div>
      </div>
    </section>
  );
};
