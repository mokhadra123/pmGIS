import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { NotificationHost } from '@shared/notifications/notification-host';

import { ReconciliationPanel } from './features/gis/reconciliation/reconciliation-panel';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, NotificationHost, ReconciliationPanel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly title = signal('client');
}
