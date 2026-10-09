import React, { useState } from 'react';
import type { RangeType } from '../core/types/models';
import { useAuth } from '../features/auth/useAuth';
import { Header } from '../features/auth/Header';
import { LandingCard } from '../features/auth/LandingCard';
import { ReauthBanner } from '../features/auth/ReauthBanner';
import { useCalendarInsights } from '../features/insights/useCalendarInsights';
import { useCalendars } from '../features/calendar/useCalendars';
import { CalendarFilterBar } from '../features/calendar/CalendarFilterBar';
import { SummaryBanner } from '../features/insights/SummaryBanner';
import { useAiBriefing } from '../features/briefing/useAiBriefing';
import { AiBriefingCard } from '../features/briefing/AiBriefingCard';
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
  const accountsKey = accounts.map((a) => `${a.accountId}:${a.requiresReauth}`).join(',');

  const {
    calendars,
    selectedCalendarIds,
    loading: calendarsLoading,
    error: calendarsError,
    toggleCalendar,
    selectAll,
    selectPrimaryOnly,
  } = useCalendars(isAuthenticated, accountsKey);

  const {
    insights,
    loading: insightsLoading,
    error: insightsError,
    refetch: refetchInsights,
    conflictingEventIds,
  } = useCalendarInsights(range, isAuthenticated, accountsKey, selectedCalendarIds);

  const {
    briefing,
    loading: briefingLoading,
    error: briefingError,
    refetch: refetchBriefing,
  } = useAiBriefing(range, isAuthenticated, accountsKey, selectedCalendarIds);

  const { start, end } = getDateRange(range);
  const rangeSubtitle = formatRangeLabel(start, end, range);

  const handleDisabledConnectClick = () => {
    setAlert('Google OAuth credentials must be configured before connecting.', 'error');
  };

  const handleDisconnectAccount = async (accountId: string, email: string) => {
    const success = await disconnectAccount(accountId, email);
    if (success) {
      await Promise.all([refetchInsights(), refetchBriefing()]);
    }
  };

  const isMultiAccount = accounts.length > 1;

  return (
    <>
      <Header
        isAuthenticated={isAuthenticated}
        accounts={accounts}
        onDisconnectAccount={handleDisconnectAccount}
        onLogout={logout}
      />

      <div className="container">
        <AlertBanner
          message={errorMessage || insightsError || calendarsError}
          type={insightsError || calendarsError ? 'error' : alertType}
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

              <CalendarFilterBar
                calendars={calendars}
                selectedCalendarIds={selectedCalendarIds}
                onToggleCalendar={toggleCalendar}
                onSelectAll={selectAll}
                onSelectPrimaryOnly={selectPrimaryOnly}
                loading={calendarsLoading}
              />

              {insightsLoading && (
                <div className="loading-state">
                  <div className="spinner" />
                  <p>Analyzing calendar events...</p>
                </div>
              )}

              {insights && !insightsLoading && (
                <div id="insights-content">
                  <SummaryBanner insights={insights} />

                  <AiBriefingCard
                    briefing={briefing}
                    loading={briefingLoading}
                    error={briefingError}
                    onRetry={refetchBriefing}
                  />

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
                    selectedCalendarIds={selectedCalendarIds}
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
