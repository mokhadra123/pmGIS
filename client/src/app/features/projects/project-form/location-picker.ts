import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';

import { APP_CONFIG } from '@core/config/app-config';
import { MapBridge } from '@core/state/map-bridge';

import { isInsideBoundary } from './project-validators';

// Project Location.
@Component({
  selector: 'app-location-picker',
  standalone: true,
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './location-picker.html',
  styleUrl: './location-picker.scss',
})
export class LocationPicker {
  readonly form = input.required<FormGroup>();

  protected readonly bridge = inject(MapBridge);
  protected readonly boundaryName = APP_CONFIG.boundary.name;

  protected readonly address = signal<string | null>(null);
  protected readonly geocoding = signal(false);

  // What is in the two boxes, exactly as typed.
  protected readonly latText = signal('');
  protected readonly lonText = signal('');

  protected readonly hasLocation = computed(() => this.bridge.draftLocation() !== null);

  protected readonly outsideBoundary = computed(() => {
    const point = this.bridge.draftLocation();
    return point !== null && !isInsideBoundary(point.latitude, point.longitude);
  });

  constructor() {
    // Map, drag, or a project loaded for edit -> the boxes, the form and the address.
    effect(() => {
      const point = this.bridge.draftLocation();

      untracked(() => {
        if (point) {
          // Only overwrite text that does not already mean this number, so a user
          // mid-way through typing "30.0" is not reformatted under them.
          if (this.parse(this.latText()) !== point.latitude) {
            this.latText.set(String(point.latitude));
          }
          if (this.parse(this.lonText()) !== point.longitude) {
            this.lonText.set(String(point.longitude));
          }
        }

        this.form().patchValue({
          latitude: point?.latitude ?? null,
          longitude: point?.longitude ?? null,
        });
      });

      if (point && isInsideBoundary(point.latitude, point.longitude)) {
        void this.reverseGeocode(point.latitude, point.longitude);
      } else {
        this.address.set(null);
      }
    });
  }

  protected togglePicking(): void {
    this.bridge.pickingLocation() ? this.bridge.stopPicking() : this.bridge.startPicking();
  }

  protected clear(): void {
    this.latText.set('');
    this.lonText.set('');
    this.form().patchValue({ latitude: null, longitude: null });
    this.bridge.clearDraftLocation();
    this.address.set(null);
  }

  // Manual entry.
  protected setCoordinate(which: 'latitude' | 'longitude', raw: string): void {
    which === 'latitude' ? this.latText.set(raw.trim()) : this.lonText.set(raw.trim());

    const latitude = this.parse(this.latText());
    const longitude = this.parse(this.lonText());

    this.form().patchValue({ latitude, longitude });
    this.form().markAsDirty();

    this.bridge.draftLocation.set(
      latitude !== null && longitude !== null ? { latitude, longitude } : null,
    );
  }

  private parse(text: string): number | null {
    if (text.trim() === '') return null;
    const parsed = Number(text);
    return Number.isFinite(parsed) ? parsed : null;
  }

  // Read-only context for the user, never stored.
  private async reverseGeocode(latitude: number, longitude: number): Promise<void> {
    this.geocoding.set(true);
    this.address.set(null);

    try {
      const { locationToAddress } = await import('@arcgis/core/rest/locator');
      const Point = (await import('@arcgis/core/geometry/Point')).default;

      const result = await locationToAddress(APP_CONFIG.arcgis.reverseGeocodeURL, {
        location: new Point({ latitude, longitude }),
      });

      this.address.set(result.address || null);
    } catch {
      this.address.set(null);
    } finally {
      this.geocoding.set(false);
    }
  }
}
