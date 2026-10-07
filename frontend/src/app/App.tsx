import React, { useState } from 'react';
import type { RangeType } from '../core/types/models';
import { useAuth } from '../features/auth/useAuth';
import { Header } from '../features/auth/Header';
import { LandingCard } from '../features/auth/LandingCard';
import { ReauthBanner } from '../features/auth/ReauthBanner';
import { useCalendarInsights } from '../features/insights/useCalendarInsights';
import { SummaryBanner } from '../features/insights/SummaryBanner';
import { MetricsGrid } from '../features/insights/MetricsGrid';
import { ObservationsList } from '../features/insights/ObservationsList';
import { ScheduleHealthSection } from '../features/schedule-health/ScheduleHealthSection';
import { TasksSection } from '../features/tasks/TasksSection';
import { EventsSection } from '../features/calendar/EventsSection';
import { AlertBanner } from '../shared/components/AlertBanner';
import { formatRangeLabel, getDateRange } from '../shared/utils/dateUtils';

export const App: React.FC = () => {
  const {
    isAuthenticated,
    isConfigured,
    accounts,
    loading: authLoading,
    errorMessage,
    alertType,
    clearError,
    setAlert,
    disconnectAccount,
    logout,
  } = useAuth();

  const [range, setRange] = useState<RangeType>('week');
  const {
    insights,
    loading: insightsLoading,
    error: insightsError,
    conflictingEventIds,
  } = useCalendarInsights(range, isAuthenticated);

  const { start, end } = getDateRange(range);
  const rangeSubtitle = formatRangeLabel(start, end, range);

  const handleDisabledConnectClick = () => {
    setAlert('Google OAuth credentials must be configured before connecting.', 'error');
  };

  const isMultiAccount = accounts.length > 1;

  return (
    <>
      <Header
        isAuthenticated={isAuthenticated}
        accounts={accounts}
        onDisconnectAccount={disconnectAccount}
        onLogout={logout}
      />

      <div className="container">
        <AlertBanner
          message={errorMessage || insightsError}
          type={insightsError ? 'error' : alertType}
          onClose={clearError}
        />
      </div>

      <main className="main-content">
        {authLoading ? (
          <div className="loading-state">
            <div className="spinner" />
            <p>Checking authentication...</p>
          </div>
        ) : !isAuthenticated ? (
          <LandingCard
            isConfigured={isConfigured}
            onDisabledClick={handleDisabledConnectClick}
          />
        ) : (
          <section className="dashboard-section">
            <div className="container">
              <ReauthBanner accounts={accounts} />

              <div className="dashboard-controls-bar">
                <div>
                  <h1 className="section-title">Calendar Insights</h1>
                  <p className="section-subtitle">{rangeSubtitle}</p>
                </div>

                <div className="time-range-selector">
                  <button
                    type="button"
                    className={`range-btn ${range === 'week' ? 'active' : ''}`}
                    onClick={() => setRange('week')}
                  >
                    This Week
                  </button>
                  <button
                    type="button"
                    className={`range-btn ${range === 'today' ? 'active' : ''}`}
                    onClick={() => setRange('today')}
                  >
                    Today
                  </button>
                </div>
              </div>

              {insightsLoading && (
                <div className="loading-state">
                  <div className="spinner" />
                  <p>Analyzing calendar events...</p>
                </div>
              )}

              {insights && !insightsLoading && (
                <div id="insights-content">
                  <SummaryBanner insights={insights} />

                  <MetricsGrid insights={insights} />

                  <ObservationsList observations={insights.observations || []} />

                  <ScheduleHealthSection
                    health={insights.scheduleHealth}
                    isMultiAccount={isMultiAccount}
                  />

                  <TasksSection
                    tasks={insights.tasks || []}
                    isMultiAccount={isMultiAccount}
                  />

                  <EventsSection
                    events={insights.events || []}
                    conflictingEventIds={conflictingEventIds}
                    isMultiAccount={isMultiAccount}
                  />
                </div>
              )}
            </div>
          </section>
        )}
      </main>

      <footer className="app-footer">
        <div className="container">
          <p>CalendarReader · Minimal, transparent calendar intelligence</p>
        </div>
      </footer>
    </>
  );
};

export default App;
