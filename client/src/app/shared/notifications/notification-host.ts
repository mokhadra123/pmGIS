import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { Notifications, type Notice } from './notifications';

@Component({
  selector: 'app-notification-host',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './notification-host.html',
  styleUrl: './notification-host.scss',
})
export class NotificationHost {
  protected readonly notifications = inject(Notifications);

  protected run(notice: Notice): void {
    this.notifications.dismiss(notice.id);
    notice.retry?.();
  }
}
