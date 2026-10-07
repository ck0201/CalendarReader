import { useCallback, useEffect, useState } from 'react';
import { calendarApiClient } from '../../core/api/calendarApiClient';
import type { ConnectedAccount } from '../../core/types/models';

export interface UseAuthReturn {
  isAuthenticated: boolean;
  isConfigured: boolean;
  missingConfigMessage: string | null;
  accounts: ConnectedAccount[];
  loading: boolean;
  errorMessage: string | null;
  clearError: () => void;
  setAlert: (message: string | null, type?: 'info' | 'warning' | 'error') => void;
  alertType: 'info' | 'warning' | 'error';
  disconnectAccount: (accountId: string, email: string) => Promise<boolean>;
  logout: () => Promise<void>;
  refreshAuth: () => Promise<void>;
}

export function useAuth(): UseAuthReturn {
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(false);
  const [isConfigured, setIsConfigured] = useState<boolean>(true);
  const [missingConfigMessage, setMissingConfigMessage] = useState<string | null>(null);
  const [accounts, setAccounts] = useState<ConnectedAccount[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [alertType, setAlertType] = useState<'info' | 'warning' | 'error'>('info');

  const setAlert = useCallback((message: string | null, type: 'info' | 'warning' | 'error' = 'info') => {
    setErrorMessage(message);
    setAlertType(type);
  }, []);

  const clearError = useCallback(() => {
    setErrorMessage(null);
  }, []);

  const refreshAuth = useCallback(async () => {
    try {
      const data = await calendarApiClient.getAuthStatus();
      setIsAuthenticated(data.isAuthenticated);
      setIsConfigured(data.isConfigured);
      setMissingConfigMessage(data.missingConfigMessage ?? null);
      setAccounts(data.accounts || []);

      if (!data.isConfigured) {
        setAlert(
          'Google OAuth is not configured on the server. Please set GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET in your environment.',
          'warning'
        );
      }
    } catch (err: unknown) {
      console.error('Failed to check auth status:', err);
      setIsAuthenticated(false);
      setAccounts([]);
    } finally {
      setLoading(false);
    }
  }, [setAlert]);

  useEffect(() => {
    // Check URL parameters for OAuth errors
    const params = new URLSearchParams(window.location.search);
    if (params.has('error')) {
      const err = params.get('error');
      if (err === 'access_denied') {
        setAlert('Google authorization was cancelled or access was denied.', 'warning');
      } else {
        setAlert(`Authentication error: ${err}`, 'error');
      }
      window.history.replaceState({}, document.title, window.location.pathname);
    }

    refreshAuth();
  }, [refreshAuth, setAlert]);

  const disconnectAccount = useCallback(
    async (accountId: string, email: string): Promise<boolean> => {
      if (!window.confirm(`Disconnect Google account: ${email}?`)) {
        return false;
      }

      try {
        const res = await calendarApiClient.disconnectAccount(accountId);
        if (res.success) {
          setAlert(`Disconnected ${email}.`, 'info');
          await refreshAuth();
          return true;
        } else {
          setAlert(`Failed to disconnect ${email}.`, 'error');
          return false;
        }
      } catch (err: unknown) {
        const msg = err instanceof Error ? err.message : 'Unknown error';
        setAlert(`Error disconnecting account: ${msg}`, 'error');
        return false;
      }
    },
    [refreshAuth, setAlert]
  );

  const logout = useCallback(async () => {
    if (accounts.length > 0 && !window.confirm('Are you sure you want to disconnect all Google accounts?')) {
      return;
    }

    try {
      await calendarApiClient.logout();
    } catch (err) {
      console.error('Logout error:', err);
    }

    setIsAuthenticated(false);
    setAccounts([]);
    clearError();
  }, [accounts.length, clearError]);

  return {
    isAuthenticated,
    isConfigured,
    missingConfigMessage,
    accounts,
    loading,
    errorMessage,
    clearError,
    setAlert,
    alertType,
    disconnectAccount,
    logout,
    refreshAuth,
  };
}
