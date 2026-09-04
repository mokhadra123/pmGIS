import { Injectable, signal } from '@angular/core';
import { Subject, type Observable } from 'rxjs';

import type { Bbox } from '@core/api/project-query';

export interface LatLng {
  readonly latitude: number;
  readonly longitude: number;
}

export interface MapViewState {
  readonly centre: LatLng;
  readonly zoom: number;
}

// The only channel between the map and the rest of the application.
@Injectable({ providedIn: 'root' })
export class MapBridge {
  // ----- selection, synchronized in both directions --------------------------

  // Null when nothing is selected.
  readonly selectedProjectId = signal<number | null>(null);

  private readonly highlightRequests = new Subject<number | null>();
  // Emits when the *list* changed the selection and the map should highlight it.
  readonly highlightRequested: Observable<number | null> = this.highlightRequests.asObservable();

  // Called by the list.
  selectFromList(projectId: number | null): void {
    this.selectedProjectId.set(projectId);
    this.highlightRequests.next(projectId);
  }

  // Called by the map when a point is clicked.
  selectFromMap(projectId: number | null): void {
    this.selectedProjectId.set(projectId);
  }

  // ----- spatial filters -----------------------------------------------------

  // Current map extent, republished as the user pans and zooms.
  readonly extent = signal<Bbox | null>(null);

  // Whether the list is currently constrained to the extent above.
  readonly extentFilterEnabled = signal(false);

  // WKT of the polygon drawn with the Sketch widget.
  readonly sketchWkt = signal<string | null>(null);

  // ----- view state, mirrored into the URL -----------------------------------

  readonly viewState = signal<MapViewState | null>(null);

  private readonly gotoRequests = new Subject<LatLng & { zoom?: number }>();
  readonly gotoRequested: Observable<LatLng & { zoom?: number }> = this.gotoRequests.asObservable();

  // "Zoom to Project", and restoring a shared URL.
  goTo(target: LatLng & { zoom?: number }): void {
    this.gotoRequests.next(target);
  }

  // ----- location picking, driven by the project form ------------------------

  // True while the form has asked the user to click a point on the map.
  readonly pickingLocation = signal(false);

  // The point the form currently holds, shown on the map as a draggable graphic.
  readonly draftLocation = signal<LatLng | null>(null);

  startPicking(): void {
    this.pickingLocation.set(true);
  }

  stopPicking(): void {
    this.pickingLocation.set(false);
  }

  clearDraftLocation(): void {
    this.draftLocation.set(null);
    this.pickingLocation.set(false);
  }

  // ----- nearby search, driven by the nearby panel ---------------------------

  // True while the panel has asked the user to click the centre of the search.
  readonly pickingNearby = signal(false);

  // The point the user clicked. Null until they pick one, or after a reset.
  readonly nearbyPoint = signal<LatLng | null>(null);

  startNearbyPicking(): void {
    this.pickingNearby.set(true);
  }

  clearNearby(): void {
    this.pickingNearby.set(false);
    this.nearbyPoint.set(null);
  }
}
