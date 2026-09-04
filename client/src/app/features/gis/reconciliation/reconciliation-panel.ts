import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';

import { GisApi } from '@core/api/gis-api';
import { toApiFailure } from '@core/api/problem-details';
import type { ReconciliationReport } from '@core/models/gis';

// Reports drift between the feature layer and the database, in both directions.
@Component({
  selector: 'app-reconciliation-panel',
  standalone: true,
  imports: [DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reconciliation-panel.html',
  styleUrl: './reconciliation-panel.scss',
})
export class ReconciliationPanel {
  private readonly api = inject(GisApi);

  // Rows listed per section. The report can carry thousands; the counts stay exact.
  protected readonly sampleSize = 25;

  protected readonly open = signal(false);
  protected readonly loading = signal(false);
  protected readonly report = signal<ReconciliationReport | null>(null);
  protected readonly failure = signal<string | null>(null);

  protected toggle(): void {
    const next = !this.open();
    this.open.set(next);

    // Always re-run on open: a cached answer would defeat the purpose of the check.
    if (next) {
      this.run();
    }
  }

  protected close(): void {
    this.open.set(false);
  }

  protected run(): void {
    this.loading.set(true);
    this.failure.set(null);

    this.api.reconciliation().subscribe({
      next: (report) => {
        this.report.set(report);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.failure.set(toApiFailure(error).message);
        this.report.set(null);
        this.loading.set(false);
      },
    });
  }
}
