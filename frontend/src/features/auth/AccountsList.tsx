import React from 'react';
import type { ConnectedAccount } from '../../core/types/models';
import { AccountChip } from './AccountChip';

interface AccountsListProps {
  accounts: ConnectedAccount[];
  onDisconnect: (accountId: string, email: string) => void;
}

export const AccountsList: React.FC<AccountsListProps> = ({
  accounts,
  onDisconnect,
}) => {
  return (
    <div className="accounts-list">
      {accounts.map((account) => (
        <AccountChip
          key={account.accountId}
          account={account}
          onDisconnect={onDisconnect}
        />
      ))}
    </div>
  );
};
