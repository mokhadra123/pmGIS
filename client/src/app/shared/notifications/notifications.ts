import { Injectable, signal } from '@angular/core';

export type NoticeKind = 'error' | 'success' | 'info';

export interface Notice {
  readonly id: number;
  readonly kind: NoticeKind;
  readonly message: string;
  // When present the toast shows a button that runs this and dismisses itself.
  readonly retry?: () => void;
  readonly retryLabel?: string;
}

// Non-blocking messages.
@Injectable({ providedIn: 'root' })
export class Notifications {
  private nextId = 1;
  private readonly _notices = signal<readonly Notice[]>([]);
  readonly notices = this._notices.asReadonly();

  private push(notice: Omit<Notice, 'id'>, autoDismissMs: number | null): number {
    const id = this.nextId++;
    this._notices.update((current) => [...current, { ...notice, id }]);

    if (autoDismissMs !== null) {
      setTimeout(() => this.dismiss(id), autoDismissMs);
    }

    return id;
  }

  // Errors persist: the user decides when they have read them.
  error(message: string, retry?: () => void, retryLabel = 'Retry'): number {
    return this.push({ kind: 'error', message, retry, retryLabel }, null);
  }

  success(message: string): number {
    return this.push({ kind: 'success', message }, 4000);
  }

  info(message: string): number {
    return this.push({ kind: 'info', message }, 5000);
  }

  dismiss(id: number): void {
    this._notices.update((current) => current.filter((n) => n.id !== id));
  }

  clear(): void {
    this._notices.set([]);
  }
}
